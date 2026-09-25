using CareLanka.Api.Data.Enums;

namespace CareLanka.Api.DTOs.Patient;

public class PreAdmitRequest
{
    public string DispatchId { get; set; } = string.Empty;

    public bool PatientIsCaller { get; set; }

    public Guid? CallerUserId { get; set; }

    public Guid? PatientId { get; set; }

    public DateTimeOffset ExpectedArrival { get; set; }

    public AdmissionUrgency Urgency { get; set; }

    public WardType? DestinationWardTypeHint { get; set; }

    public string? ProvisionalName { get; set; }

    public Gender? ProvisionalGender { get; set; }
}
