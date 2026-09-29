using CareLanka.Api.Common.Persistence;
using CareLanka.Api.Data.Entities.Common;
using CareLanka.Api.Data.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CareLanka.Api.Data.Configurations.Common;

public class NotificationDeliveryConfiguration : IEntityTypeConfiguration<NotificationDelivery>
{
    public void Configure(EntityTypeBuilder<NotificationDelivery> builder)
    {
        builder.ToTable("notification_deliveries", t =>
        {
            t.HasCheckConstraint("ck_notification_deliveries_channel", EnumWire.CheckConstraint<NotificationChannel>("channel"));
            t.HasCheckConstraint("ck_notification_deliveries_status", EnumWire.CheckConstraint<NotificationStatus>("status"));
        });

        builder.HasKey(d => d.Id);

        builder.Property(d => d.Channel)
            .HasConversion(new SnakeCaseEnumConverter<NotificationChannel>())
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(d => d.Status)
            .HasConversion(new SnakeCaseEnumConverter<NotificationStatus>())
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(d => d.FailureReason).HasMaxLength(200);

        builder.HasIndex(d => d.NextAttemptAt)
            .HasDatabaseName("ix_notification_deliveries_due")
            .HasFilter("status = 'queued'");

        builder.HasOne(d => d.Notification)
            .WithMany(n => n.Deliveries)
            .HasForeignKey(d => d.NotificationId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
