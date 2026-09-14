using CareLanka.Api.Common.Persistence;
using CareLanka.Api.Data.Entities.Patient;
using CareLanka.Api.Data.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CareLanka.Api.Data.Configurations.Patient;

public class WardConfiguration : IEntityTypeConfiguration<Ward>
{
    public const string NameUniqueIndex = "ux_wards_name";

    public void Configure(EntityTypeBuilder<Ward> builder)
    {
        builder.ToTable("wards", t =>
        {
            t.HasCheckConstraint("ck_wards_type", EnumWire.CheckConstraint<WardType>("ward_type"));
            t.HasCheckConstraint(
                "ck_wards_gender_policy", EnumWire.CheckConstraint<GenderPolicy>("gender_policy"));
        });

        builder.HasKey(w => w.Id);

        builder.Property(w => w.Name).HasMaxLength(100).IsRequired();

        builder.Property(w => w.WardType)
            .HasConversion(new SnakeCaseEnumConverter<WardType>())
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(w => w.GenderPolicy)
            .HasConversion(new SnakeCaseEnumConverter<GenderPolicy>())
            .HasMaxLength(20)
            .IsRequired();

        builder.HasIndex(w => w.Name)
            .HasDatabaseName(NameUniqueIndex)
            .IsUnique()
            .HasFilter("is_active");

        builder.HasQueryFilter(w => w.IsActive);
    }
}
