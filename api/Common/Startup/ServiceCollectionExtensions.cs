using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using CareLanka.Api.Common.Errors;
using CareLanka.Api.Data;
using CareLanka.Api.Data.Interceptors;
using CareLanka.Api.Services.Common;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

namespace CareLanka.Api.Common.Startup;

/// <summary>
/// All the registration, kept out of Program.cs so four people are not editing the same
/// twenty lines. Add your component's services in AddComponentServices at the bottom.
/// </summary>
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddCareLankaPersistence(
        this IServiceCollection services, IConfiguration config)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUser, CurrentUser>();
        services.AddScoped<AuditSaveChangesInterceptor>();

        services.AddDbContext<CareLankaDbContext>((sp, options) =>
        {
            options
                .UseNpgsql(config.GetConnectionString("CareLanka"))
                // Tables and columns become snake_case automatically, so the C# stays
                // ordinary C# and the database stays ordinary Postgres.
                .UseSnakeCaseNamingConvention()
                .AddInterceptors(sp.GetRequiredService<AuditSaveChangesInterceptor>());
        });

        return services;
    }

    public static IServiceCollection AddCareLankaAuth(
        this IServiceCollection services, IConfiguration config)
    {
        services.AddOptions<JwtOptions>()
            .Bind(config.GetSection(JwtOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();   // a missing signing key kills startup, not the first login

        var jwt = config.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(o =>
            {
                o.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = jwt.Issuer,
                    ValidAudience = jwt.Audience,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key)),
                    ClockSkew = TimeSpan.FromSeconds(30)
                };
            });

        services.AddAuthorization();
        services.AddScoped<ITokenService, TokenService>();
        services.AddScoped<IAuthService, AuthService>();

        return services;
    }

    public static IServiceCollection AddCareLankaWebApi(this IServiceCollection services)
    {
        services.AddControllers()
            .AddJsonOptions(o =>
            {
                // Bodies go over the wire as snake_case (full_name). Query parameters stay
                // camelCase — that is separate, and comes from the parameter names.
                o.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower;
                o.JsonSerializerOptions.DictionaryKeyPolicy = JsonNamingPolicy.SnakeCaseLower;
                o.JsonSerializerOptions.DefaultIgnoreCondition =
                    JsonIgnoreCondition.WhenWritingNull;
                // Enums as snake_case strings, matching the specs (ward_nurse, not 0).
                o.JsonSerializerOptions.Converters.Add(
                    new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower));
            });

        // Model-binding failures become ValidationProblemDetails, same shape as everything
        // else, instead of ASP.NET's default envelope.
        services.AddProblemDetails();
        services.AddExceptionHandler<GlobalExceptionHandler>();

        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(o =>
        {
            o.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "CareLanka API",
                Version = "v1",
                Description = "One API for Emergency, Staff, Equipment and Patient Management."
            });

            o.AddSecurityDefinition("bearerAuth", new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                In = ParameterLocation.Header,
                Description = "Paste the access_token from POST /auth/login. No \"Bearer \" prefix."
            });

            o.AddSecurityRequirement(new OpenApiSecurityRequirement
            {
                [new OpenApiSecurityScheme
                {
                    Reference = new OpenApiReference
                    {
                        Type = ReferenceType.SecurityScheme,
                        Id = "bearerAuth"
                    }
                }] = Array.Empty<string>()
            });

            foreach (var xml in Directory.GetFiles(AppContext.BaseDirectory, "*.xml"))
                o.IncludeXmlComments(xml, includeControllerXmlComments: true);
        });

        return services;
    }

    /// <summary>
    /// Per-component registration. Add one line per component here and keep the actual
    /// registrations in your own file, so nobody edits anybody else's.
    /// </summary>
    public static IServiceCollection AddComponentServices(this IServiceCollection services)
    {
        // services.AddEmergencyServices();   // Member 1
        // services.AddStaffServices();       // Member 2
        // services.AddEquipmentServices();   // Member 3
        // services.AddPatientServices();     // Member 4
        return services;
    }
}
