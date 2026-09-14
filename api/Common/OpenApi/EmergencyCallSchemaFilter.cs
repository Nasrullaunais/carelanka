using CareLanka.Api.DTOs.Emergency;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace CareLanka.Api.Common.OpenApi;

public sealed class EmergencyCallSchemaFilter : ISchemaFilter
{
    private static readonly string[] IntakeRequired =
    [
        "patient_is_caller",
        "latitude",
        "longitude",
        "location_accuracy_metres",
        "location_captured_at",
        "idempotency_key"
    ];

    public void Apply(OpenApiSchema schema, SchemaFilterContext context)
    {
        if (context.Type == typeof(CreateEmergencyCallRequest))
        {
            foreach (var name in IntakeRequired)
            {
                schema.Required.Add(name);
                schema.Properties[name].Nullable = false;
            }

            schema.Properties["caller_name"].MaxLength = 200;
            schema.Properties["caller_phone"].MaxLength = 20;
            schema.Properties["latitude"].Minimum = -90;
            schema.Properties["latitude"].Maximum = 90;
            schema.Properties["longitude"].Minimum = -180;
            schema.Properties["longitude"].Maximum = 180;
            schema.Properties["location_accuracy_metres"].Minimum = 0;
            schema.Properties["details"].MaxLength = 1000;
        }

        if (context.Type == typeof(UpdateEmergencyCallRequest))
        {
            foreach (var property in schema.Properties.Values)
            {
                property.Nullable = false;
            }

            schema.Properties["caller_name"].MaxLength = 200;
            schema.Properties["caller_phone"].MaxLength = 20;
            schema.Properties["details"].MaxLength = 1000;
            schema.Properties["latitude"].Minimum = -90;
            schema.Properties["latitude"].Maximum = 90;
            schema.Properties["longitude"].Minimum = -180;
            schema.Properties["longitude"].Maximum = 180;
        }

        if (context.Type == typeof(EmergencyCallDetail))
        {
            schema.Properties["cancellation_request_status"].Nullable = true;
            schema.Properties["dispatches"].Nullable = false;
        }

        if (context.Type == typeof(MyEmergencyCallSummary))
        {
            schema.Properties["cancellation_request_status"].Nullable = true;
        }
    }
}
