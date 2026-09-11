using CareLanka.Api.Data.Entities.Equipment;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CareLanka.Api.Data.Configurations.Equipment;

public class PharmacyItemConfiguration : IEntityTypeConfiguration<PharmacyItem>
{
    public const string NameUniqueIndex = "ux_pharmacy_items_name";

    public void Configure(EntityTypeBuilder<PharmacyItem> builder)
    {
        builder.ToTable("pharmacy_items", t =>
        {
            // The last line of defence under the conditional update in PharmacyItemService.
            // If a future caller ever writes the column directly and gets the guard wrong,
            // the database refuses rather than quietly shipping medicine that is not there.
            t.HasCheckConstraint("ck_pharmacy_items_quantity", "quantity_on_hand >= 0");
            t.HasCheckConstraint("ck_pharmacy_items_reorder_threshold", "reorder_threshold >= 0");
        });

        builder.HasKey(i => i.Id);

        builder.Property(i => i.Name).HasMaxLength(200).IsRequired();
        builder.Property(i => i.Manufacturer).HasMaxLength(150);
        builder.Property(i => i.BatchNumber).HasMaxLength(50);
        builder.Property(i => i.Unit).HasMaxLength(20).IsRequired();
        builder.Property(i => i.QuantityOnHand).IsRequired();
        builder.Property(i => i.ReorderThreshold).IsRequired();

        // Money is never a float. Two decimal places, and a width that covers a Rupee price
        // for a pallet of anything this hospital buys.
        builder.Property(i => i.UnitPrice).HasPrecision(12, 2);

        builder.HasOne(i => i.Category)
            .WithMany(c => c.Items)
            .HasForeignKey(i => i.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(i => i.Name)
            .HasDatabaseName(NameUniqueIndex)
            .IsUnique()
            .HasFilter("is_active");

        builder.HasIndex(i => i.CategoryId);

        // The expiry sweep only asks about things that expire, so the index does not carry
        // the bandages and syringes it would never return.
        builder.HasIndex(i => i.ExpiryDate).HasFilter("expiry_date IS NOT NULL");

        builder.HasQueryFilter(i => i.IsActive);
    }
}
