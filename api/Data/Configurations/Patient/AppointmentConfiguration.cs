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

        builder.HasOne<Entities.Common.StaffMember>()
            .WithMany()
            .HasForeignKey(a => a.BookedByStaffMemberId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(a => a.Admission)
            .WithMany()
            .HasForeignKey(a => a.AdmissionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(a => a.PatientId).HasDatabaseName("ix_appointments_patient_id");

        builder.HasQueryFilter(a => a.Patient.IsActive);

        builder.HasIndex(a => new { a.ScheduledAt, a.Status })
            .HasDatabaseName("ix_appointments_scheduled_at_status");
    }
}
