using CareLanka.Api.Data.Enums;

namespace CareLanka.Api.Data.Entities.Equipment;

// The physical frame, and the register Patient Management reads to build its bed agent's
// candidate list. Whether anyone is in the bed is not a column here: it is the presence of
// one of their BedAssignment rows. See integration_of_functions.md 6.1.
//
// Retirement is a soft delete. There is no un-retire, so a retired bed simply stops being
// visible through the global query filter.
public class Bed : SoftDeletableEntity
{
    // Patient Management's Ward, stored as a bare reference with no navigation property.
    // Their table is not on main yet and Equipment never writes it. See STUBS.md row 2.
    public Guid WardId { get; set; }

    public string BedNumber { get; set; } = null!;

    public bool HasIsolation { get; set; }

    // 1 is closest to the nurse station. The bed agent ranks on this.
    public int NurseStationDistance { get; set; } = 1;

    public BedCondition Condition { get; set; }

    public string? AssetTag { get; set; }
}
