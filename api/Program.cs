using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using CareLanka.Api.Common.Auth;
using CareLanka.Api.Common.Errors;
using CareLanka.Api.Common.OpenApi;
using CareLanka.Api.Common.Persistence;
using CareLanka.Api.Data;
using CareLanka.Api.Data.Enums;
using CareLanka.Api.Services.Common;
using CareLanka.Api.Services.Equipment;
using CareLanka.Api.Services.Equipment.Stubs;
using CareLanka.Api.Services.Emergency;
using CareLanka.Api.Services.Emergency.Stubs;
using CareLanka.Api.Services.Patient;
using FluentValidation;
using FluentValidation.AspNetCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Formatters;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using CareLanka.Api.Common.Serialization;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

static void ConfigureJson(JsonSerializerOptions json)
{
    json.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower;
    json.DictionaryKeyPolicy = JsonNamingPolicy.SnakeCaseLower;
    json.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower));
    json.Converters.Add(new DateOnlyJsonConverter());
}

builder.Services
    .AddControllers(options =>
    {
        options.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true;
        options.ModelMetadataDetailsProviders.Add(new EmergencyQueryBindingMetadataProvider());
    })
    .AddJsonOptions(options => ConfigureJson(options.JsonSerializerOptions));

builder.Services.AddFluentValidationAutoValidation();
builder.Services.AddValidatorsFromAssemblyContaining<Program>();

builder.Services.ConfigureHttpJsonOptions(options => ConfigureJson(options.SerializerOptions));

builder.Services.Configure<MvcOptions>(options =>
{
    options.OutputFormatters.RemoveType<StringOutputFormatter>();

    foreach (var formatter in options.OutputFormatters.OfType<SystemTextJsonOutputFormatter>())
    {
        formatter.SupportedMediaTypes.Remove("text/json");
    }
});

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();

builder.Services.Configure<ApiBehaviorOptions>(options =>
{
    options.InvalidModelStateResponseFactory = context =>
    {
        var problem = new ValidationProblemDetails(context.ModelState)
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "Bad Request",
            Detail = MessageCode.ValidationFailed.ToText()
        };

        problem.WithCareLankaExtensions(context.HttpContext, MessageCode.ValidationFailed);

        return new BadRequestObjectResult(problem)
        {
            ContentTypes = { "application/problem+json" }
        };
    };
});

var connectionString = builder.Configuration.GetConnectionString("CareLanka")
    ?? throw new InvalidOperationException(
        "ConnectionStrings:CareLanka is not configured. See api/README.md for local setup.");

builder.Services.AddSingleton<TimestampInterceptor>();

builder.Services.AddDbContext<CareLankaDbContext>((provider, options) => options
    .UseNpgsql(connectionString)
    .UseSnakeCaseNamingConvention()
    .AddInterceptors(provider.GetRequiredService<TimestampInterceptor>()));

builder.Services
    .AddOptions<JwtOptions>()
    .Bind(builder.Configuration.GetSection(JwtOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services
    .AddOptions<EmergencyOptions>()
    .Bind(builder.Configuration.GetSection(EmergencyOptions.SectionName))
    .Validate(options => options.MinimumReadyCrew > 0,
        "Emergency:MinimumReadyCrew must be greater than zero.")
    .Validate(options => options.LocationMaxAgeMinutes > 0,
        "Emergency:LocationMaxAgeMinutes must be greater than zero.")
    .ValidateOnStart();

builder.Services
    .AddOptions<EquipmentOptions>()
    .Bind(builder.Configuration.GetSection(EquipmentOptions.SectionName))
    .Validate(options => !string.IsNullOrWhiteSpace(options.ConfirmationCode),
        "Equipment:ConfirmationCode must be set.")
    .ValidateOnStart();

var jwt = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();

JsonWebTokenHandler.DefaultInboundClaimTypeMap.Clear();

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;
        options.RequireHttpsMetadata = !builder.Environment.IsDevelopment();

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwt.Issuer,
            ValidateAudience = true,
            ValidAudience = jwt.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(string.IsNullOrEmpty(jwt.SigningKey)
                    ? new string('x', 32)   // never reached: ValidateOnStart fails first
                    : jwt.SigningKey)),
            ValidateLifetime = true,

            ClockSkew = TimeSpan.Zero,

            NameClaimType = CareLankaClaims.Subject,
            RoleClaimType = CareLankaClaims.Role
        };

        options.Events = new JwtBearerEvents
        {
            OnChallenge = async context =>
            {
                context.HandleResponse();
                await ProblemResponseWriter.WriteAsync(
                    context.HttpContext, StatusCodes.Status401Unauthorized,
                    "Unauthorized", MessageCode.NotAuthenticated);
            },
            OnForbidden = context => ProblemResponseWriter.WriteAsync(
                context.HttpContext, StatusCodes.Status403Forbidden,
                "Forbidden", MessageCode.Forbidden)
        };
    });

