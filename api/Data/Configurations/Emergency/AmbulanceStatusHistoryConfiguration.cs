using CareLanka.Api.Common.Persistence;
using CareLanka.Api.Data.Entities.Emergency;
using CareLanka.Api.Data.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CareLanka.Api.Data.Configurations.Emergency;

public sealed class AmbulanceStatusHistoryConfiguration : IEntityTypeConfiguration<AmbulanceStatusHistory>
{
    public void Configure(EntityTypeBuilder<AmbulanceStatusHistory> builder)
    {
        builder.ToTable("ambulance_status_history", table =>
            table.HasCheckConstraint("ck_ambulance_status_history_status", EnumWire.CheckConstraint<AmbulanceStatus>("status")));
        builder.HasKey(history => history.Id);
        builder.Property(history => history.Status)
            .HasConversion(new SnakeCaseEnumConverter<AmbulanceStatus>())
            .HasMaxLength(20)
            .IsRequired();
        builder.Property(history => history.StartedAt).IsRequired();
        builder.HasOne(history => history.Ambulance)
            .WithMany(ambulance => ambulance.StatusHistory)
            .HasForeignKey(history => history.AmbulanceId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(history => new { history.AmbulanceId, history.StartedAt });
    }
}
