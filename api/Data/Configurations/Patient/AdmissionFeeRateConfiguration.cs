using CareLanka.Api.Common.Persistence;
using CareLanka.Api.Data.Entities.Patient;
using CareLanka.Api.Data.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CareLanka.Api.Data.Configurations.Patient;

public class AdmissionFeeRateConfiguration : IEntityTypeConfiguration<AdmissionFeeRate>
{
    public const string CategoryUniqueIndex = "ux_admission_fee_rates_category";

    public void Configure(EntityTypeBuilder<AdmissionFeeRate> builder)
    {
        builder.ToTable("admission_fee_rates", t =>
        {
            t.HasCheckConstraint(
                "ck_admission_fee_rates_category",
                EnumWire.CheckConstraint<AdmissionCategory>("category"));

            t.HasCheckConstraint("ck_admission_fee_rates_amount", "amount >= 0");
        });

        builder.HasKey(r => r.Id);

        builder.Property(r => r.Category)
            .HasConversion(new SnakeCaseEnumConverter<AdmissionCategory>())
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(r => r.Amount).HasPrecision(12, 2).IsRequired();

        builder.HasIndex(r => r.Category)
            .HasDatabaseName(CategoryUniqueIndex)
            .IsUnique()
            .HasFilter("is_active");

        builder.HasQueryFilter(r => r.IsActive);
    }
}
