using CareLanka.Api.Common.Persistence;
using CareLanka.Api.Data.Entities.Equipment;
using CareLanka.Api.Data.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CareLanka.Api.Data.Configurations.Equipment;

public class BedConfiguration : IEntityTypeConfiguration<Bed>
{
    public const string WardNumberUniqueIndex = "ux_beds_ward_number";
    public const string AssetTagUniqueIndex = "ux_beds_asset_tag";

    public void Configure(EntityTypeBuilder<Bed> builder)
    {
        builder.ToTable("beds", t =>
        {
            t.HasCheckConstraint("ck_beds_condition", EnumWire.CheckConstraint<BedCondition>("condition"));
            t.HasCheckConstraint("ck_beds_nurse_station_distance", "nurse_station_distance >= 1");
        });

        builder.HasKey(b => b.Id);

        builder.Property(b => b.WardId).IsRequired();

        builder.Property(b => b.BedNumber).HasMaxLength(20).IsRequired();

        builder.Property(b => b.AssetTag).HasMaxLength(50);

        builder.Property(b => b.NurseStationDistance).HasDefaultValue(1).IsRequired();

        builder.Property(b => b.Condition)
            .HasConversion(new SnakeCaseEnumConverter<BedCondition>())
            .HasMaxLength(20)
            .IsRequired();

        builder.HasIndex(b => new { b.WardId, b.BedNumber })
            .HasDatabaseName(WardNumberUniqueIndex)
            .IsUnique()
            .HasFilter("is_active");

        builder.HasIndex(b => b.AssetTag)
            .HasDatabaseName(AssetTagUniqueIndex)
            .IsUnique()
            .HasFilter("is_active AND asset_tag IS NOT NULL");

        builder.HasIndex(b => b.WardId);

        builder.HasQueryFilter(b => b.IsActive);
    }
}
