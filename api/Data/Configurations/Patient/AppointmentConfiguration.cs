using CareLanka.Api.Common.Persistence;
using CareLanka.Api.Data.Entities.Patient;
using CareLanka.Api.Data.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CareLanka.Api.Data.Configurations.Patient;

public class AppointmentConfiguration : IEntityTypeConfiguration<Appointment>
{
    public void Configure(EntityTypeBuilder<Appointment> builder)
    {
        builder.ToTable("appointments", t => t.HasCheckConstraint(
            "ck_appointments_status", EnumWire.CheckConstraint<AppointmentStatus>("status")));

        builder.HasKey(a => a.Id);

        builder.Property(a => a.Status)
            .HasConversion(new SnakeCaseEnumConverter<AppointmentStatus>())
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(a => a.Reason).HasMaxLength(300);

        builder.HasOne(a => a.Patient)
            .WithMany(p => p.Appointments)
            .HasForeignKey(a => a.PatientId)
            .OnDelete(DeleteBehavior.Restrict);

        // Foreign key, no navigation. integration_of_functions.md §5.1: we store the staff
        // id and nothing else, and resolve the name through POST /staff/lookup at read time.
        // A navigation here would invite Include(), which is the coupling that rule forbids —
        // and StaffMember is soft-deletable, so a retired clerk would drop their bookings out
        // of every query.
        builder.HasOne<Entities.Common.StaffMember>()
            .WithMany()
            .HasForeignKey(a => a.BookedByStaffMemberId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(a => a.Admission)
            .WithMany()
            .HasForeignKey(a => a.AdmissionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(a => a.PatientId).HasDatabaseName("ix_appointments_patient_id");

        // Matches the filter on Patient. Without it, deactivating a patient leaves their
        // bookings visible in every list that does not join to the patient row.
        builder.HasQueryFilter(a => a.Patient.IsActive);

        // The desk's day view: today's bookings, in time order.
        builder.HasIndex(a => new { a.ScheduledAt, a.Status })
            .HasDatabaseName("ix_appointments_scheduled_at_status");
    }
}