builder.Services.AddAuthorization(options =>
{
    foreach (var role in Enum.GetValues<StaffRole>())
    {
        options.AddPolicy(role.ToString(), policy => policy.RequireRole(EnumWire.ToWire(role)));
    }

    var allStaffRoles = Enum.GetValues<StaffRole>().Select(EnumWire.ToWire).ToArray();

    options.AddPolicy(Policies.AnyStaff, policy => policy.RequireRole(allStaffRoles));

    options.AddPolicy(Policies.PatientOnly,
        policy => policy.RequireRole(EnumWire.ToWire(PrincipalRole.Patient)));

    options.AddPolicy(Policies.EmergencyResponder, policy => policy.RequireRole(
        EnumWire.ToWire(StaffRole.DutyManager),
        EnumWire.ToWire(StaffRole.AmbulanceCrew)));

    options.AddPolicy(Policies.WorkflowReader, policy => policy.RequireRole(
        EnumWire.ToWire(StaffRole.DutyManager),
        EnumWire.ToWire(StaffRole.HospitalAdministrator),
        EnumWire.ToWire(StaffRole.EquipmentManager),
        EnumWire.ToWire(StaffRole.WardNurse)));

    options.AddPolicy(Policies.WorkflowStarter, policy => policy.RequireRole(
        EnumWire.ToWire(StaffRole.DutyManager),
        EnumWire.ToWire(StaffRole.HospitalAdministrator)));

    options.AddPolicy(Policies.PatientRegistrar, policy => policy.RequireRole(
        EnumWire.ToWire(StaffRole.GeneralStaff),
        EnumWire.ToWire(StaffRole.WardNurse),
        EnumWire.ToWire(StaffRole.DutyManager)));

    options.AddPolicy(Policies.PatientDetails, policy => policy.RequireRole(
        EnumWire.ToWire(StaffRole.GeneralStaff),
        EnumWire.ToWire(StaffRole.WardNurse),
        EnumWire.ToWire(StaffRole.DutyManager),
        EnumWire.ToWire(StaffRole.HospitalAdministrator),
        EnumWire.ToWire(StaffRole.Doctor),
        EnumWire.ToWire(StaffRole.EquipmentManager)));

    options.AddPolicy(Policies.PatientEditor, policy => policy.RequireRole(
        EnumWire.ToWire(StaffRole.GeneralStaff),
        EnumWire.ToWire(StaffRole.WardNurse),
        EnumWire.ToWire(StaffRole.DutyManager)));

    options.AddPolicy(Policies.MedicalProfileReader, policy => policy.RequireRole(
        EnumWire.ToWire(StaffRole.WardNurse),
        EnumWire.ToWire(StaffRole.Doctor),
        EnumWire.ToWire(StaffRole.DutyManager)));

    options.AddPolicy(Policies.MedicalProfileAuthor, policy => policy.RequireRole(
        EnumWire.ToWire(StaffRole.WardNurse),
        EnumWire.ToWire(StaffRole.Doctor)));

    options.AddPolicy(Policies.AdmissionEditor, policy => policy.RequireRole(
        EnumWire.ToWire(StaffRole.WardNurse),
        EnumWire.ToWire(StaffRole.DutyManager)));

    options.AddPolicy(Policies.BedAssigner, policy => policy.RequireRole(
        EnumWire.ToWire(StaffRole.GeneralStaff),
        EnumWire.ToWire(StaffRole.WardNurse),
        EnumWire.ToWire(StaffRole.DutyManager)));

    options.AddPolicy(Policies.ArrivalConfirmer, policy => policy.RequireRole(
        EnumWire.ToWire(StaffRole.GeneralStaff),
        EnumWire.ToWire(StaffRole.WardNurse),
        EnumWire.ToWire(StaffRole.DutyManager)));

    options.AddPolicy(Policies.DischargeConfirmer, policy => policy.RequireRole(
        EnumWire.ToWire(StaffRole.GeneralStaff),
        EnumWire.ToWire(StaffRole.WardNurse),
        EnumWire.ToWire(StaffRole.DutyManager)));

    options.AddPolicy(Policies.DischargeChecklist, policy => policy.RequireRole(
        EnumWire.ToWire(StaffRole.WardNurse),
        EnumWire.ToWire(StaffRole.Doctor),
        EnumWire.ToWire(StaffRole.DutyManager)));

    options.AddPolicy(Policies.DischargeBoard, policy => policy.RequireRole(
        EnumWire.ToWire(StaffRole.WardNurse),
        EnumWire.ToWire(StaffRole.Doctor),
        EnumWire.ToWire(StaffRole.DutyManager),
        EnumWire.ToWire(StaffRole.GeneralStaff),
        EnumWire.ToWire(StaffRole.HospitalAdministrator)));

    options.AddPolicy(Policies.BillingDesk, policy => policy.RequireRole(
        EnumWire.ToWire(StaffRole.GeneralStaff),
        EnumWire.ToWire(StaffRole.HospitalAdministrator),
        EnumWire.ToWire(StaffRole.DutyManager)));

    options.AddPolicy(Policies.AppointmentBillingDesk, policy => policy.RequireRole(
        EnumWire.ToWire(StaffRole.GeneralStaff),
        EnumWire.ToWire(StaffRole.WardNurse),
        EnumWire.ToWire(StaffRole.HospitalAdministrator),
        EnumWire.ToWire(StaffRole.DutyManager)));

    options.AddPolicy(Policies.AppointmentDesk, policy => policy.RequireRole(
        EnumWire.ToWire(StaffRole.GeneralStaff),
        EnumWire.ToWire(StaffRole.WardNurse),
        EnumWire.ToWire(StaffRole.DutyManager)));

    options.AddPolicy(Policies.AppointmentBoard, policy => policy.RequireRole(
        EnumWire.ToWire(StaffRole.WardNurse),
        EnumWire.ToWire(StaffRole.DutyManager),
        EnumWire.ToWire(StaffRole.GeneralStaff),
        EnumWire.ToWire(StaffRole.HospitalAdministrator)));

    // Narrower than AnyStaff on purpose: a result is clinical information about a named person,
    // so ambulance crew and general staff are off it even though they hold a staff login.
    options.AddPolicy(Policies.LabReportReader, policy => policy.RequireRole(
        EnumWire.ToWire(StaffRole.Doctor),
        EnumWire.ToWire(StaffRole.WardNurse),
        EnumWire.ToWire(StaffRole.DutyManager),
        EnumWire.ToWire(StaffRole.EquipmentManager)));

    options.AddPolicy(Policies.LabReportAuthor, policy => policy.RequireRole(
        EnumWire.ToWire(StaffRole.EquipmentManager)));

    options.AddPolicy(Policies.PatientLocationReader, policy => policy.RequireRole(
        EnumWire.ToWire(StaffRole.Doctor),
        EnumWire.ToWire(StaffRole.WardNurse),
        EnumWire.ToWire(StaffRole.DutyManager),
        EnumWire.ToWire(StaffRole.EquipmentManager)));

    options.AddPolicy(Policies.EquipmentConfirmer, policy => policy.RequireRole(
        EnumWire.ToWire(StaffRole.HospitalAdministrator)));

    options.AddPolicy(Policies.EquipmentConfirmationTracker, policy => policy.RequireRole(
        EnumWire.ToWire(StaffRole.EquipmentManager),
        EnumWire.ToWire(StaffRole.HospitalAdministrator)));

    options.AddPolicy(Policies.PharmacyRemover, policy => policy.RequireRole(
        EnumWire.ToWire(StaffRole.EquipmentManager),
        EnumWire.ToWire(StaffRole.HospitalAdministrator)));

    options.AddPolicy(Policies.MaintenanceDesk, policy => policy.RequireRole(
        EnumWire.ToWire(StaffRole.HospitalAdministrator)));

    options.AddPolicy(Policies.EquipmentItemEditor, policy => policy.RequireRole(
        EnumWire.ToWire(StaffRole.EquipmentManager),
        EnumWire.ToWire(StaffRole.HospitalAdministrator)));
});

