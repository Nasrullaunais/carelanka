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
            t.HasCheckConstraint("ck_notifications_type", EnumWire.CheckConstraint<NotificationType>("type"));
            t.HasCheckConstraint("ck_notifications_one_recipient",
                "num_nonnulls(recipient_staff_member_id, recipient_patient_account_id) = 1");
        });

        builder.HasKey(n => n.Id);

        builder.Property(n => n.Type)
            .HasConversion(new SnakeCaseEnumConverter<NotificationType>())
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(n => n.Title).HasMaxLength(200).IsRequired();
        builder.Property(n => n.Body).HasMaxLength(500).IsRequired();
        builder.Property(n => n.EntityType).HasMaxLength(50);
        builder.Property(n => n.DedupeKey).HasMaxLength(200).IsRequired();

        builder.HasIndex(n => n.DedupeKey)
            .HasDatabaseName("ux_notifications_dedupe_key")
            .IsUnique();

        builder.HasIndex(n => new { n.RecipientStaffMemberId, n.CreatedAt })
            .HasDatabaseName("ix_notifications_staff_recipient");

        builder.HasIndex(n => new { n.RecipientPatientAccountId, n.CreatedAt })
            .HasDatabaseName("ix_notifications_patient_recipient");

        builder.HasIndex(n => n.RecipientStaffMemberId)
            .HasDatabaseName("ix_notifications_staff_unread")
            .HasFilter("read_at IS NULL");

        builder.HasIndex(n => n.RecipientPatientAccountId)
            .HasDatabaseName("ix_notifications_patient_unread")
            .HasFilter("read_at IS NULL");

        builder.HasOne(n => n.RecipientStaffMember)
            .WithMany()
            .HasForeignKey(n => n.RecipientStaffMemberId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(n => n.RecipientPatientAccount)
            .WithMany()
            .HasForeignKey(n => n.RecipientPatientAccountId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
