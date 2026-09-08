using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace CareLanka.Api.Common.OpenApi;

public sealed class AnonymousOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var anonymous = context.MethodInfo.GetCustomAttributes(inherit: true).OfType<IAllowAnonymous>().Any()
            || context.MethodInfo.DeclaringType?.GetCustomAttributes(inherit: true)
                .OfType<IAllowAnonymous>().Any() == true;

        if (anonymous)
        {
            operation.Security.Clear();
            return;
        }

        var authorized = context.MethodInfo.GetCustomAttributes(inherit: true).OfType<IAuthorizeData>().Any()
            || context.MethodInfo.DeclaringType?.GetCustomAttributes(inherit: true)
                .OfType<IAuthorizeData>().Any() == true;

        if (authorized)
        {
            operation.Security.Add(new OpenApiSecurityRequirement
            {
                [new OpenApiSecurityScheme
                {
                    Reference = new OpenApiReference
                    {
                        Type = ReferenceType.SecurityScheme,
                        Id = JwtBearerDefaults.AuthenticationScheme
                    }
                }] = []
            });
        }
    }
}
