using CareLanka.Api.Common.Persistence;
using CareLanka.Api.Data.Entities.Common;
using CareLanka.Api.Data.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CareLanka.Api.Data.Configurations.Common;

public class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.ToTable("notifications", t =>
        {
            t.HasCheckConstraint("ck_notifications_channel", EnumWire.CheckConstraint<NotificationChannel>("channel"));
            t.HasCheckConstraint("ck_notifications_status", EnumWire.CheckConstraint<NotificationStatus>("status"));
        });

        builder.HasKey(n => n.Id);

        builder.Property(n => n.Channel)
            .HasConversion(new SnakeCaseEnumConverter<NotificationChannel>())
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(n => n.Status)
            .HasConversion(new SnakeCaseEnumConverter<NotificationStatus>())
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(n => n.Title).HasMaxLength(200).IsRequired();
        builder.Property(n => n.Body).HasMaxLength(500).IsRequired();
        builder.Property(n => n.EntityType).HasMaxLength(50);
        builder.Property(n => n.DedupeKey).HasMaxLength(200).IsRequired();
        builder.Property(n => n.FailureReason).HasMaxLength(200);

        builder.HasIndex(n => n.DedupeKey)
            .HasDatabaseName("ux_notifications_dedupe_key")
            .IsUnique();

        builder.HasIndex(n => n.NextAttemptAt)
            .HasDatabaseName("ix_notifications_due")
            .HasFilter("status = 'queued'");

        builder.HasIndex(n => new { n.RecipientStaffMemberId, n.CreatedAt })
            .HasDatabaseName("ix_notifications_recipient");

        builder.HasOne(n => n.RecipientStaffMember)
            .WithMany()
            .HasForeignKey(n => n.RecipientStaffMemberId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
