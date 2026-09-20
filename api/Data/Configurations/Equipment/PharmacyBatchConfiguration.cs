using CareLanka.Api.Data.Entities.Equipment;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CareLanka.Api.Data.Configurations.Equipment;

public class PharmacyBatchConfiguration : IEntityTypeConfiguration<PharmacyBatch>
{
    public const string NumberUniqueIndex = "ux_pharmacy_batches_item_number";

    public void Configure(EntityTypeBuilder<PharmacyBatch> builder)
    {
        builder.ToTable("pharmacy_batches", t => t.HasCheckConstraint(
            "ck_pharmacy_batches_quantity", "quantity_on_hand >= 0"));

        builder.HasKey(b => b.Id);

        builder.Property(b => b.BatchNumber).IsRequired();
        builder.Property(b => b.Reference).HasMaxLength(50);
        builder.Property(b => b.QuantityOnHand).IsRequired();
        builder.Property(b => b.Note).HasMaxLength(300);

        builder.HasOne(b => b.Item)
            .WithMany(i => i.Batches)
            .HasForeignKey(b => b.PharmacyItemId)
            .OnDelete(DeleteBehavior.Cascade);

        // Two deliveries of the same medicine cannot both be "batch 2".
        builder.HasIndex(b => new { b.PharmacyItemId, b.BatchNumber })
            .HasDatabaseName(NumberUniqueIndex)
            .IsUnique();

        // What the dispensing rule reads: the batch expiring first, among those with stock left.
        builder.HasIndex(b => new { b.PharmacyItemId, b.ExpiryDate })
            .HasFilter("quantity_on_hand > 0");
    }
}
