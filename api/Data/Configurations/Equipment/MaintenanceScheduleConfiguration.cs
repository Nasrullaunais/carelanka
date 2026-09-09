using CareLanka.Api.Common.Persistence;
using CareLanka.Api.Data.Entities.Equipment;
using CareLanka.Api.Data.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CareLanka.Api.Data.Configurations.Equipment;

public class MaintenanceScheduleConfiguration : IEntityTypeConfiguration<MaintenanceSchedule>
{
    public void Configure(EntityTypeBuilder<MaintenanceSchedule> builder)
    {
        builder.ToTable("maintenance_schedules", t =>
        {
            t.HasCheckConstraint(
                "ck_maintenance_schedules_asset_type", EnumWire.CheckConstraint<AssetType>("asset_type"));
            t.HasCheckConstraint(
                "ck_maintenance_schedules_schedule_type",
                EnumWire.CheckConstraint<MaintenanceType>("schedule_type"));
            t.HasCheckConstraint(
                "ck_maintenance_schedules_status",
                EnumWire.CheckConstraint<MaintenanceStatus>("status"));
            t.HasCheckConstraint(
                "ck_maintenance_schedules_created_by", EnumWire.CheckConstraint<RaisedBy>("created_by"));
        });

        builder.HasKey(m => m.Id);

        builder.Property(m => m.Notes).HasMaxLength(1000);

        builder.Property(m => m.AssetType)
            .HasConversion(new SnakeCaseEnumConverter<AssetType>()).HasMaxLength(20).IsRequired();
        builder.Property(m => m.ScheduleType)
            .HasConversion(new SnakeCaseEnumConverter<MaintenanceType>()).HasMaxLength(20).IsRequired();
        builder.Property(m => m.Status)
            .HasConversion(new SnakeCaseEnumConverter<MaintenanceStatus>()).HasMaxLength(20).IsRequired();
        builder.Property(m => m.CreatedBy)
            .HasConversion(new SnakeCaseEnumConverter<RaisedBy>()).HasMaxLength(10).IsRequired();

        // Service history for one item.
        builder.HasIndex(m => new { m.AssetType, m.AssetId });

        // The due and overdue sweep. Cancelled and completed rows are never in it.
        builder.HasIndex(m => m.ScheduledDate)
            .HasFilter("status IN ('scheduled', 'in_progress')");

        // No polymorphic foreign key: asset_id points at two different tables, so the
        // database cannot enforce it. The service checks the asset exists before writing.
    }
}
