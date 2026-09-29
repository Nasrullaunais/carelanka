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
        builder.ToTable("staff_members", t =>
        {
            t.HasCheckConstraint("ck_staff_members_role", EnumWire.CheckConstraint<StaffRole>("role"));
            t.HasCheckConstraint("ck_staff_members_title", EnumWire.CheckConstraint<PersonTitle>("title"));
        });

        builder.HasKey(s => s.Id);

        builder.Property(s => s.Email).HasMaxLength(256).IsRequired();
        builder.Property(s => s.PasswordHash).HasMaxLength(512).IsRequired();
        builder.Property(s => s.FirstName).HasMaxLength(100).IsRequired();
        builder.Property(s => s.LastName).HasMaxLength(100).IsRequired();
        builder.Property(s => s.PhoneNumber).HasMaxLength(20);
        builder.Property(s => s.Department).HasMaxLength(100);
        builder.Property(s => s.Specialization).HasMaxLength(150);
        builder.Property(s => s.RegistrationNumber).HasMaxLength(50);

        builder.Property(s => s.Role)
            .HasConversion(new SnakeCaseEnumConverter<StaffRole>())
            .HasMaxLength(40)
            .IsRequired();

        builder.Property(s => s.Title)
            .HasConversion(new SnakeCaseEnumConverter<PersonTitle>())
            .HasMaxLength(20);

        builder.Ignore(s => s.FullName);

        builder.HasIndex(s => s.Email)
            .HasDatabaseName("ux_staff_members_email")
            .IsUnique()
            .HasFilter("is_active");

        builder.HasIndex(s => s.RegistrationNumber)
            .HasDatabaseName("ux_staff_members_registration_number")
            .IsUnique()
            .HasFilter("is_active AND registration_number IS NOT NULL");

        builder.HasQueryFilter(s => s.IsActive);
    }
}
