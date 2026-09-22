using System.Text.Json;
using System.Text.Json.Serialization;

namespace CareLanka.Api.Services.Emergency;

/// <summary>
/// One set of options for everything the dispatch agent stores as jsonb, matching the API's own
/// wire format so a workflow row read straight out of PostgreSQL says the same thing a reviewer sees.
/// </summary>
public static class DispatchWorkflowJson
{
    public static readonly JsonSerializerOptions Options = Build();

    public static string Write<TValue>(TValue value) => JsonSerializer.Serialize(value, Options);

    public static TValue? Read<TValue>(string? json)
        => string.IsNullOrWhiteSpace(json) ? default : JsonSerializer.Deserialize<TValue>(json, Options);

    private static JsonSerializerOptions Build()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            DictionaryKeyPolicy = JsonNamingPolicy.SnakeCaseLower,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower));

        return options;
    }
}
