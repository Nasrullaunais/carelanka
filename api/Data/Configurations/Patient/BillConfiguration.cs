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

        builder.Ignore(b => b.IsSettled);

        builder.HasOne(b => b.Admission)
            .WithOne()
            .HasForeignKey<Bill>(b => b.AdmissionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(b => b.AdmissionId)
            .HasDatabaseName(AdmissionUniqueIndex)
            .IsUnique();

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
