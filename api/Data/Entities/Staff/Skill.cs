namespace CareLanka.Api.Data.Entities.Staff;

public class Skill : SoftDeletableEntity
{
    public string Name { get; set; } = null!;

    public string? Description { get; set; }

    // Navigation
    public ICollection<StaffMemberSkill> StaffMemberSkills { get; set; } = new List<StaffMemberSkill>();
    public ICollection<Shift> Shifts { get; set; } = new List<Shift>();
    public ICollection<WardStaffingRule> WardStaffingRules { get; set; } = new List<WardStaffingRule>();
}
