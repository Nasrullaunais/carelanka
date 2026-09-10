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
using CareLanka.Api.Services.Patient;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Formatters;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

static void ConfigureJson(JsonSerializerOptions json)
{
    json.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower;
    json.DictionaryKeyPolicy = JsonNamingPolicy.SnakeCaseLower;
    json.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower));
}

builder.Services
    .AddControllers()
    .AddJsonOptions(options => ConfigureJson(options.JsonSerializerOptions));

// For responses written outside MVC — the exception handler and the JWT middleware both do.
builder.Services.ConfigureHttpJsonOptions(options => ConfigureJson(options.SerializerOptions));

builder.Services.Configure<MvcOptions>(options =>
{
    // Left alone, MVC also offers text/plain and text/json on every 200, and a generated
    // client is free to pick the first one it sees.
    options.OutputFormatters.RemoveType<StringOutputFormatter>();

    foreach (var formatter in options.OutputFormatters.OfType<SystemTextJsonOutputFormatter>())
    {
        formatter.SupportedMediaTypes.Remove("text/json");
    }
});

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();

// MVC builds validation failures before any of our code runs, so the shared extensions have
// to be bolted on here rather than in the exception handler.
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

var jwt = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();

// Keep sub as sub. Without this the handler rewrites inbound claim names to long
// WS-Federation URIs and every lookup of "sub" quietly returns nothing.
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

            // The default is five minutes, which quietly makes a 15-minute access token a 20-minute one.
            ClockSkew = TimeSpan.Zero,

            NameClaimType = CareLankaClaims.Subject,
            RoleClaimType = CareLankaClaims.Role
        };

        // Without these, a missing token returns an empty 401 body and a wrong role an empty 403 —
        // neither of which matches the specs, and neither of which a generated client can classify.
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

    options.AddPolicy(Policies.WorkflowReader, policy => policy.RequireRole(
        EnumWire.ToWire(StaffRole.DutyManager),
        EnumWire.ToWire(StaffRole.HospitalAdministrator),
        EnumWire.ToWire(StaffRole.EquipmentManager),
        EnumWire.ToWire(StaffRole.WardNurse)));

    options.AddPolicy(Policies.WorkflowStarter, policy => policy.RequireRole(
        EnumWire.ToWire(StaffRole.DutyManager),
        EnumWire.ToWire(StaffRole.HospitalAdministrator)));

    options.AddPolicy(Policies.PatientRegistrar, policy => policy.RequireRole(
        EnumWire.ToWire(StaffRole.WardNurse),
        EnumWire.ToWire(StaffRole.AmbulanceCrew),
        EnumWire.ToWire(StaffRole.DutyManager)));

    options.AddPolicy(Policies.PatientReader, policy => policy.RequireRole(
        EnumWire.ToWire(StaffRole.WardNurse),
        EnumWire.ToWire(StaffRole.DutyManager),
        EnumWire.ToWire(StaffRole.HospitalAdministrator)));

    options.AddPolicy(Policies.PatientEditor, policy => policy.RequireRole(
        EnumWire.ToWire(StaffRole.WardNurse),
        EnumWire.ToWire(StaffRole.DutyManager)));
});

// 20 a minute per IP in production. Configurable only so the integration tests can raise it:
// every test class shares one IP, so the whole suite spends one budget, and at 20 the next
// test anybody adds fails on a 429 that reads like a broken login. Nothing sets this outside
// the test fixture, and the default is what ships.
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

builder.Services.AddScoped<IBedService, BedService>();
builder.Services.AddScoped<IEquipmentCategoryService, EquipmentCategoryService>();
builder.Services.AddScoped<IEquipmentItemService, EquipmentItemService>();
builder.Services.AddScoped<IWardService, WardService>();
builder.Services.AddScoped<IPatientService, PatientService>();

// Real: ward bed counts now come from Equipment's register instead of a constant.
// Scoped, not Singleton — it delegates to IBedService, which is scoped because it holds a
// DbContext. Registering it as a singleton captures one DbContext for the life of the app.
builder.Services.AddScoped<IBedRegistryService, BedRegistryService>();

// STUB registrations - Equipment still stands in for Patient Management. STUBS.md rows 2
// and 3. Row 2 can be swapped whenever M3 wants: IWardService is real. Row 3 cannot yet —
// occupancy is the presence of a live BedAssignment, and no service writes those until
// step 6 of build/patient.md.
builder.Services.AddSingleton<IWardDirectory, StubWardDirectory>();
builder.Services.AddSingleton<IBedOccupancyPort, StubBedOccupancyPort>();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "CareLanka API",
        Version = "v1",
        Description = "One ASP.NET Core application behind all four components. "
                      + "Routes, operationIds and schema names are global, not per-component."
    });

    // operationId becomes the generated client's function name, so it comes from the route name
    // rather than from a C# method name someone may rename.
    options.CustomOperationIds(description => description.ActionDescriptor.AttributeRouteInfo?.Name);

    options.AddSecurityDefinition(JwtBearerDefaults.AuthenticationScheme, new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Paste the access_token from POST /api/auth/login. No \"Bearer \" prefix."
    });

    options.DocumentFilter<ApiPrefixAsServerFilter>();
    options.OperationFilter<AnonymousOperationFilter>();

    var xmlPath = Path.Combine(AppContext.BaseDirectory, "CareLanka.Api.xml");

    if (File.Exists(xmlPath))
    {
        options.IncludeXmlComments(xmlPath, includeControllerXmlComments: true);
    }
});

var app = builder.Build();

// Order matters: the exception handler has to be outermost to catch anything thrown further in,
// and authentication has to run before authorization can read a role.
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

// Exposed so an integration test project can spin the real application up.
public partial class Program;
