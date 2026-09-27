using CareLanka.Api.Data.Entities.Patient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CareLanka.Api.Data.Configurations.Patient;

public class PatientMedicalProfileConfiguration : IEntityTypeConfiguration<PatientMedicalProfile>
{
    public const string PatientUniqueIndex = "ux_patient_medical_profiles_patient_id";

    public const int LongFieldLength = 2000;
    public const int AllergiesLength = 1000;

    public void Configure(EntityTypeBuilder<PatientMedicalProfile> builder)
    {
        builder.ToTable("patient_medical_profiles");

        builder.HasKey(profile => profile.Id);

        builder.Property(profile => profile.KnownConditions).HasMaxLength(LongFieldLength);
        builder.Property(profile => profile.Allergies).HasMaxLength(AllergiesLength);
        builder.Property(profile => profile.CurrentSymptoms).HasMaxLength(LongFieldLength);

        builder.HasOne(profile => profile.Patient)
            .WithOne()
            .HasForeignKey<PatientMedicalProfile>(profile => profile.PatientId)
            .OnDelete(DeleteBehavior.Restrict);

        // A plain unique, deliberately not scoped WHERE is_active: this is an AuditedEntity, not
        // a soft-deletable one. There is no such thing as retiring a patient's medical history -
        // the Patient row is the soft-deletable thing and the profile goes with it.
        builder.HasIndex(profile => profile.PatientId)
            .HasDatabaseName(PatientUniqueIndex)
            .IsUnique();

        builder.HasOne<Entities.Common.StaffMember>()
            .WithMany()
            .HasForeignKey(profile => profile.UpdatedByStaffMemberId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasQueryFilter(profile => profile.Patient.IsActive);
    }
}
