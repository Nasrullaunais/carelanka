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

        builder.HasIndex(i => i.ExpiryDate).HasFilter("expiry_date IS NOT NULL");

        builder.HasQueryFilter(i => i.IsActive);
    }
}
