using System.ComponentModel.DataAnnotations;
using CareLanka.Api.Data.Enums;

namespace CareLanka.Api.DTOs.Patient;

/// <summary>What a patient may see about their own stay.</summary>
/// <remarks>
/// A separate shape, not a filtered staff object — it cannot leak staff notes, agent
/// reasoning, a rejection history or another patient, because it does not contain them.
/// </remarks>
public class MyAdmission
{
    [Required]
    public Guid AdmissionId { get; set; }

    [Required]
    public AdmissionStatus Status { get; set; }

    /// <summary>
    /// The same status in plain language, e.g. "A bed is being arranged for you". The app shows
    /// this sentence; <c>status</c> is what it branches on.
    /// </summary>
    /// <remarks>
    /// Server-side rather than a lookup table in the app, so the wording is written once for
    /// both frontends and can be translated into Sinhala and Tamil in one place.
    ///
    /// <b>No dates or times in this sentence.</b> Timestamps are stored in UTC and this API has
    /// no notion of a local timezone, so a formatted time here would read five and a half hours
    /// early on a phone in Colombo. The app formats the timestamp fields itself, in the
    /// timezone the device is actually in — which is a thing the phone knows and the server
    /// does not.
    /// </remarks>
    [Required]
    public string StatusText { get; set; } = null!;

    /// <summary>Null until a bed is held or taken. For a finished visit, the last ward they were in.</summary>
    public string? WardName { get; set; }

    /// <summary>Null until a bed is held or taken. For a finished visit, the last bed they were in.</summary>
    public string? BedNumber { get; set; }

    public DateTimeOffset? AdmittedAt { get; set; }

    public DateTimeOffset? ExpectedArrival { get; set; }

    public DateTimeOffset? DischargedAt { get; set; }

    /// <summary>
    /// What the ward wrote for the patient to read on the way out. <c>Discharge.SummaryNote</c>,
    /// which the entity plan describes as exactly this: "instructions the patient can read".
    /// </summary>
    public string? DischargeInstructions { get; set; }

    [Required]
    public bool DetailsComplete { get; set; }

    /// <summary>What is still outstanding, so the patient can be asked to supply it rather than chased for it.</summary>
    [Required]
    public IReadOnlyList<string> MissingFields { get; set; } = Array.Empty<string>();
}
