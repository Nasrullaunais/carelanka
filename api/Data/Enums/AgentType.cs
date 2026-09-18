namespace CareLanka.Api.Data.Enums;

/// <summary>
/// Group-owned vocabulary, published by <c>specs/common-spec.yaml</c>. One value per agent:
/// a coordinator that only plans and delegates, and one domain agent per member.
/// </summary>
public enum AgentType
{
    Coordinator,
    DispatchRouting,
    PatientAdmissionBed,
    StaffAllocation,
    EquipmentMonitoring
}
