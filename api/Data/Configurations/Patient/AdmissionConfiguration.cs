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

        builder.Property(a => a.IsInfectious).HasDefaultValue(false);

        builder.Property(a => a.DispatchId).HasMaxLength(64);

        builder.Property(a => a.MissingFields)
            .HasColumnType("text[]")
            .HasDefaultValueSql("'{}'::text[]")
            .IsRequired();

        // Stored generated column. The one place a computed value is right here: unlike
        // Status it has no transition rules to enforce, so it cannot drift by construction.
        builder.Property(a => a.DetailsComplete)
            .HasComputedColumnSql("cardinality(missing_fields) = 0", stored: true);

        builder.HasOne(a => a.Patient)
            .WithMany(p => p.Admissions)
            .HasForeignKey(a => a.PatientId)
            .OnDelete(DeleteBehavior.Restrict);

        // Foreign keys with no navigation, per integration_of_functions.md §5.1: the id is
        // the fact, the name is Staff's to serve. Leaving the navigation off is also what
        // keeps a deactivated clinician from taking every admission they ever categorised
        // out of the results — StaffMember is soft-deletable and this end is required.
        builder.HasOne<Entities.Common.StaffMember>()
            .WithMany()
            .HasForeignKey(a => a.CategorySetByStaffMemberId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Entities.Common.PatientAccount>()
            .WithMany()
            .HasForeignKey(a => a.ReportedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        // Named overload plus HasDatabaseName on both, and both are needed. EF keys an index
        // by its property set, so two plain HasIndex(a => a.PatientId) calls are one index and
        // the second silently replaces the first - which quietly dropped this one, the one
        // that serves a patient's visit history across every status. The name in the overload
        // is the model name; without HasDatabaseName as well the second index reaches the
        // database as ix_admissions_patient_id1.
        builder.HasIndex(a => a.PatientId, "ix_admissions_patient_id")
            .HasDatabaseName("ix_admissions_patient_id");

        // Drives the admissions worklist and incoming_next_2h, which Staff reads to staff
        // ahead of a rush rather than react to one.
        builder.HasIndex(a => new { a.Status, a.ExpectedArrivalAt })
            .HasDatabaseName("ix_admissions_status_expected_arrival_at");

        builder.HasIndex(a => a.DispatchId)
            .HasDatabaseName("ix_admissions_dispatch_id")
            .HasFilter("dispatch_id IS NOT NULL");

        // One person, one open stay. Scoped to the statuses that are still running, so a
        // patient can be admitted again after they are discharged or the visit is cancelled.
        //
        // This is the guarantee, not the service-layer read that precedes it: two desks
        // admitting the same person in the same instant both see "no open admission" and both
        // insert. Same reasoning as ux_patients_nic and ux_wards_name.
        builder.HasIndex(a => a.PatientId, OpenAdmissionUniqueIndex)
            .HasDatabaseName(OpenAdmissionUniqueIndex)
            .IsUnique()
            .HasFilter("status NOT IN ('discharged', 'cancelled')");

        builder.HasQueryFilter(a => a.Patient.IsActive);
    }
}
