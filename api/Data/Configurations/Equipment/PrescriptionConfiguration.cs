using CareLanka.Api.Common.Persistence;
using CareLanka.Api.Data.Entities.Equipment;
using CareLanka.Api.Data.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CareLanka.Api.Data.Configurations.Equipment;

public class PrescriptionConfiguration : IEntityTypeConfiguration<Prescription>
{
    public const string TokenUniqueIndex = "ux_prescriptions_token";

    public void Configure(EntityTypeBuilder<Prescription> builder)
    {
        builder.ToTable("prescriptions", t =>
        {
            t.HasCheckConstraint("ck_prescriptions_byte_size", "byte_size > 0");
            t.HasCheckConstraint(
                "ck_prescriptions_status", EnumWire.CheckConstraint<PrescriptionStatus>("status"));
        });

        builder.HasKey(p => p.Id);

        builder.Property(p => p.Note).HasMaxLength(500);
        builder.Property(p => p.FileName).HasMaxLength(255).IsRequired();
        builder.Property(p => p.ContentType).HasMaxLength(100).IsRequired();
        builder.Property(p => p.Content).IsRequired();
        builder.Property(p => p.RejectionReason).HasMaxLength(500);

        builder.Property(p => p.Status)
            .HasConversion(new SnakeCaseEnumConverter<PrescriptionStatus>())
            .HasMaxLength(20)
            .IsRequired();

        // No foreign key on PatientId, for the reason LabReportConfiguration gives.

        builder.HasIndex(p => new { p.TokenDate, p.TokenNumber })
            .HasDatabaseName(TokenUniqueIndex)
            .IsUnique()
            .HasFilter("token_number IS NOT NULL");

        builder.HasIndex(p => new { p.PatientId, p.CreatedAt })
            .IsDescending(false, true);

        builder.HasIndex(p => new { p.Status, p.CreatedAt });
    }
}
