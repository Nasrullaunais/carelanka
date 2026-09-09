namespace CareLanka.Api.Data.Enums;

// One bed row walks reserved -> occupied -> released. "Reserved" is the 30-minute hold;
// the partial unique indexes on bed_assignments are what make it exclusive, not code.
public enum AssignmentStatus
{
    Reserved,
    Occupied,
    Released
}
