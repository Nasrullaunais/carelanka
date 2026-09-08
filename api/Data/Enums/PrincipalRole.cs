namespace CareLanka.Api.Data.Enums;

// Never stored, only put in the token. Separate from StaffRole because a patient has no
// staff_members row, and one enum for both would make an impossible column value representable.
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
