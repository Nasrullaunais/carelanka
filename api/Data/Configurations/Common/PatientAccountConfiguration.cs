using CareLanka.Api.Data.Entities.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CareLanka.Api.Data.Configurations.Common;

public class PatientAccountConfiguration : IEntityTypeConfiguration<PatientAccount>
{
    public void Configure(EntityTypeBuilder<PatientAccount> builder)
    {
        builder.ToTable("patient_accounts");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.PhoneNumber).HasMaxLength(20).IsRequired();
        builder.Property(p => p.PasswordHash).HasMaxLength(512).IsRequired();
        builder.Property(p => p.FullName).HasMaxLength(200).IsRequired();

        builder.HasIndex(p => p.PhoneNumber)
            .HasDatabaseName("ux_patient_accounts_phone")
            .IsUnique()
            .HasFilter("is_active");

        builder.HasQueryFilter(p => p.IsActive);
    }
}
