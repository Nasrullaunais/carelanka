namespace CareLanka.Api.Data.Enums;

/// <summary>
/// What goes in the JWT <c>role</c> claim: the seven <see cref="StaffRole"/> values plus
/// <c>patient</c>.
/// <para>
/// A separate name rather than adding <c>Patient</c> to <see cref="StaffRole"/>, because
/// <see cref="StaffRole"/> is a column on <c>staff_members</c> and a patient has no row
/// there. One enum for two different things would make an impossible value representable
/// in the database.
/// </para>
/// This one is never stored — it exists only on the wire and in the token.
/// </summary>
public enum PrincipalRole
{
    WardNurse,
    Doctor,
    AmbulanceCrew,
    GeneralStaff,
    DutyManager,
    HospitalAdministrator,
    EquipmentManager,
    Patient
}
