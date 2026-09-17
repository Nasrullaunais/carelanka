using CareLanka.Api.Data.Entities.Staff;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CareLanka.Api.Data.Configurations.Staff;

public class StaffMemberSkillConfiguration : IEntityTypeConfiguration<StaffMemberSkill>
{
    public void Configure(EntityTypeBuilder<StaffMemberSkill> builder)
    {
        builder.ToTable("staff_member_skills");

        builder.HasKey(s => s.Id);

        builder.HasOne<Entities.Common.StaffMember>()
            .WithMany()
            .HasForeignKey(s => s.StaffMemberId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(s => s.Skill)
            .WithMany(sk => sk.StaffMemberSkills)
            .HasForeignKey(s => s.SkillId)
            .OnDelete(DeleteBehavior.Restrict);

        // One skill record per person — re-granting a skill updates the existing row.
        builder.HasIndex(s => new { s.StaffMemberId, s.SkillId })
            .HasDatabaseName("ux_staff_member_skills_staff_skill")
            .IsUnique();
    }
}
