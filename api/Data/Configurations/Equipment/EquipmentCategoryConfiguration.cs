using CareLanka.Api.Data.Entities.Equipment;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CareLanka.Api.Data.Configurations.Equipment;

public class EquipmentCategoryConfiguration : IEntityTypeConfiguration<EquipmentCategory>
{
    public const string NameUniqueIndex = "ux_equipment_categories_name";

    public void Configure(EntityTypeBuilder<EquipmentCategory> builder)
    {
        builder.ToTable("equipment_categories");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.Name).HasMaxLength(150).IsRequired();

        builder.HasIndex(c => c.Name)
            .HasDatabaseName(NameUniqueIndex)
            .IsUnique()
            .HasFilter("is_active");

        builder.HasQueryFilter(c => c.IsActive);
    }
}
