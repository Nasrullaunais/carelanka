using System.Text.Json.Serialization;

namespace CareLanka.Api.DTOs.Patient;

public class AssignBedRequest
{
    [JsonRequired]
    public Guid BedId { get; set; }

    /// <summary>
    /// Present when the nurse pressed "Use this bed" on an agent suggestion, absent when they
    /// picked the bed themselves. It is the only thing that differs between the two paths. A
    /// workflow that does not exist, or that suggested a different bed, is recorded as a manual
    /// assignment rather than trusted.
    /// </summary>
    public Guid? WorkflowId { get; set; }

    public string? OverrideReason { get; set; }
}
