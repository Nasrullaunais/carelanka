using CareLanka.Api.Common.Persistence;
using CareLanka.Api.Data.Entities.Common;
using CareLanka.Api.Data.Entities.Equipment;
using CareLanka.Api.Data.Entities.Patient;
using CareLanka.Api.Data.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CareLanka.Api.Data.Configurations.Common;

public class AgentProposedChangeConfiguration : IEntityTypeConfiguration<AgentProposedChange>
{
    public void Configure(EntityTypeBuilder<AgentProposedChange> builder)
    {
        builder.ToTable("agent_proposed_changes", t =>
        {
            t.HasCheckConstraint("ck_agent_proposed_changes_type", EnumWire.CheckConstraint<ProposedChangeType>("change_type"));
            t.HasCheckConstraint("ck_agent_proposed_changes_validation",
                EnumWire.CheckConstraint<ProposedChangeValidationStatus>("validation_status"));
            t.HasCheckConstraint("ck_agent_proposed_changes_sequence", "sequence >= 1");
        });

        builder.HasKey(c => c.Id);

        builder.Property(c => c.ChangeType)
            .HasConversion(new SnakeCaseEnumConverter<ProposedChangeType>())
            .HasMaxLength(40)
            .IsRequired();

        builder.Property(c => c.ValidationStatus)
            .HasConversion(new SnakeCaseEnumConverter<ProposedChangeValidationStatus>())
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(c => c.TargetEntityType).HasMaxLength(50);
        builder.Property(c => c.ValidationMessage).HasMaxLength(500);
        builder.Property(c => c.Payload).HasColumnType("jsonb").HasDefaultValueSql("'{}'::jsonb").IsRequired();

        builder.HasOne(c => c.AgentWorkflow)
            .WithMany(w => w.ProposedChanges)
            .HasForeignKey(c => c.AgentWorkflowId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<StaffMember>().WithMany().HasForeignKey(c => c.ProposedStaffMemberId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Bed>().WithMany().HasForeignKey(c => c.ProposedBedId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Ward>().WithMany().HasForeignKey(c => c.ProposedWardId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(c => new { c.AgentWorkflowId, c.Sequence })
            .HasDatabaseName("ux_agent_proposed_changes_sequence")
            .IsUnique();
    }
}
