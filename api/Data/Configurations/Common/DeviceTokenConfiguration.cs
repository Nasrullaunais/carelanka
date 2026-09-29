using CareLanka.Api.Common.Persistence;
using CareLanka.Api.Data.Entities.Common;
using CareLanka.Api.Data.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CareLanka.Api.Data.Configurations.Common;

public class DeviceTokenConfiguration : IEntityTypeConfiguration<DeviceToken>
{
    public void Configure(EntityTypeBuilder<DeviceToken> builder)
    {
        builder.ToTable("device_tokens", t =>
        {
            t.HasCheckConstraint("ck_device_tokens_platform", EnumWire.CheckConstraint<DevicePlatform>("platform"));
            t.HasCheckConstraint("ck_device_tokens_one_owner",
                "num_nonnulls(staff_member_id, patient_account_id) = 1");
        });

        builder.HasKey(d => d.Id);

        builder.Property(d => d.Token).HasMaxLength(512).IsRequired();

        builder.Property(d => d.Platform)
            .HasConversion(new SnakeCaseEnumConverter<DevicePlatform>())
            .HasMaxLength(20)
            .IsRequired();

        builder.HasIndex(d => d.Token)
            .HasDatabaseName("ux_device_tokens_token")
            .IsUnique();

        builder.HasIndex(d => d.StaffMemberId)
            .HasDatabaseName("ix_device_tokens_staff_member")
            .HasFilter("revoked_at IS NULL");

        builder.HasIndex(d => d.PatientAccountId)
            .HasDatabaseName("ix_device_tokens_patient_account")
            .HasFilter("revoked_at IS NULL");

        builder.HasOne(d => d.StaffMember)
            .WithMany()
            .HasForeignKey(d => d.StaffMemberId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(d => d.PatientAccount)
            .WithMany()
            .HasForeignKey(d => d.PatientAccountId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
