namespace CareLanka.Api.Services.Emergency;

public sealed class EmergencyOptions
{
    public const string SectionName = "Emergency";

    public int MinimumReadyCrew { get; set; } = 2;

    public int LocationMaxAgeMinutes { get; set; } = 5;

    public int AcknowledgementTimeoutSeconds { get; set; } = 30;

    public HospitalEntranceOptions HospitalEntrance { get; set; } = new();
}

public sealed class HospitalEntranceOptions
{
    public string Label { get; set; } = string.Empty;

    public double Latitude { get; set; }

    public double Longitude { get; set; }
}
