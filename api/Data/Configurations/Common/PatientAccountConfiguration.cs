using CareLanka.Api.Data.Entities.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CareLanka.Api.Data.Configurations.Common;

public class PatientAccountConfiguration : IEntityTypeConfiguration<PatientAccount>
{
    public const string UsernameUniqueIndex = "ux_patient_accounts_username";

    public void Configure(EntityTypeBuilder<PatientAccount> builder)
    {
        builder.ToTable("patient_accounts");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.Username).HasMaxLength(50).IsRequired();
        builder.Property(p => p.PasswordHash).HasMaxLength(512).IsRequired();

        builder.HasIndex(p => p.Username)
            .HasDatabaseName(UsernameUniqueIndex)
            .IsUnique()
            .HasFilter("is_active");

        builder.HasQueryFilter(p => p.IsActive);
    }
}
