using CareLanka.Api.Common.Persistence;
using CareLanka.Api.Data.Entities.Emergency;
using CareLanka.Api.Data.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CareLanka.Api.Data.Configurations.Emergency;

public sealed class AmbulanceConfiguration : IEntityTypeConfiguration<Ambulance>
{
    public const string RegistrationNumberUniqueIndex = "ux_ambulances_registration_number";

    public void Configure(EntityTypeBuilder<Ambulance> builder)
    {
        builder.ToTable("ambulances", table =>
        {
            table.HasCheckConstraint("ck_ambulances_status", EnumWire.CheckConstraint<AmbulanceStatus>("status"));
            table.HasCheckConstraint("ck_ambulances_latitude", "current_latitude IS NULL OR current_latitude BETWEEN -90 AND 90");
            table.HasCheckConstraint("ck_ambulances_longitude", "current_longitude IS NULL OR current_longitude BETWEEN -180 AND 180");
        });

        builder.HasKey(ambulance => ambulance.Id);
        builder.Property(ambulance => ambulance.RegistrationNumber).HasMaxLength(20).IsRequired();
        builder.Property(ambulance => ambulance.CurrentLatitude).HasPrecision(9, 6);
        builder.Property(ambulance => ambulance.CurrentLongitude).HasPrecision(9, 6);
        builder.Property(ambulance => ambulance.Status)
            .HasConversion(new SnakeCaseEnumConverter<AmbulanceStatus>())
            .HasMaxLength(20)
            .IsRequired();
        builder.Property(ambulance => ambulance.OutOfServiceReason).HasMaxLength(500);

        builder.HasIndex(ambulance => ambulance.RegistrationNumber)
            .HasDatabaseName(RegistrationNumberUniqueIndex)
            .IsUnique()
            .HasFilter("is_active");
        builder.HasIndex(ambulance => ambulance.Status);
    }
}
