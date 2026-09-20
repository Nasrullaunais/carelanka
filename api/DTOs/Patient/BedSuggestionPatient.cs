using System.Text.Json.Serialization;
using CareLanka.Api.Data.Enums;

namespace CareLanka.Api.DTOs.Patient;

/// <summary>
/// Who the suggestion is for. Part of the answer rather than a second lookup - a bed suggestion
/// with no name on it is how the right bed gets given to the wrong person.
/// </summary>
public sealed class BedSuggestionPatient
{
    [JsonRequired]
    public Guid PatientId { get; set; }

    [JsonRequired]
    public string PatientCode { get; set; } = string.Empty;

    [JsonRequired]
    public string FullName { get; set; } = string.Empty;

    public int? Age { get; set; }

    public Gender Gender { get; set; }

    public Guid? AdmissionId { get; set; }

    public AdmissionCategory? AdmissionCategory { get; set; }

    public AdmissionUrgency? Urgency { get; set; }

    public bool IsInfectious { get; set; }

    public AdmissionStatus? Status { get; set; }
}
