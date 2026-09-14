using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace CareLanka.Api.Common.OpenApi;

public sealed class EmergencyCallOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        if (operation.OperationId is "createEmergencyCall" or "updateEmergencyCall")
        {
            operation.RequestBody.Required = true;
        }

        if (operation.OperationId == "listEmergencyCalls")
        {
            ConfigurePage(Parameter(operation, "page"), 1);
            ConfigurePageSize(Parameter(operation, "pageSize"));
            ConfigureSortDirection(Parameter(operation, "sortDir"));
            Parameter(operation, "sortBy").Schema.Default = new OpenApiString("priority");
            Parameter(operation, "unassignedOnly").Schema.Default = new OpenApiBoolean(false);
        }

        if (operation.OperationId == "getMyEmergencyCalls")
        {
            ConfigurePage(Parameter(operation, "page"), 1);
            ConfigurePageSize(Parameter(operation, "pageSize"));
        }
    }

    private static OpenApiParameter Parameter(OpenApiOperation operation, string name)
        => operation.Parameters.Single(parameter => parameter.Name == name);

    private static void ConfigurePage(OpenApiParameter parameter, int value)
    {
        parameter.Schema.Minimum = 1;
        parameter.Schema.Default = new OpenApiInteger(value);
    }

    private static void ConfigurePageSize(OpenApiParameter parameter)
    {
        parameter.Schema.Minimum = 1;
        parameter.Schema.Maximum = 100;
        parameter.Schema.Default = new OpenApiInteger(20);
    }

    private static void ConfigureSortDirection(OpenApiParameter parameter)
    {
        parameter.Schema.Enum = [new OpenApiString("asc"), new OpenApiString("desc")];
        parameter.Schema.Default = new OpenApiString("desc");
    }
}
