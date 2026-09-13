using CareLanka.Api.Data.Enums;

namespace CareLanka.Api.Data.Entities.Patient;

public class Patient : SoftDeletableEntity
{
    public string PatientCode { get; set; } = null!;

    public string FullName { get; set; } = null!;

    public string? Nic { get; set; }

    public string? TempReference { get; set; }

    public Gender Gender { get; set; }

    public DateOnly? DateOfBirth { get; set; }

    public string? Phone { get; set; }

    public string? Address { get; set; }

    public string? EmergencyContactName { get; set; }

    public string? EmergencyContactPhone { get; set; }

    public Guid? UserAccountId { get; set; }

    public ICollection<Admission> Admissions { get; set; } = new List<Admission>();

    public ICollection<Appointment> Appointments { get; set; } = new List<Appointment>();
}
