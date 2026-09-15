using CareLanka.Api.Common.Persistence;
using CareLanka.Api.Data.Entities.Staff;
using CareLanka.Api.Data.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CareLanka.Api.Data.Configurations.Staff;

public class LeaveRequestConfiguration : IEntityTypeConfiguration<LeaveRequest>
{
    public void Configure(EntityTypeBuilder<LeaveRequest> builder)
    {
        builder.ToTable("leave_requests", t =>
        {
            t.HasCheckConstraint(
                "ck_leave_requests_type",
                EnumWire.CheckConstraint<LeaveType>("type"));
            t.HasCheckConstraint(
                "ck_leave_requests_status",
                EnumWire.CheckConstraint<LeaveStatus>("status"));
        });

        builder.HasKey(l => l.Id);

        builder.Property(l => l.Type)
            .HasConversion(new SnakeCaseEnumConverter<LeaveType>())
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(l => l.Status)
            .HasConversion(new SnakeCaseEnumConverter<LeaveStatus>())
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(l => l.Reason).HasMaxLength(1000);
        builder.Property(l => l.ReviewNotes).HasMaxLength(500);

        builder.HasOne<Entities.Common.StaffMember>()
            .WithMany()
            .HasForeignKey(l => l.StaffMemberId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Entities.Common.StaffMember>()
            .WithMany()
            .HasForeignKey(l => l.ReviewedByStaffMemberId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Entities.Common.StaffMember>()
            .WithMany()
            .HasForeignKey(l => l.SwapWithStaffMemberId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(l => l.SwapShift)
            .WithMany()
            .HasForeignKey(l => l.SwapShiftId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(l => l.StaffMemberId)
            .HasDatabaseName("ix_leave_requests_staff_id");

        builder.HasIndex(l => new { l.Status, l.StartDate })
            .HasDatabaseName("ix_leave_requests_status_start");
    }
}
