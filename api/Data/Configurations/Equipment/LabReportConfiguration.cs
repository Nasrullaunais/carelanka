using CareLanka.Api.Data.Entities.Equipment;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CareLanka.Api.Data.Configurations.Equipment;

public class LabReportConfiguration : IEntityTypeConfiguration<LabReport>
{
    public const string PatientHistoryIndex = "ix_lab_reports_patient_created_at";

    public void Configure(EntityTypeBuilder<LabReport> builder)
    {
        builder.ToTable("lab_reports", t =>
            // An empty file is a row that looks like a result and opens as nothing. The upload
            // path rejects it first; this is the guarantee that outlives that code path.
            t.HasCheckConstraint("ck_lab_reports_byte_size", "byte_size > 0"));

        builder.HasKey(r => r.Id);

        builder.Property(r => r.TestName).HasMaxLength(120).IsRequired();
        builder.Property(r => r.Summary).HasMaxLength(1000);
        builder.Property(r => r.FileName).HasMaxLength(255).IsRequired();
        builder.Property(r => r.ContentType).HasMaxLength(100).IsRequired();
        builder.Property(r => r.Content).IsRequired();
        builder.Property(r => r.ByteSize).IsRequired();
        builder.Property(r => r.UploadedByStaffId).IsRequired();

        // No foreign key to Patient, deliberately, and no navigation. Patient Management owns
        // that table; a constraint from here would mean Equipment's migrations decide whether
        // one of their rows can be deleted. The same reasoning as
        // EquipmentItem.AssignedToAdmissionId, which is also an id with no constraint behind it.

        // Newest first, per patient. That is the only way this table is ever read: a ward opens
        // one patient and wants the latest result at the top.
        builder.HasIndex(r => new { r.PatientId, r.CreatedAt })
            .HasDatabaseName(PatientHistoryIndex)
            .IsDescending(false, true);

        // No query filter and no soft delete. A result that can be made to disappear is not a
        // medical record, and a correction is a new row rather than an edit.
    }
}
