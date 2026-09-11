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

            // Quantity carries no sign; type does. A zero or negative row would make the
            // consumption report and the agent's usage rate meaningless.
            t.HasCheckConstraint("ck_pharmacy_transactions_quantity", "quantity > 0");
        });

        builder.HasKey(t => t.Id);

        builder.Property(t => t.Type)
            .HasConversion(new SnakeCaseEnumConverter<PharmacyTransactionType>())
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(t => t.Quantity).IsRequired();
        builder.Property(t => t.Note).HasMaxLength(300);

        // No navigation back to the item, deliberately. PharmacyItem is soft-deletable and
        // carries a query filter; a required navigation into a filtered entity makes an
        // Include quietly drop history rows once an item is retired. The trail has to
        // outlive the thing it describes, so the transaction holds the id and nothing more.
        builder.HasOne<PharmacyItem>()
            .WithMany(i => i.Transactions)
            .HasForeignKey(t => t.PharmacyItemId)
            .OnDelete(DeleteBehavior.Restrict);

        // Newest first, per item. Serves both the history endpoint and the usage-rate
        // calculation the low-stock warning needs.
        builder.HasIndex(t => new { t.PharmacyItemId, t.CreatedAt })
            .HasDatabaseName(ItemHistoryIndex)
            .IsDescending(false, true);

        // No query filter and no soft delete: this table is the audit trail.
    }
}
