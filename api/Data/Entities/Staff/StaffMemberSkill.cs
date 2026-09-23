namespace CareLanka.Api.Data.Entities.Staff;

public class StaffMemberSkill : Entity
{
    public Guid StaffMemberId { get; set; }

    public Guid SkillId { get; set; }

    /// <summary>Certification valid-from date. Null means no start restriction.</summary>
    public DateOnly? ValidFrom { get; set; }

    /// <summary>Certification expiry. Null means does not expire.</summary>
    public DateOnly? ExpiresAt { get; set; }

    // Navigation
    public Skill Skill { get; set; } = null!;
}
