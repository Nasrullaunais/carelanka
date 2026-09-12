using CareLanka.Api.Common.Persistence;
using CareLanka.Api.Data.Entities.Common;
using CareLanka.Api.Data.Entities.Emergency;
using CareLanka.Api.Data.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PatientEntity = CareLanka.Api.Data.Entities.Patient.Patient;

namespace CareLanka.Api.Data.Configurations.Emergency;

public sealed class EmergencyCallConfiguration : IEntityTypeConfiguration<EmergencyCall>
{
    public void Configure(EntityTypeBuilder<EmergencyCall> builder)
    {
        builder.ToTable("emergency_calls", table =>
        {
            table.HasCheckConstraint("ck_emergency_calls_priority", EnumWire.CheckConstraint<CallPriority>("priority"));
            table.HasCheckConstraint("ck_emergency_calls_status", EnumWire.CheckConstraint<CallStatus>("status"));
            table.HasCheckConstraint("ck_emergency_calls_latitude", "latitude BETWEEN -90 AND 90");
            table.HasCheckConstraint("ck_emergency_calls_longitude", "longitude BETWEEN -180 AND 180");
        });

        builder.HasKey(call => call.Id);
        builder.Property(call => call.CallerName).HasMaxLength(200);
        builder.Property(call => call.CallerPhone).HasMaxLength(20);
        builder.Property(call => call.AddressLabel).HasMaxLength(500);
        builder.Property(call => call.Details).HasMaxLength(1000);
        builder.Property(call => call.Outcome).HasMaxLength(1000);
        builder.Property(call => call.Latitude).HasPrecision(9, 6).IsRequired();
        builder.Property(call => call.Longitude).HasPrecision(9, 6).IsRequired();
        builder.Property(call => call.Priority)
            .HasConversion(new SnakeCaseEnumConverter<CallPriority>())
            .HasMaxLength(20)
            .IsRequired();
        builder.Property(call => call.Status)
            .HasConversion(new SnakeCaseEnumConverter<CallStatus>())
            .HasMaxLength(20)
            .IsRequired();

        builder.HasOne<PatientEntity>()
            .WithMany()
            .HasForeignKey(call => call.PatientId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<PatientAccount>()
            .WithMany()
            .HasForeignKey(call => call.CallerUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(call => new { call.Status, call.CreatedAt });
        builder.HasIndex(call => new { call.Priority, call.CreatedAt });
    }
}
