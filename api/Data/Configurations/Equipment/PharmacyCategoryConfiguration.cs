using CareLanka.Api.Data.Entities.Equipment;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CareLanka.Api.Data.Configurations.Equipment;

public class PharmacyCategoryConfiguration : IEntityTypeConfiguration<PharmacyCategory>
{
    public const string NameUniqueIndex = "ux_pharmacy_categories_name";

    public void Configure(EntityTypeBuilder<PharmacyCategory> builder)
    {
        builder.ToTable("pharmacy_categories");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.Name).HasMaxLength(150).IsRequired();
        builder.Property(c => c.RequiresPrescription).IsRequired();

        // Scoped WHERE is_active, like every other unique index in this codebase: retiring
        // a category must not make its name unusable forever, and the query filter would
        // hide the clashing row from the service-layer check.
        builder.HasIndex(c => c.Name)
            .HasDatabaseName(NameUniqueIndex)
            .IsUnique()
            .HasFilter("is_active");

        builder.HasQueryFilter(c => c.IsActive);
    }
}
