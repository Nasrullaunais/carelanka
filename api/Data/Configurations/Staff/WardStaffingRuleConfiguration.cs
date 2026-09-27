using CareLanka.Api.Common.Persistence;
using CareLanka.Api.Data.Entities.Staff;
using CareLanka.Api.Data.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CareLanka.Api.Data.Configurations.Staff;

public class WardStaffingRuleConfiguration : IEntityTypeConfiguration<WardStaffingRule>
{
    public void Configure(EntityTypeBuilder<WardStaffingRule> builder)
    {
        builder.ToTable("ward_staffing_rules", t =>
        {
            t.HasCheckConstraint(
                "ck_ward_staffing_rules_required_role",
                EnumWire.CheckConstraint<StaffRole>("required_role"));
            t.HasCheckConstraint(
                "ck_ward_staffing_rules_min",
                "minimum_headcount > 0");
        });

        builder.HasKey(r => r.Id);

        builder.Property(r => r.RequiredRole)
            .HasConversion(new SnakeCaseEnumConverter<StaffRole>())
            .HasMaxLength(40)
            .IsRequired();

        builder.HasOne(r => r.RequiredSkill)
            .WithMany(s => s.WardStaffingRules)
            .HasForeignKey(r => r.RequiredSkillId)
            .OnDelete(DeleteBehavior.Restrict);

        // Unique: one rule per ward+role+skill combination. A plain unique index would not
        // catch duplicate role-only rules, because Postgres treats every NULL as distinct -
        // so the null and non-null cases each need their own filtered unique index.
        builder.HasIndex(r => new { r.WardId, r.RequiredRole, r.RequiredSkillId })
            .HasDatabaseName("ux_ward_staffing_rules_ward_role_skill")
            .IsUnique()
            .HasFilter("required_skill_id IS NOT NULL");

        builder.HasIndex(r => new { r.WardId, r.RequiredRole })
            .HasDatabaseName("ux_ward_staffing_rules_ward_role_no_skill")
            .IsUnique()
            .HasFilter("required_skill_id IS NULL");

        builder.HasIndex(r => r.WardId)
            .HasDatabaseName("ix_ward_staffing_rules_ward_id");
    }
}
