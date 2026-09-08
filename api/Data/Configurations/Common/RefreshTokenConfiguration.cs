using CareLanka.Api.Common.Persistence;
using CareLanka.Api.Data.Entities.Common;
using CareLanka.Api.Data.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CareLanka.Api.Data.Configurations.Common;

public class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("refresh_tokens", t =>
        {
            t.HasCheckConstraint(
                "ck_refresh_tokens_principal_type",
                EnumWire.CheckConstraint<PrincipalType>("principal_type"));

            // Exactly one owner, matching principal_type. Without it a row could name a staff
            // member and claim to be a patient, and /auth/me would answer about the wrong person.
            t.HasCheckConstraint(
                "ck_refresh_tokens_one_principal",
                "(principal_type = 'staff' AND staff_member_id IS NOT NULL AND patient_account_id IS NULL)"
                + " OR (principal_type = 'patient' AND patient_account_id IS NOT NULL AND staff_member_id IS NULL)");
        });

        builder.HasKey(r => r.Id);

        builder.Property(r => r.PrincipalType)
            .HasConversion(new SnakeCaseEnumConverter<PrincipalType>())
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(r => r.TokenHash).HasMaxLength(128).IsRequired();
        builder.Property(r => r.RevokedReason).HasMaxLength(100);

        builder.Ignore(r => r.PrincipalId);

        builder.HasIndex(r => r.TokenHash)
            .HasDatabaseName("ux_refresh_tokens_hash")
            .IsUnique();

        builder.HasIndex(r => new { r.PrincipalType, r.StaffMemberId, r.PatientAccountId })
            .HasDatabaseName("ix_refresh_tokens_principal");

        builder.HasOne(r => r.StaffMember)
            .WithMany(s => s.RefreshTokens)
            .HasForeignKey(r => r.StaffMemberId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(r => r.PatientAccount)
            .WithMany(p => p.RefreshTokens)
            .HasForeignKey(r => r.PatientAccountId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
