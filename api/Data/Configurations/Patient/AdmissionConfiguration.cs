using CareLanka.Api.Common.Persistence;
using CareLanka.Api.Data.Entities.Patient;
using CareLanka.Api.Data.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CareLanka.Api.Data.Configurations.Patient;

public class AdmissionConfiguration : IEntityTypeConfiguration<Admission>
{
    public const string OpenAdmissionUniqueIndex = "ux_admissions_open_patient";

    public void Configure(EntityTypeBuilder<Admission> builder)
    {
        builder.ToTable("admissions", t =>
        {
            t.HasCheckConstraint(
                "ck_admissions_source", EnumWire.CheckConstraint<AdmissionSource>("source"));
            t.HasCheckConstraint(
                "ck_admissions_category", EnumWire.CheckConstraint<AdmissionCategory>("category"));
            t.HasCheckConstraint(
                "ck_admissions_urgency", EnumWire.CheckConstraint<AdmissionUrgency>("urgency"));
            t.HasCheckConstraint(
                "ck_admissions_status", EnumWire.CheckConstraint<AdmissionStatus>("status"));
            t.HasCheckConstraint(
                "ck_admissions_cancel_reason",
                $"cancel_reason IS NULL OR {EnumWire.CheckConstraint<CancelReason>("cancel_reason")}");
        });

        builder.HasKey(a => a.Id);

        builder.Property(a => a.Source)
            .HasConversion(new SnakeCaseEnumConverter<AdmissionSource>())
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(a => a.Category)
            .HasConversion(new SnakeCaseEnumConverter<AdmissionCategory>())
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(a => a.Urgency)
            .HasConversion(new SnakeCaseEnumConverter<AdmissionUrgency>())
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(a => a.Status)
            .HasConversion(new SnakeCaseEnumConverter<AdmissionStatus>())
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(a => a.CancelReason)
            .HasConversion(new SnakeCaseEnumConverter<CancelReason>())
            .HasMaxLength(30);

        builder.Property(a => a.CancelNote).HasMaxLength(500);

        builder.Property(a => a.IsInfectious).HasDefaultValue(false);

        builder.Property(a => a.DispatchId).HasMaxLength(64);

        builder.Property(a => a.MissingFields)
            .HasColumnType("text[]")
            .HasDefaultValueSql("'{}'::text[]")
            .IsRequired();

        builder.Property(a => a.DetailsComplete)
            .HasComputedColumnSql("cardinality(missing_fields) = 0", stored: true);

        builder.HasOne(a => a.Patient)
            .WithMany(p => p.Admissions)
            .HasForeignKey(a => a.PatientId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Entities.Common.StaffMember>()
            .WithMany()
            .HasForeignKey(a => a.CategorySetByStaffMemberId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Entities.Common.PatientAccount>()
            .WithMany()
            .HasForeignKey(a => a.ReportedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(a => a.PatientId, "ix_admissions_patient_id")
            .HasDatabaseName("ix_admissions_patient_id");

        builder.HasIndex(a => new { a.Status, a.ExpectedArrivalAt })
            .HasDatabaseName("ix_admissions_status_expected_arrival_at");

        builder.HasIndex(a => a.DispatchId)
            .HasDatabaseName("ix_admissions_dispatch_id")
            .HasFilter("dispatch_id IS NOT NULL");

        builder.HasIndex(a => a.PatientId, OpenAdmissionUniqueIndex)
            .HasDatabaseName(OpenAdmissionUniqueIndex)
            .IsUnique()
            .HasFilter("status NOT IN ('discharged', 'cancelled')");

        builder.HasQueryFilter(a => a.Patient.IsActive);
    }
}
