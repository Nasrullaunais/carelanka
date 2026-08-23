namespace CareLanka.Api.Data.Entities.Common;

/// <summary>
/// Every operator role in the system. Serialized as snake_case (ward_nurse, duty_manager)
/// in both the JWT role claim and the wire format. See entity_diagram.md StaffRole.
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

public enum AuditOperation
{
    Create,
    Update,
    Delete
}

public enum DevicePlatform
{
    Android,
    Ios,
    Web
}
