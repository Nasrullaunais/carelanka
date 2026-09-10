using CareLanka.Api.Common.Persistence;
using CareLanka.Api.Data.Entities.Equipment;
using CareLanka.Api.Data.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CareLanka.Api.Data.Configurations.Equipment;

public class EquipmentItemConfiguration : IEntityTypeConfiguration<EquipmentItem>
{
    public const string AssetTagUniqueIndex = "ux_equipment_items_asset_tag";
    public const string SerialNumberUniqueIndex = "ux_equipment_items_serial_number";

    public void Configure(EntityTypeBuilder<EquipmentItem> builder)
    {
        builder.ToTable("equipment_items", t => t.HasCheckConstraint(
            "ck_equipment_items_status", EnumWire.CheckConstraint<EquipmentStatus>("status")));

        builder.HasKey(i => i.Id);

        builder.Property(i => i.Name).HasMaxLength(150).IsRequired();
        builder.Property(i => i.Model).HasMaxLength(100).IsRequired();
        builder.Property(i => i.Manufacturer).HasMaxLength(150).IsRequired();
        builder.Property(i => i.AssetTag).HasMaxLength(50).IsRequired();
        builder.Property(i => i.SerialNumber).HasMaxLength(100);

        builder.Property(i => i.Status)
            .HasConversion(new SnakeCaseEnumConverter<EquipmentStatus>())
            .HasMaxLength(20)
            .IsRequired();

        // The category is ours, so this is a real foreign key. Restrict rather than cascade:
        // deleting a category out from under its items would silently destroy the register.
        builder.HasOne(i => i.Category)
            .WithMany(c => c.Items)
            .HasForeignKey(i => i.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(i => i.AssetTag)
            .HasDatabaseName(AssetTagUniqueIndex)
            .IsUnique()
            .HasFilter("is_active");

        builder.HasIndex(i => i.SerialNumber)
            .HasDatabaseName(SerialNumberUniqueIndex)
            .IsUnique()
            .HasFilter("is_active AND serial_number IS NOT NULL");

        builder.HasIndex(i => i.CategoryId);
        builder.HasIndex(i => i.WardId);
        builder.HasIndex(i => i.Status);

        // The maintenance-due sweep only ever looks at items still in service, so the index
        // does not carry retired rows it would never return.
        builder.HasIndex(i => i.NextMaintenanceDue)
            .HasFilter("status <> 'retired'");

        builder.HasQueryFilter(i => i.IsActive);
    }
}
