using CareLanka.Api.Common.Persistence;
using CareLanka.Api.Data.Entities.Emergency;
using CareLanka.Api.Data.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CareLanka.Api.Data.Configurations.Emergency;

public sealed class DispatchProposalConfiguration : IEntityTypeConfiguration<DispatchProposal>
{
    public const string OpenPerCallUniqueIndex = "ux_dispatch_proposals_open_per_call";

    public void Configure(EntityTypeBuilder<DispatchProposal> builder)
    {
        builder.ToTable("dispatch_proposals", table =>
        {
            table.HasCheckConstraint(
                "ck_dispatch_proposals_status", EnumWire.CheckConstraint<DispatchProposalStatus>("status"));
            table.HasCheckConstraint(
                "ck_dispatch_proposals_priority", EnumWire.CheckConstraint<CallPriority>("call_priority"));
            table.HasCheckConstraint(
                "ck_dispatch_proposals_recommendation_source",
                EnumWire.CheckConstraint<DispatchRecommendationSource>("recommendation_source"));
            table.HasCheckConstraint(
                "ck_dispatch_proposals_withdrawal_reason",
                EnumWire.CheckConstraint<DispatchWithdrawalReason>("withdrawal_reason"));
        });

        builder.HasKey(proposal => proposal.Id);
        builder.Property(proposal => proposal.Status)
            .HasConversion(new SnakeCaseEnumConverter<DispatchProposalStatus>())
            .HasMaxLength(30)
            .IsRequired();
        builder.Property(proposal => proposal.CallPriority)
            .HasConversion(new SnakeCaseEnumConverter<CallPriority>())
            .HasMaxLength(20)
            .IsRequired();
        builder.Property(proposal => proposal.Outcome)
            .HasConversion(new SnakeCaseEnumConverter<DispatchOutcome>())
            .HasMaxLength(30);
        builder.Property(proposal => proposal.SourceCallPriority)
            .HasConversion(new SnakeCaseEnumConverter<CallPriority>())
            .HasMaxLength(20);
        builder.Property(proposal => proposal.SourceDispatchStatus)
            .HasConversion(new SnakeCaseEnumConverter<DispatchStatus>())
            .HasMaxLength(30);
        builder.Property(proposal => proposal.RejectionReason)
            .HasConversion(new SnakeCaseEnumConverter<DispatchRejectionReason>())
            .HasMaxLength(40);
        builder.Property(proposal => proposal.WithdrawalReason)
            .HasConversion(new SnakeCaseEnumConverter<DispatchWithdrawalReason>())
            .HasMaxLength(30);
        builder.Property(proposal => proposal.Version).IsRowVersion();
        builder.Property(proposal => proposal.RecommendationSource)
            .HasConversion(new SnakeCaseEnumConverter<DispatchRecommendationSource>())
            .HasMaxLength(30);
        builder.Property(proposal => proposal.Rationale).HasMaxLength(1000);
        builder.Property(proposal => proposal.RecommendationNote).HasMaxLength(500);
        builder.Property(proposal => proposal.SourceCallAddressLabel).HasMaxLength(300);
        builder.Property(proposal => proposal.ReviewNotes).HasMaxLength(500);

        builder.HasOne(proposal => proposal.EmergencyCall)
            .WithMany()
            .HasForeignKey(proposal => proposal.EmergencyCallId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(proposal => proposal.EmergencyCallId);
        builder.HasIndex(proposal => proposal.Status);
        builder.HasIndex(proposal => proposal.WorkflowId).IsUnique();
        builder.HasIndex(proposal => proposal.EmergencyCallId)
            .HasDatabaseName(OpenPerCallUniqueIndex)
            .IsUnique()
            .HasFilter("status IN ('pending', 'pending_confirmation', 'pending_approval')");
    }
}
