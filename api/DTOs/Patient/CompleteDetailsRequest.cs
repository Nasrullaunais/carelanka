using System.ComponentModel.DataAnnotations;

namespace CareLanka.Api.DTOs.Patient;

/// <summary>
/// Body of PATCH /api/admissions/{id}/details. Any subset of the fields that were missing —
/// a key left out is left alone, which is what makes this different from the PUT on a patient.
/// </summary>
/// <remarks>
/// Emergency arrivals are registered with the bare minimum so care is not delayed, and the
/// rest is collected later, often from a relative. This is also the endpoint that finishes a
/// bystander-reported patient: same endpoint, same screen, not a separate copy of it.
/// </remarks>
public class CompleteDetailsRequest
{
    [MaxLength(20)]
    public string? Nic { get; set; }

    [MaxLength(200)]
    public string? FullName { get; set; }

    public DateOnly? DateOfBirth { get; set; }

    [MaxLength(20)]
    public string? Phone { get; set; }

    [MaxLength(300)]
    public string? Address { get; set; }

    [MaxLength(200)]
    public string? EmergencyContactName { get; set; }

    [MaxLength(20)]
    public string? EmergencyContactPhone { get; set; }
}
