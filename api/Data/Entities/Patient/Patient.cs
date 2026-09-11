using CareLanka.Api.Data.Enums;

namespace CareLanka.Api.Data.Entities.Patient;

// One row per human being, reused across visits. Never one row per admission.
public class Patient : SoftDeletableEntity
{
    public string FullName { get; set; } = null!;

    // Nullable because an unconscious arrival has no papers, but unique whenever present so
    // a returning patient is one row and not three.
    public string? Nic { get; set; }

    // Server-generated at intake when there is no NIC and no phone: UNKNOWN-2026-0142.
    // Never cleared once a NIC turns up later — the wristband and the verbal handover from
    // the unidentified period still have to resolve to this person.
    public string? TempReference { get; set; }

    public Gender Gender { get; set; }

    public DateOnly? DateOfBirth { get; set; }

    public string? Phone { get; set; }

    public string? Address { get; set; }

    public string? EmergencyContactName { get; set; }

    public string? EmergencyContactPhone { get; set; }

    // The optional link to a patient login. Most records never have one — a walk-in is a
    // medical record, not a user. Id only, no navigation: "has_account" is
    // UserAccountId is not null, and the account itself is common auth's to serve.
    public Guid? UserAccountId { get; set; }

    public ICollection<Admission> Admissions { get; set; } = new List<Admission>();

    public ICollection<Appointment> Appointments { get; set; } = new List<Appointment>();
}
