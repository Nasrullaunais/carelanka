using System.ComponentModel.DataAnnotations;
using CareLanka.Api.Data.Enums;

namespace CareLanka.Api.DTOs.Patient;

/// <summary>
/// Who the suggestion is for. Part of the answer rather than a second lookup - a bed suggestion
/// with no name on it is how the right bed gets given to the wrong person.
/// </summary>
public class BedSuggestionPatient
{
    [Required]
    public Guid PatientId { get; set; }

    [Required]
    public string PatientCode { get; set; } = null!;

    [Required]
    public string FullName { get; set; } = null!;

    public int? Age { get; set; }

    [Required]
    public Gender Gender { get; set; }

    public Guid? AdmissionId { get; set; }

    public AdmissionCategory? AdmissionCategory { get; set; }

    public AdmissionUrgency? Urgency { get; set; }

    /// <summary>
    /// Nullable for the same reason the three fields above it are: it is read off the admission,
    /// and a patient the agent resolved with no open visit has none. Sending <c>false</c> there
    /// would be a claim about infection control made from the absence of a record.
    /// </summary>
    public bool? IsInfectious { get; set; }

    public AdmissionStatus? Status { get; set; }
}
