using CareLanka.Api.Common.Persistence;
using CareLanka.Api.Data.Entities.Common;
using CareLanka.Api.Data.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CareLanka.Api.Data.Configurations.Common;

public class AgentWorkflowConfiguration : IEntityTypeConfiguration<AgentWorkflow>
{
    public void Configure(EntityTypeBuilder<AgentWorkflow> builder)
    {
        builder.ToTable("agent_workflows", t =>
        {
            t.HasCheckConstraint("ck_agent_workflows_agent_type", EnumWire.CheckConstraint<AgentType>("agent_type"));
            t.HasCheckConstraint("ck_agent_workflows_status", EnumWire.CheckConstraint<AgentWorkflowStatus>("status"));
            t.HasCheckConstraint("ck_agent_workflows_attempts", "attempt_count >= 0");
        });

        builder.HasKey(w => w.Id);

        builder.Property(w => w.AgentType)
            .HasConversion(new SnakeCaseEnumConverter<AgentType>())
            .HasMaxLength(40)
            .IsRequired();

        builder.Property(w => w.Status)
            .HasConversion(new SnakeCaseEnumConverter<AgentWorkflowStatus>())
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(w => w.RequiredApproverRole)
            .HasConversion(new SnakeCaseEnumConverter<StaffRole>())
            .HasMaxLength(30);

        builder.Property(w => w.EntityType).HasMaxLength(50).IsRequired();
        builder.Property(w => w.Objective).HasMaxLength(500).IsRequired();
        builder.Property(w => w.ReviewNotes).HasMaxLength(500);
        builder.Property(w => w.FinalOutcome).HasMaxLength(200);

        builder.Property(w => w.Plan).HasColumnType("jsonb").HasDefaultValueSql("'[]'::jsonb").IsRequired();
        builder.Property(w => w.CompletedSteps).HasColumnType("jsonb").HasDefaultValueSql("'[]'::jsonb").IsRequired();
        builder.Property(w => w.ToolResults).HasColumnType("jsonb").HasDefaultValueSql("'[]'::jsonb").IsRequired();
        builder.Property(w => w.ValidationResults).HasColumnType("jsonb");
        builder.Property(w => w.Errors).HasColumnType("jsonb");

        builder.HasOne(w => w.ParentWorkflow)
            .WithMany()
            .HasForeignKey(w => w.ParentWorkflowId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(w => w.ReviewedByStaffMember)
            .WithMany()
            .HasForeignKey(w => w.ReviewedByStaffMemberId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(w => w.CorrelationId).HasDatabaseName("ix_agent_workflows_correlation");
        builder.HasIndex(w => new { w.EntityType, w.EntityId }).HasDatabaseName("ix_agent_workflows_entity");
        builder.HasIndex(w => new { w.AgentType, w.Status }).HasDatabaseName("ix_agent_workflows_agent_status");
    }
}
