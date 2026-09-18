namespace CareLanka.Api.Data.Enums;

/// <summary>
/// The objective allow-list. An objective is chosen from this enum and never accepted as free
/// text - a planner reached by an arbitrary string is a prompt-injection surface, and assignment
/// §9.1 requires validated tool inputs. Group-owned, published by <c>specs/common-spec.yaml</c>.
/// </summary>
public enum WorkflowObjective
{
    EmergencyResponse,
    AssignBed,
    FillRosterGap,
    MonitorStockAndMaintenance,
    CheckWardReadiness
}
