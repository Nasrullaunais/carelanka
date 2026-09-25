using System.Text.Json.Serialization;

namespace CareLanka.Api.DTOs.Patient;

public sealed class CreateWalkInAppointmentRequest
{
    [JsonRequired]
    public Guid PatientId { get; set; }

    public string? Reason { get; set; }
}
