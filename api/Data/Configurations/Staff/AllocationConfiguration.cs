using CareLanka.Api.Common.Persistence;
using CareLanka.Api.Data.Entities.Staff;
using CareLanka.Api.Data.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CareLanka.Api.Data.Configurations.Staff;

public class AllocationConfiguration : IEntityTypeConfiguration<Allocation>
{
    public const string ConfirmedUniqueIndex = "ux_allocations_confirmed";

    public void Configure(EntityTypeBuilder<Allocation> builder)
    {
        builder.ToTable("allocations", t =>
        {
            t.HasCheckConstraint(
                "ck_allocations_status",
                EnumWire.CheckConstraint<AllocationStatus>("status"));
            t.HasCheckConstraint(
                "ck_allocations_source",
                EnumWire.CheckConstraint<AllocationSource>("source"));
            t.HasCheckConstraint(
                "ck_allocations_ended_reason",
                $"ended_reason IS NULL OR {EnumWire.CheckConstraint<AllocationEndReason>("ended_reason")}");
        });

        builder.HasKey(a => a.Id);

        builder.Property(a => a.Status)
            .HasConversion(new SnakeCaseEnumConverter<AllocationStatus>())
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(a => a.Source)
            .HasConversion(new SnakeCaseEnumConverter<AllocationSource>())
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(a => a.EndedReason)
            .HasConversion(new SnakeCaseEnumConverter<AllocationEndReason>())
            .HasMaxLength(30);

        builder.HasOne(a => a.Shift)
            .WithMany(s => s.Allocations)
            .HasForeignKey(a => a.ShiftId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Entities.Common.StaffMember>()
            .WithMany()
            .HasForeignKey(a => a.StaffMemberId)
            .OnDelete(DeleteBehavior.Restrict);

        // Self-referencing FK: the replacement allocation in a cascading swap.
        builder.HasOne(a => a.ReplacedByAllocation)
            .WithMany()
            .HasForeignKey(a => a.ReplacedByAllocationId)
            .OnDelete(DeleteBehavior.Restrict);

        // Null-safe FK: optional staff member who manually created this allocation.
        builder.HasOne<Entities.Common.StaffMember>()
            .WithMany()
            .HasForeignKey(a => a.CreatedByStaffId)
            .OnDelete(DeleteBehavior.Restrict);

        // Partial unique: only one confirmed allocation per staff per shift.
        // Released/cancelled rows can coexist so the audit trail is preserved.
        builder.HasIndex(a => new { a.ShiftId, a.StaffMemberId })
            .HasDatabaseName(ConfirmedUniqueIndex)
            .IsUnique()
            .HasFilter("status = 'confirmed'");

        builder.HasIndex(a => a.ShiftId)
            .HasDatabaseName("ix_allocations_shift_id");

        builder.HasIndex(a => a.StaffMemberId)
            .HasDatabaseName("ix_allocations_staff_id");
    }
}
