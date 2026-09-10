namespace CareLanka.Api.Data.Enums;

// available -> assigned -> available        (returned after use)
// available -> maintenance -> available     (repaired, back in service)
// available -> maintenance -> retired       (beyond repair)
// available -> retired                      (planned decommission)
//
// Retired is terminal. A replacement is a new row, never a reactivated one.
public enum EquipmentStatus
{
    Available,
    Assigned,
    Maintenance,
    Retired
}
