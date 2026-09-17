using CareLanka.Api.Services.Equipment;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace CareLanka.Api.Common.OpenApi;

// The action binds the header as string? so a missing code reaches the service and answers the
// same 403 as a wrong one. That nullable would otherwise publish the header as optional, and
// both generated clients would let a caller leave it out.
public sealed class ConfirmationCodeHeaderOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        foreach (var parameter in operation.Parameters.Where(parameter =>
                     parameter.In == ParameterLocation.Header
                     && parameter.Name == EquipmentOptions.ConfirmationCodeHeader))
        {
            parameter.Required = true;
        }
    }
}