var authRequestsPerMinute = builder.Configuration.GetValue("RateLimits:AuthPerMinute", 20);

builder.Services.AddRateLimiter(options =>
{
    options.AddPolicy(RateLimitPolicies.Auth, context => RateLimitPartition.GetFixedWindowLimiter(
        partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        factory: _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = authRequestsPerMinute,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0
        }));

    options.OnRejected = async (context, ct) =>
    {
        await ProblemResponseWriter.WriteAsync(
            context.HttpContext, StatusCodes.Status429TooManyRequests,
            "Too Many Requests", MessageCode.TooManyRequests, ct);
    };
});

const string WebUiCors = "web-ui";
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
    ?? ["http://localhost:5173"];

builder.Services.AddCors(options => options.AddPolicy(WebUiCors, policy => policy
    .WithOrigins(allowedOrigins)
    .AllowAnyHeader()
    .AllowAnyMethod()
    .WithExposedHeaders(ProblemDetailsFactoryExtensions.TraceIdHeader)));

builder.Services.AddHttpContextAccessor();
builder.Services.AddMemoryCache();

builder.Services.AddSingleton<IPasswordService, PasswordService>();
builder.Services.AddSingleton<ITokenService, TokenService>();
builder.Services.AddSingleton<ILoginThrottle, LoginThrottle>();
builder.Services.AddScoped<ICurrentUser, CurrentUser>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IHealthService, HealthService>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<IAmbulanceEligibilityService, AmbulanceEligibilityService>();
builder.Services.AddScoped<IAmbulanceService, AmbulanceService>();
builder.Services.AddScoped<IAmbulanceCrewService, AmbulanceCrewService>();
builder.Services.AddScoped<IEmergencyCallService, EmergencyCallService>();
builder.Services.AddScoped<IDispatchService, DispatchService>();
builder.Services.AddHostedService<UnacknowledgedDispatchAlertWorker>();
builder.Services.AddScoped<IStaffLookupService, StubStaffLookupService>();
builder.Services.AddSingleton<IAmbulanceDistanceService, StubAmbulanceDistanceService>();

