using CareLanka.Api.Data.Entities.Patient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CareLanka.Api.Data.Configurations.Patient;

public class BillConfiguration : IEntityTypeConfiguration<Bill>
{
    public const string AdmissionUniqueIndex = "ux_bills_admission_id";
    public const string AppointmentUniqueIndex = "ux_bills_appointment_id";
    public const string BillNumberUniqueIndex = "ux_bills_bill_number";

    public const string OneOwnerCheck = "ck_bills_one_owner";

    public const int BillNumberLength = 8;

    public void Configure(EntityTypeBuilder<Bill> builder)
    {
        builder.ToTable("bills", t => t.HasCheckConstraint(
            OneOwnerCheck,
            "(admission_id IS NULL) <> (appointment_id IS NULL)"));

        builder.HasKey(b => b.Id);

        builder.Property(b => b.BillNumber)
            .HasMaxLength(BillNumberLength)
            .IsRequired();

        builder.Property(b => b.SettlementNote).HasMaxLength(300);

        builder.Ignore(b => b.IsSettled);

        builder.HasOne(b => b.Admission)
            .WithOne()
            .HasForeignKey<Bill>(b => b.AdmissionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(b => b.Appointment)
            .WithOne(a => a.Bill)
            .HasForeignKey<Bill>(b => b.AppointmentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(b => b.AdmissionId)
            .HasDatabaseName(AdmissionUniqueIndex)
            .HasFilter("admission_id IS NOT NULL")
            .IsUnique();

        builder.HasIndex(b => b.AppointmentId)
            .HasDatabaseName(AppointmentUniqueIndex)
            .HasFilter("appointment_id IS NOT NULL")
            .IsUnique();

        builder.HasIndex(b => b.BillNumber)
            .HasDatabaseName(BillNumberUniqueIndex)
            .IsUnique();

        builder.HasOne<Entities.Common.StaffMember>()
            .WithMany()
            .HasForeignKey(b => b.SettledByStaffMemberId)
            .OnDelete(DeleteBehavior.Restrict);

        // A bill hangs off one or the other, so the filter has to reach the
        // patient down whichever leg is set. Following only the admission
        // would hide every appointment bill.
        builder.HasQueryFilter(b =>
            (b.Admission != null && b.Admission.Patient.IsActive)
            || (b.Appointment != null && b.Appointment.Patient.IsActive));
    }
}
