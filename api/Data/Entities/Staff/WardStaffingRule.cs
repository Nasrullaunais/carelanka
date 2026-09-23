using CareLanka.Api.Data.Enums;

namespace CareLanka.Api.Data.Entities.Staff;

public class WardStaffingRule : AuditedEntity
{
    public Guid WardId { get; set; }

    public StaffRole RequiredRole { get; set; }

    /// <summary>Skill additionally required for this rule. Null means role alone is sufficient.</summary>
    public Guid? RequiredSkillId { get; set; }

    public int MinimumHeadcount { get; set; }

    // Navigation
    public Skill? RequiredSkill { get; set; }
}
