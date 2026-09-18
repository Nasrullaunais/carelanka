using System.Text.Json;
using CareLanka.Api.Common.Persistence;
using CareLanka.Api.Data.Entities.Common;
using CareLanka.Api.Data.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace CareLanka.Api.Data.Configurations.Common;

/// <summary>
/// STUB - see STUBS.md. Group-owned table (ADR 3), built here only so the bed agent can persist
/// its run. Delete this file and the entity when the common track builds the real pair.
/// </summary>
public class AgentWorkflowConfiguration : IEntityTypeConfiguration<AgentWorkflow>
{
    public const string CorrelationIndex = "ix_agent_workflows_correlation_id";
    public const string EntityIndex = "ix_agent_workflows_entity";

    /// <summary>
    /// Enum-insensitive on the way in, snake_case on the way out - the same vocabulary the wire
    /// and the enum columns use, so a jsonb payload reads like the rest of the row in psql.
    /// </summary>
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    public void Configure(EntityTypeBuilder<AgentWorkflow> builder)
    {
        builder.ToTable("agent_workflows", table =>
        {
            table.HasCheckConstraint(
                "ck_agent_workflows_agent_type", EnumWire.CheckConstraint<AgentType>("agent_type"));
            table.HasCheckConstraint(
                "ck_agent_workflows_status", EnumWire.CheckConstraint<AgentWorkflowStatus>("status"));
            table.HasCheckConstraint(
                "ck_agent_workflows_objective",
                EnumWire.CheckConstraint<WorkflowObjective>("objective"));
            table.HasCheckConstraint(
                "ck_agent_workflows_required_approver_role",
                "required_approver_role IS NULL OR "
                + EnumWire.CheckConstraint<StaffRole>("required_approver_role"));
        });

        builder.HasKey(workflow => workflow.Id);

        builder.Property(workflow => workflow.AgentType)
            .HasConversion(new SnakeCaseEnumConverter<AgentType>())
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(workflow => workflow.Status)
            .HasConversion(new SnakeCaseEnumConverter<AgentWorkflowStatus>())
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(workflow => workflow.Objective)
            .HasConversion(new SnakeCaseEnumConverter<WorkflowObjective>())
            .HasMaxLength(40)
            .IsRequired();

        builder.Property(workflow => workflow.RequiredApproverRole)
            .HasConversion(new SnakeCaseEnumConverter<StaffRole>())
            .HasMaxLength(30);

        builder.Property(workflow => workflow.EntityType).HasMaxLength(64).IsRequired();

        builder.Property(workflow => workflow.FinalOutcome).HasMaxLength(64);

        builder.Property(workflow => workflow.ReviewNotes).HasMaxLength(1000);

        builder.Property(workflow => workflow.AttemptCount).HasDefaultValue(0);

        JsonList(builder.Property(workflow => workflow.Plan));
        JsonList(builder.Property(workflow => workflow.CompletedSteps));
        JsonList(builder.Property(workflow => workflow.ToolResults));
        JsonList(builder.Property(workflow => workflow.ValidationResults));
        JsonList(builder.Property(workflow => workflow.Errors));

        builder.HasOne<AgentWorkflow>()
            .WithMany()
            .HasForeignKey(workflow => workflow.ParentWorkflowId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<StaffMember>()
            .WithMany()
            .HasForeignKey(workflow => workflow.ReviewedByStaffMemberId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(workflow => workflow.CorrelationId).HasDatabaseName(CorrelationIndex);

        builder.HasIndex(workflow => new { workflow.EntityType, workflow.EntityId })
            .HasDatabaseName(EntityIndex);
    }

    /// <summary>
    /// A jsonb column over a list. The comparer is not optional: without one EF compares the list
    /// by reference, so mutating it in place and calling SaveChanges writes nothing at all.
    /// </summary>
    private static void JsonList<T>(PropertyBuilder<List<T>> property)
        => property
            .HasColumnType("jsonb")
            .HasDefaultValueSql("'[]'::jsonb")
            .IsRequired()
            .HasConversion(
                new ValueConverter<List<T>, string>(
                    value => JsonSerializer.Serialize(value, Json),
                    text => JsonSerializer.Deserialize<List<T>>(text, Json) ?? new List<T>()),
                new ValueComparer<List<T>>(
                    (left, right) => JsonSerializer.Serialize(left, Json)
                        == JsonSerializer.Serialize(right, Json),
                    value => JsonSerializer.Serialize(value, Json).GetHashCode(),
                    value => JsonSerializer.Deserialize<List<T>>(
                        JsonSerializer.Serialize(value, Json), Json)!));
}
