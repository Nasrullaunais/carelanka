namespace CareLanka.Api.DTOs.Emergency;

public sealed class AmbulanceHandover
{
    public string AmbulanceRegistration { get; set; } = string.Empty;
    public DateTimeOffset HandedOverAt { get; set; }
    public string? PatientCondition { get; set; }
    public string? Notes { get; set; }
}
