using CareLanka.Api.Common.Persistence;
using CareLanka.Api.Data.Entities.Patient;
using CareLanka.Api.Data.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CareLanka.Api.Data.Configurations.Patient;

public class BedAssignmentConfiguration : IEntityTypeConfiguration<BedAssignment>
{
    public const string LiveBedUniqueIndex = "ux_bed_assignments_live_bed";
    public const string LiveAdmissionUniqueIndex = "ux_bed_assignments_live_admission";

    private const string LiveFilter = "status IN ('reserved', 'occupied')";

    public void Configure(EntityTypeBuilder<BedAssignment> builder)
    {
        builder.ToTable("bed_assignments", t =>
        {
            t.HasCheckConstraint(
                "ck_bed_assignments_status", EnumWire.CheckConstraint<AssignmentStatus>("status"));
            t.HasCheckConstraint(
                "ck_bed_assignments_assigned_by",
                EnumWire.CheckConstraint<AssignedBy>("assigned_by"));
            t.HasCheckConstraint(
                "ck_bed_assignments_release_reason",
                $"release_reason IS NULL OR {EnumWire.CheckConstraint<ReleaseReason>("release_reason")}");
        });

        builder.HasKey(b => b.Id);

        builder.Property(b => b.Status)
            .HasConversion(new SnakeCaseEnumConverter<AssignmentStatus>())
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(b => b.AssignedBy)
            .HasConversion(new SnakeCaseEnumConverter<AssignedBy>())
            .HasMaxLength(10)
            .IsRequired();

        builder.Property(b => b.ReleaseReason)
            .HasConversion(new SnakeCaseEnumConverter<ReleaseReason>())
            .HasMaxLength(20);

        builder.Property(b => b.IsDowngrade).HasDefaultValue(false);

        builder.Property(b => b.OverrideReason).HasMaxLength(500);

        builder.HasOne(b => b.Admission)
            .WithMany(a => a.BedAssignments)
            .HasForeignKey(b => b.AdmissionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Entities.Common.StaffMember>()
            .WithMany()
            .HasForeignKey(b => b.ApprovedByStaffMemberId)
            .OnDelete(DeleteBehavior.Restrict);

        // The column has existed since step 2 with nothing behind it. Now that agent_workflows
        // exists the key is real, so a run cannot be deleted out from under the bed it suggested.
        builder.HasOne<Entities.Common.AgentWorkflow>()
            .WithMany()
            .HasForeignKey(b => b.WorkflowId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(b => b.BedId)
            .HasDatabaseName(LiveBedUniqueIndex)
            .IsUnique()
            .HasFilter(LiveFilter);

        builder.HasIndex(b => b.AdmissionId)
            .HasDatabaseName(LiveAdmissionUniqueIndex)
            .IsUnique()
            .HasFilter(LiveFilter);

        builder.HasIndex(b => b.ReservedUntil)
            .HasDatabaseName("ix_bed_assignments_reserved_until")
            .HasFilter("status = 'reserved'");

        builder.HasQueryFilter(b => b.Admission.Patient.IsActive);
    }
}
