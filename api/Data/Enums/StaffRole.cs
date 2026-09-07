namespace CareLanka.Api.Data.Enums;

/// <summary>
/// The seven internal roles. Group-owned: this is the JWT role list, so it is
/// byte-identical here, in <c>specs/common-spec.yaml</c> and in <c>specs/staff-spec.yaml</c>.
/// <para>
/// <c>patient</c> is deliberately absent — a patient is not staff and has no
/// <c>staff_members</c> row. See <see cref="PrincipalRole"/>.
/// </para>
/// </summary>
public enum StaffRole
{
    WardNurse,
    Doctor,
    AmbulanceCrew,
    GeneralStaff,
    DutyManager,
    HospitalAdministrator,
    EquipmentManager
}
