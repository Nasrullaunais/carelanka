using CareLanka.Api.Common.Persistence;
using CareLanka.Api.Data.Entities.Equipment;
using CareLanka.Api.Data.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CareLanka.Api.Data.Configurations.Equipment;

public class PharmacyTransactionConfiguration : IEntityTypeConfiguration<PharmacyTransaction>
{
    public const string ItemHistoryIndex = "ix_pharmacy_transactions_item_created_at";

    public void Configure(EntityTypeBuilder<PharmacyTransaction> builder)
    {
        builder.ToTable("pharmacy_transactions", t =>
        {
            t.HasCheckConstraint(
                "ck_pharmacy_transactions_type",
                EnumWire.CheckConstraint<PharmacyTransactionType>("type"));

            t.HasCheckConstraint("ck_pharmacy_transactions_quantity", "quantity > 0");
        });

        builder.HasKey(t => t.Id);

        builder.Property(t => t.Type)
            .HasConversion(new SnakeCaseEnumConverter<PharmacyTransactionType>())
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(t => t.Quantity).IsRequired();
        builder.Property(t => t.Note).HasMaxLength(300);

        builder.HasOne<PharmacyItem>()
            .WithMany(i => i.Transactions)
            .HasForeignKey(t => t.PharmacyItemId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(t => new { t.PharmacyItemId, t.CreatedAt })
            .HasDatabaseName(ItemHistoryIndex)
            .IsDescending(false, true);

    }
}
