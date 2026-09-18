using CareLanka.Api.Common.Persistence;
using CareLanka.Api.Data.Entities.Patient;
using CareLanka.Api.Data.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CareLanka.Api.Data.Configurations.Patient;

public class BedSuggestionConfiguration : IEntityTypeConfiguration<BedSuggestion>
{
    public const string WorkflowUniqueIndex = "ux_bed_suggestions_workflow_id";

    public void Configure(EntityTypeBuilder<BedSuggestion> builder)
    {
        builder.ToTable("bed_suggestions", table =>
        {
            table.HasCheckConstraint(
                "ck_bed_suggestions_outcome",
                "outcome IS NULL OR " + EnumWire.CheckConstraint<BedAgentOutcome>("outcome"));
            table.HasCheckConstraint(
                "ck_bed_suggestions_blocker_code",
                "blocker_code IS NULL OR "
                + EnumWire.CheckConstraint<BedSuggestionBlockerCode>("blocker_code"));
        });

        builder.HasKey(suggestion => suggestion.Id);

        builder.Property(suggestion => suggestion.Outcome)
            .HasConversion(new SnakeCaseEnumConverter<BedAgentOutcome>())
            .HasMaxLength(30);

        builder.Property(suggestion => suggestion.RequestedIdentifier).HasMaxLength(32);

        builder.Property(suggestion => suggestion.BlockerCode)
            .HasConversion(new SnakeCaseEnumConverter<BedSuggestionBlockerCode>())
            .HasMaxLength(30);

        builder.Property(suggestion => suggestion.BlockerMessage).HasMaxLength(500);

        builder.Property(suggestion => suggestion.ValidationPassed).HasDefaultValue(true);

        builder.HasOne<Entities.Common.AgentWorkflow>()
            .WithMany()
            .HasForeignKey(suggestion => suggestion.WorkflowId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(suggestion => suggestion.Patient)
            .WithMany()
            .HasForeignKey(suggestion => suggestion.PatientId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(suggestion => suggestion.Admission)
            .WithMany()
            .HasForeignKey(suggestion => suggestion.AdmissionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(suggestion => suggestion.WorkflowId)
            .HasDatabaseName(WorkflowUniqueIndex)
            .IsUnique();

        // No query filter on Patient.IsActive, unlike BedAssignment: a run that matched nobody has
        // no patient at all, and a filter dereferencing a null navigation hides every one of them.
    }
}