builder.Services.AddScoped<IBedService, BedService>();
builder.Services.AddScoped<IEquipmentCategoryService, EquipmentCategoryService>();
builder.Services.AddScoped<IEquipmentItemService, EquipmentItemService>();
builder.Services.AddScoped<IPharmacyCategoryService, PharmacyCategoryService>();
builder.Services.AddScoped<IPharmacyItemService, PharmacyItemService>();
builder.Services.AddScoped<IMaintenanceService, MaintenanceService>();
builder.Services.AddSingleton<IEquipmentConfirmationCode, EquipmentConfirmationCode>();
builder.Services.AddScoped<ILabReportService, LabReportService>();
builder.Services.AddScoped<IPrescriptionService, PrescriptionService>();
builder.Services.AddScoped<IWardService, WardService>();
builder.Services.AddScoped<IPatientService, PatientService>();
builder.Services.AddScoped<IMedicalProfileService, MedicalProfileService>();
builder.Services.AddScoped<IAdmissionService, AdmissionService>();
builder.Services.AddScoped<ICapacityService, CapacityService>();
builder.Services.AddScoped<IAppointmentService, AppointmentService>();
builder.Services.AddScoped<IBedAssignmentService, BedAssignmentService>();
builder.Services.AddScoped<IBedOccupancyService, BedOccupancyService>();
builder.Services.AddScoped<IWorklistService, WorklistService>();
builder.Services.AddScoped<IDischargeService, DischargeService>();
builder.Services.AddScoped<IBillingService, BillingService>();
builder.Services.AddScoped<IBillingRateService, BillingRateService>();
builder.Services.AddScoped<IMeService, MeService>();

builder.Services.AddScoped<IBedRegistryService, BedRegistryService>();

builder.Services.AddSingleton<IWardDirectory, StubWardDirectory>();

builder.Services.AddScoped<IBedOccupancyPort, BedOccupancyAdapter>();

// Both read Patient Management through their own services rather than their tables. Scoped for
// the same reason as the occupancy adapter above: they reach a DbContext through what they
// delegate to, and a singleton would capture one for the life of the app.
builder.Services.AddScoped<IPatientDirectory, PatientDirectoryAdapter>();
builder.Services.AddScoped<IWardPatientService, WardPatientService>();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "CareLanka API",
        Version = "v1"
    });

    options.CustomOperationIds(description => description.ActionDescriptor.AttributeRouteInfo?.Name);

    options.AddSecurityDefinition(JwtBearerDefaults.AuthenticationScheme, new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header
    });

    options.DocumentFilter<ApiPrefixAsServerFilter>();
    options.OperationFilter<AnonymousOperationFilter>();
    options.OperationFilter<EmergencyCallOperationFilter>();
    options.OperationFilter<ConfirmationCodeHeaderOperationFilter>();
    options.SchemaFilter<JsonRequiredSchemaFilter>();
    options.SchemaFilter<EmergencyCallSchemaFilter>();

});

var app = builder.Build();

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
else
{
    app.UseHttpsRedirection();
}

app.UseCors(WebUiCors);
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();

public partial class Program;
