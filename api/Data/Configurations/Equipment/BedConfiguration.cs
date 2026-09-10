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

        // Scoped WHERE is_active, for the reason WardConfiguration gives: a plain UNIQUE
        // would make bed 3 in a ward unusable forever once it had been retired once, and
        // the query filter hides the blocking row so the service duplicate check passes
        // and SaveChanges throws instead.
        builder.HasIndex(b => new { b.WardId, b.BedNumber })
            .HasDatabaseName(WardNumberUniqueIndex)
            .IsUnique()
            .HasFilter("is_active");

        // Also scoped to rows that have a tag at all. Postgres treats NULLs as distinct in
        // a unique index, but naming the condition keeps the intent readable in the DDL.
        builder.HasIndex(b => b.AssetTag)
            .HasDatabaseName(AssetTagUniqueIndex)
            .IsUnique()
            .HasFilter("is_active AND asset_tag IS NOT NULL");

        // Every read Patient Management makes filters or groups by ward.
        builder.HasIndex(b => b.WardId);

        builder.HasQueryFilter(b => b.IsActive);
    }
}
