using System.Text.Json;
using System.Text.Json.Serialization;

namespace CareLanka.Api.Agents.Staff;

/// <summary>
/// JSON serialization settings for Staff Roster Proposal workflows and proposed changes,
/// matching the API's snake_case wire format.
/// </summary>
public static class RosterWorkflowJson
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
