using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace CareLanka.Api.Common.OpenApi;

public sealed class JsonRequiredSchemaFilter : ISchemaFilter
{
    public void Apply(OpenApiSchema schema, SchemaFilterContext context)
    {
        foreach (var property in context.Type.GetProperties()
                     .Where(property => property.GetCustomAttribute<JsonRequiredAttribute>() is not null))
        {
            var wireName = property.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name
                ?? JsonNamingPolicy.SnakeCaseLower.ConvertName(property.Name);
            schema.Required.Add(wireName);
        }
    }
}
