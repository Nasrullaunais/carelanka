using CareLanka.Api.Data.Enums;

namespace CareLanka.Api.Data.Entities.Staff;

public class Shift : AuditedEntity
{
    public Guid WardId { get; set; }

    public DateOnly Date { get; set; }

    /// <summary>
    /// When EndTime is earlier than StartTime the shift crosses midnight and ends on Date + 1 day.
    /// Every overlap and coverage query must apply this roll-over rule.
    /// </summary>
    public TimeOnly StartTime { get; set; }

    public TimeOnly EndTime { get; set; }

    public StaffRole RequiredRole { get; set; }

    /// <summary>Optional skill required for this slot.</summary>
    public Guid? RequiredSkillId { get; set; }

    /// <summary>Target headcount. May be higher than MinimumHeadcount.</summary>
    public int HeadcountNeeded { get; set; }

    /// <summary>Legal floor — the validator refuses to breach this.</summary>
    public int MinimumHeadcount { get; set; }

    // Navigation
    public Skill? RequiredSkill { get; set; }
    public ICollection<Allocation> Allocations { get; set; } = new List<Allocation>();
}
