using CareLanka.Api.Data.Entities.Common;
using CareLanka.Api.Data.Enums;
using CareLanka.Api.DTOs.Staff;

namespace CareLanka.Api.Agents.Staff;

public sealed record StaffAllocationAgentRequest(
    Guid ShiftId,
    string? Objective = null,
    bool AllowCascadingSwap = true,
    IReadOnlyList<Guid>? ExcludeStaffIds = null,
    IReadOnlyList<Guid>? ExcludeWardIds = null,
    Guid? WorkflowId = null,
    Guid? ParentWorkflowId = null,
    Guid? CorrelationId = null,
    Guid? InitiatedByStaffId = null);

public sealed record StaffAllocationAgentRun(
    IReadOnlyList<PlanStepDto> Plan,
    IReadOnlyList<ToolCallDto> ToolCalls,
    IReadOnlyList<RosterProposedChangeDto> ProposedChanges,
    IReadOnlyList<RosterValidationResult> Validation,
    IReadOnlyList<RosterProposalErrorDto> Errors,
    AgentOutcome Outcome,
    AgentWorkflowStatus Status,
    bool IsCascadingSwap,
    string? Rationale,
    Guid TargetShiftId,
    string TargetWardName,
    DateOnly ShiftDate,
    Guid? ProposedStaffMemberId,
    string? ProposedStaffName,
    Guid? DonorAllocationId,
    Guid? DonorShiftId,
    string? DonorWardName,
    AgentWorkflow Workflow,
    IReadOnlyList<AgentProposedChange> ProposedChangeEntities);

/// <summary>
/// Staff Allocation Agent contract. Evaluates staffing deficits, searches for free
/// qualified staff and cross-ward swaps, and drafts proposal workflows for human review.
/// </summary>
public interface IStaffAllocationAgent
{
    Task<StaffAllocationAgentRun> RunAsync(
        StaffAllocationAgentRequest request,
        CancellationToken cancellationToken = default);
}
