using CareLanka.Api.Data.Entities.Patient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CareLanka.Api.Data.Configurations.Patient;

public class BillConfiguration : IEntityTypeConfiguration<Bill>
{
    public const string AdmissionUniqueIndex = "ux_bills_admission_id";
    public const string BillNumberUniqueIndex = "ux_bills_bill_number";

    public const int BillNumberLength = 8;

    public void Configure(EntityTypeBuilder<Bill> builder)
    {
        builder.ToTable("bills");

        builder.HasKey(b => b.Id);

        builder.Property(b => b.BillNumber)
            .HasMaxLength(BillNumberLength)
            .IsRequired();

        builder.Property(b => b.SettlementNote).HasMaxLength(300);

        // IsSettled is Settled At read a different way, not a column. EF would try to map it
        // otherwise and the migration would carry a field that can disagree with the timestamp.
        builder.Ignore(b => b.IsSettled);

        // One bill per visit, enforced rather than assumed - the same shape as Discharge.
        builder.HasOne(b => b.Admission)
            .WithOne()
            .HasForeignKey<Bill>(b => b.AdmissionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(b => b.AdmissionId)
            .HasDatabaseName(AdmissionUniqueIndex)
            .IsUnique();

        // Not scoped WHERE is_active: a bill number is quoted by a patient holding a piece of
        // paper, so it has to stay unique for good. Bills are not soft-deletable anyway.
        builder.HasIndex(b => b.BillNumber)
            .HasDatabaseName(BillNumberUniqueIndex)
            .IsUnique();

        builder.HasOne<Entities.Common.StaffMember>()
            .WithMany()
            .HasForeignKey(b => b.SettledByStaffMemberId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasQueryFilter(b => b.Admission.Patient.IsActive);
    }
}
