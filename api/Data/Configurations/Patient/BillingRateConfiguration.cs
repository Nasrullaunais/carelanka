using CareLanka.Api.Common.Persistence;
using CareLanka.Api.Data.Entities.Patient;
using CareLanka.Api.Data.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CareLanka.Api.Data.Configurations.Patient;

public class BillingRateConfiguration : IEntityTypeConfiguration<BillingRate>
{
    public const string CellUniqueIndex = "ux_billing_rates_ward_expense";

    public void Configure(EntityTypeBuilder<BillingRate> builder)
    {
        builder.ToTable("billing_rates", t =>
        {
            t.HasCheckConstraint(
                "ck_billing_rates_ward_type", EnumWire.CheckConstraint<WardType>("ward_type"));

            t.HasCheckConstraint("ck_billing_rates_amount", "amount >= 0");
        });

        builder.HasKey(r => r.Id);

        builder.Property(r => r.WardType)
            .HasConversion(new SnakeCaseEnumConverter<WardType>())
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(r => r.ExpenseKey).HasMaxLength(40).IsRequired();

        builder.Property(r => r.Amount).HasPrecision(12, 2).IsRequired();

        builder.HasIndex(r => new { r.WardType, r.ExpenseKey })
            .HasDatabaseName(CellUniqueIndex)
            .IsUnique()
            .HasFilter("is_active");

        builder.HasQueryFilter(r => r.IsActive);
    }
}
