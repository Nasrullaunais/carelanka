using System.Text.Json;
using System.Text.Json.Serialization;

namespace CareLanka.Api.Services.Patient;

/// <summary>
/// One set of options for everything the agent stores as jsonb, matching the API's own wire format
/// exactly. A workflow row read straight out of PostgreSQL then says the same thing, in the same
/// words, as the response a reviewer sees on screen - which is the point of persisting it.
/// </summary>
public static class CareWorkflowJson
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
