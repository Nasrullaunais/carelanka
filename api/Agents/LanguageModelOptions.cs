namespace CareLanka.Api.Agents;

public sealed class LanguageModelOptions
{
    public const string SectionName = "LanguageModel";

    /// <summary>
    /// Read from configuration, never from source. Empty in the repository and in every committed
    /// appsettings file; supplied as an environment variable. With no key the API still starts and
    /// every agent still answers - see <see cref="NoLanguageModel"/>.
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;

    public string Model { get; set; } = "gemini-2.0-flash";

    public string BaseUrl { get; set; } = "https://generativelanguage.googleapis.com/v1beta";

    /// <summary>Hard timeout per attempt. §9.1 asks for one by name.</summary>
    public int TimeoutSeconds { get; set; } = 20;

    /// <summary>Retries after the first attempt, so 2 means at most three calls.</summary>
    public int MaxRetries { get; set; } = 2;
}
