using CareLanka.Api.Common.Persistence;
using CareLanka.Api.Data.Entities.Emergency;
using CareLanka.Api.Data.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CareLanka.Api.Data.Configurations.Emergency;

public sealed class PreAdmissionNoticeConfiguration : IEntityTypeConfiguration<PreAdmissionNotice>
{
    public void Configure(EntityTypeBuilder<PreAdmissionNotice> builder)
    {
        builder.ToTable("pre_admission_notices", table =>
        {
            table.HasCheckConstraint("ck_pre_admission_notices_status", EnumWire.CheckConstraint<PreAdmissionStatus>("status"));
            table.HasCheckConstraint("ck_pre_admission_notices_attempts", "attempt_count >= 0");
        });

        builder.HasKey(notice => notice.Id);
        builder.Property(notice => notice.Status)
            .HasConversion(new SnakeCaseEnumConverter<PreAdmissionStatus>())
            .HasMaxLength(20)
            .IsRequired();
        builder.Property(notice => notice.FailureReason).HasMaxLength(200);
        builder.HasOne(notice => notice.EmergencyCall)
            .WithMany()
            .HasForeignKey(notice => notice.EmergencyCallId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Dispatch>()
            .WithMany()
            .HasForeignKey(notice => notice.DispatchId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(notice => notice.EmergencyCallId)
            .HasDatabaseName("ux_pre_admission_notices_call")
            .IsUnique();
        builder.HasIndex(notice => notice.NextAttemptAt)
            .HasDatabaseName("ix_pre_admission_notices_due")
            .HasFilter("status = 'queued'");
    }
}
