using CareLanka.Api.Common.Persistence;
using CareLanka.Api.Data.Entities.Common;
using CareLanka.Api.Data.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CareLanka.Api.Data.Configurations.Common;

public class StaffMemberConfiguration : IEntityTypeConfiguration<StaffMember>
{
    public void Configure(EntityTypeBuilder<StaffMember> builder)
    {
        builder.ToTable("staff_members", t => t.HasCheckConstraint(
            "ck_staff_members_role", EnumWire.CheckConstraint<StaffRole>("role")));

        builder.HasKey(s => s.Id);

        builder.Property(s => s.Email).HasMaxLength(256).IsRequired();
        builder.Property(s => s.PasswordHash).HasMaxLength(512).IsRequired();
        builder.Property(s => s.FirstName).HasMaxLength(100).IsRequired();
        builder.Property(s => s.LastName).HasMaxLength(100).IsRequired();
        builder.Property(s => s.PhoneNumber).HasMaxLength(20);
        builder.Property(s => s.Department).HasMaxLength(100);

        builder.Property(s => s.Role)
            .HasConversion(new SnakeCaseEnumConverter<StaffRole>())
            .HasMaxLength(40)
            .IsRequired();

        // FullName is computed in C#; there is no column behind it.
        builder.Ignore(s => s.FullName);

        // Scoped WHERE is_active. A plain UNIQUE here means a deactivated staff member's
        // email can never be reused, and the global query filter hides the row that is
        // blocking it — so the duplicate check passes and SaveChanges throws instead.
        builder.HasIndex(s => s.Email)
            .HasDatabaseName("ux_staff_members_email")
            .IsUnique()
            .HasFilter("is_active");

        builder.HasQueryFilter(s => s.IsActive);
    }
}
