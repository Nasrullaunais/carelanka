using CareLanka.Api.Data.Entities.Common;
using CareLanka.Api.Data.Entities.Emergency;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CareLanka.Api.Data.Configurations.Emergency;

public sealed class AmbulanceCrewAssignmentConfiguration
    : IEntityTypeConfiguration<AmbulanceCrewAssignment>
{
    public const string CurrentStaffUniqueIndex = "ux_ambulance_crew_assignments_current_staff";
    public const string CurrentAmbulanceStaffUniqueIndex =
        "ux_ambulance_crew_assignments_current_ambulance_staff";

    public void Configure(EntityTypeBuilder<AmbulanceCrewAssignment> builder)
    {
        builder.ToTable("ambulance_crew_assignments");
        builder.HasKey(assignment => assignment.Id);
        builder.Property(assignment => assignment.AssignedAt).IsRequired();

        builder.HasOne(assignment => assignment.Ambulance)
            .WithMany(ambulance => ambulance.CrewAssignments)
            .HasForeignKey(assignment => assignment.AmbulanceId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<StaffMember>()
            .WithMany()
            .HasForeignKey(assignment => assignment.StaffMemberId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<StaffMember>()
            .WithMany()
            .HasForeignKey(assignment => assignment.AssignedByStaffId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<StaffMember>()
            .WithMany()
            .HasForeignKey(assignment => assignment.UnassignedByStaffId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(assignment => assignment.StaffMemberId)
            .HasDatabaseName(CurrentStaffUniqueIndex)
            .IsUnique()
            .HasFilter("unassigned_at IS NULL");
        builder.HasIndex(assignment => new { assignment.AmbulanceId, assignment.StaffMemberId })
            .HasDatabaseName(CurrentAmbulanceStaffUniqueIndex)
            .IsUnique()
            .HasFilter("unassigned_at IS NULL");
    }
}
