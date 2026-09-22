namespace CareLanka.Api.Services.Common;

public sealed class PushOptions
{
    public const string SectionName = "Push";

    public string? CredentialsPath { get; set; }

    public int PollSeconds { get; set; } = 5;

    public int MaxAttempts { get; set; } = 5;

    public int RetryBaseSeconds { get; set; } = 10;

    public int BatchSize { get; set; } = 50;
}
