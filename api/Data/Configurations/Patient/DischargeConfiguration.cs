using CareLanka.Api.Common.Persistence;
using CareLanka.Api.Data.Entities.Patient;
using CareLanka.Api.Data.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CareLanka.Api.Data.Configurations.Patient;

public class DischargeConfiguration : IEntityTypeConfiguration<Discharge>
{
    public const string AdmissionUniqueIndex = "ux_discharges_admission_id";

    public void Configure(EntityTypeBuilder<Discharge> builder)
    {
        builder.ToTable("discharges", t => t.HasCheckConstraint(
            "ck_discharges_flagged_by", EnumWire.CheckConstraint<AssignedBy>("flagged_by")));

        builder.HasKey(d => d.Id);

        builder.Property(d => d.FlaggedBy)
            .HasConversion(new SnakeCaseEnumConverter<AssignedBy>())
            .HasMaxLength(10)
            .IsRequired();

        builder.Property(d => d.SummaryNote).HasMaxLength(2000);

        // One discharge per admission, enforced rather than assumed.
        builder.HasOne(d => d.Admission)
            .WithOne(a => a.Discharge)
            .HasForeignKey<Discharge>(d => d.AdmissionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(d => d.AdmissionId)
            .HasDatabaseName(AdmissionUniqueIndex)
            .IsUnique();

        builder.HasOne<Entities.Common.StaffMember>()
            .WithMany()
            .HasForeignKey(d => d.ConfirmedByStaffMemberId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasQueryFilter(d => d.Admission.Patient.IsActive);
    }
}
