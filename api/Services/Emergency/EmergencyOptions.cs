namespace CareLanka.Api.Services.Emergency;

public sealed class EmergencyOptions
{
    public const string SectionName = "Emergency";

    public int MinimumReadyCrew { get; set; } = 2;

    public int LocationMaxAgeMinutes { get; set; } = 5;
}
