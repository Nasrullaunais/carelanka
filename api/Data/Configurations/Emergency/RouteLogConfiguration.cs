using CareLanka.Api.Data.Entities.Emergency;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CareLanka.Api.Data.Configurations.Emergency;

public sealed class RouteLogConfiguration : IEntityTypeConfiguration<RouteLog>
{
    public void Configure(EntityTypeBuilder<RouteLog> builder)
    {
        builder.ToTable("route_logs", table =>
        {
            table.HasCheckConstraint("ck_route_logs_origin_latitude", "origin_latitude BETWEEN -90 AND 90");
            table.HasCheckConstraint("ck_route_logs_origin_longitude", "origin_longitude BETWEEN -180 AND 180");
            table.HasCheckConstraint("ck_route_logs_destination_latitude", "destination_latitude BETWEEN -90 AND 90");
            table.HasCheckConstraint("ck_route_logs_destination_longitude", "destination_longitude BETWEEN -180 AND 180");
            table.HasCheckConstraint("ck_route_logs_distance", "planned_distance_km >= 0");
            table.HasCheckConstraint("ck_route_logs_duration", "planned_duration_minutes >= 0");
        });

        builder.HasKey(route => route.Id);
        builder.Property(route => route.OriginLatitude).HasPrecision(9, 6).IsRequired();
        builder.Property(route => route.OriginLongitude).HasPrecision(9, 6).IsRequired();
        builder.Property(route => route.DestinationLatitude).HasPrecision(9, 6).IsRequired();
        builder.Property(route => route.DestinationLongitude).HasPrecision(9, 6).IsRequired();
        builder.Property(route => route.PlannedDistanceKm).HasPrecision(7, 2).IsRequired();
        builder.Property(route => route.PlannedDurationMinutes).IsRequired();
        builder.Property(route => route.MapsApiReference).HasMaxLength(200);
        builder.HasOne(route => route.Dispatch)
            .WithOne(dispatch => dispatch.RouteLog)
            .HasForeignKey<RouteLog>(route => route.DispatchId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(route => route.DispatchId).IsUnique();
    }
}
