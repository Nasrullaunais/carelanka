using CareLanka.Api.Common.Persistence;
using CareLanka.Api.Data.Entities.Staff;
using CareLanka.Api.Data.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CareLanka.Api.Data.Configurations.Staff;

public class ShiftConfiguration : IEntityTypeConfiguration<Shift>
{
    public void Configure(EntityTypeBuilder<Shift> builder)
    {
        builder.ToTable("shifts", t =>
        {
            t.HasCheckConstraint(
                "ck_shifts_required_role",
                EnumWire.CheckConstraint<StaffRole>("required_role"));
            t.HasCheckConstraint(
                "ck_shifts_headcount",
                "headcount_needed > 0 AND minimum_headcount > 0 AND minimum_headcount <= headcount_needed");
        });

        builder.HasKey(s => s.Id);

        builder.Property(s => s.RequiredRole)
            .HasConversion(new SnakeCaseEnumConverter<StaffRole>())
            .HasMaxLength(40)
            .IsRequired();

        builder.HasOne(s => s.RequiredSkill)
            .WithMany(sk => sk.Shifts)
            .HasForeignKey(s => s.RequiredSkillId)
            .OnDelete(DeleteBehavior.Restrict);

        // Primary lookup index: roster grid queries by ward and date.
        builder.HasIndex(s => new { s.WardId, s.Date })
            .HasDatabaseName("ix_shifts_ward_date");

        builder.HasIndex(s => s.Date)
            .HasDatabaseName("ix_shifts_date");
    }
}
