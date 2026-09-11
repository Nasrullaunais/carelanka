using CareLanka.Api.Common.Persistence;
using CareLanka.Api.Data.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PatientEntity = CareLanka.Api.Data.Entities.Patient.Patient;

namespace CareLanka.Api.Data.Configurations.Patient;

// The alias is not decoration: this namespace ends in "Patient", so the bare name Patient
// resolves to the namespace, not the class, and the file will not compile without it.
public class PatientConfiguration : IEntityTypeConfiguration<PatientEntity>
{
    public const string PatientCodeUniqueIndex = "ux_patients_patient_code";

    /// <summary>How long a patient code is. Short enough to read out over a ward phone.</summary>
    public const int PatientCodeLength = 8;

    public const string NicUniqueIndex = "ux_patients_nic";
    public const string TempReferenceUniqueIndex = "ux_patients_temp_reference";
    public const string UserAccountUniqueIndex = "ux_patients_user_account_id";

    public void Configure(EntityTypeBuilder<PatientEntity> builder)
    {
        builder.ToTable("patients", t =>
        {
            t.HasCheckConstraint("ck_patients_gender", EnumWire.CheckConstraint<Gender>("gender"));

            // Every patient row carries at least one identifier. Without this, three
            // unidentified arrivals in one evening are three rows differing only by id and
            // staff have no handle to say which one they mean.
            t.HasCheckConstraint(
                "ck_patients_identifier",
                "nic IS NOT NULL OR phone IS NOT NULL OR temp_reference IS NOT NULL");
        });

        builder.HasKey(p => p.Id);

        builder.Property(p => p.PatientCode).HasMaxLength(PatientCodeLength).IsRequired();
        builder.Property(p => p.FullName).HasMaxLength(200).IsRequired();
        builder.Property(p => p.Nic).HasMaxLength(20);
        builder.Property(p => p.TempReference).HasMaxLength(30);
        builder.Property(p => p.Phone).HasMaxLength(20);
        builder.Property(p => p.Address).HasMaxLength(300);
        builder.Property(p => p.EmergencyContactName).HasMaxLength(200);
        builder.Property(p => p.EmergencyContactPhone).HasMaxLength(20);

        builder.Property(p => p.Gender)
            .HasConversion(new SnakeCaseEnumConverter<Gender>())
            .HasMaxLength(20)
            .IsRequired();

        // The one unique index here NOT scoped to is_active, and deliberately so. The repo rule
        // scopes them because a human re-enters a code after a merge or a deactivation — a ward
        // called ICU-1 has to be creatable again. Nobody ever types a patient code in to create
        // one; the server picks it. Handing a new person a deactivated record's code would make
        // one wristband resolve to two people, so the database refuses it outright.
        builder.HasIndex(p => p.PatientCode)
            .HasDatabaseName(PatientCodeUniqueIndex)
            .IsUnique();

        // Unique only where the value exists — most patients have a NIC, unidentified
        // arrivals have none, and a plain UNIQUE would allow exactly one of the latter.
        builder.HasIndex(p => p.Nic)
            .HasDatabaseName(NicUniqueIndex)
            .IsUnique()
            .HasFilter("nic IS NOT NULL AND is_active");

        builder.HasIndex(p => p.TempReference)
            .HasDatabaseName(TempReferenceUniqueIndex)
            .IsUnique()
            .HasFilter("temp_reference IS NOT NULL AND is_active");

        builder.HasIndex(p => p.UserAccountId)
            .HasDatabaseName(UserAccountUniqueIndex)
            .IsUnique()
            .HasFilter("user_account_id IS NOT NULL AND is_active");

        // A foreign key with no navigation. The constraint is real; the join is not offered,
        // because patient_accounts belongs to common auth and this component only needs to
        // know whether the link exists.
        builder.HasOne<Entities.Common.PatientAccount>()
            .WithMany()
            .HasForeignKey(p => p.UserAccountId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasQueryFilter(p => p.IsActive);
    }
}
