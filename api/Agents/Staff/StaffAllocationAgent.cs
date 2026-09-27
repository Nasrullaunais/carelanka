using System.Diagnostics;
using System.Text.Json;
using CareLanka.Api.Common.Persistence;
using CareLanka.Api.Data.Entities.Common;
using CareLanka.Api.Data.Enums;
using CareLanka.Api.DTOs.Staff;

namespace CareLanka.Api.Agents.Staff;

/// <summary>
/// Staff Allocation Agent / Solver.
/// Evaluates understaffed shifts, searches for free qualified staff, evaluates
/// cascading cross-ward swaps from surplus wards, and executes deterministic validation.
/// Strict safety rule: writes zero allocations to database; pauses at human approval gate.
/// </summary>
public sealed class StaffAllocationAgent : IStaffAllocationAgent
{
    private const string ReadRequirements = "read_shift_requirements";
    private const string FindFreeStaff = "find_free_qualified_staff";
    private const string EvaluateCrossWardSwaps = "evaluate_cross_ward_swaps";
    private const string ValidateConstraints = "validate_constraints";
    private const string PauseForApproval = "pause_for_human_approval";

    private readonly IStaffAllocationAgentTools _tools;
    private readonly RosterProposalValidator _validator;
    private readonly TimeProvider _clock;

    public StaffAllocationAgent(
        IStaffAllocationAgentTools tools,
        RosterProposalValidator validator,
        TimeProvider? clock = null)
    {
        _tools = tools;
        _validator = validator;
        _clock = clock ?? TimeProvider.System;
    }

    public async Task<StaffAllocationAgentRun> RunAsync(
        StaffAllocationAgentRequest request,
        CancellationToken cancellationToken = default)
    {
        var startedAt = _clock.GetUtcNow();
        var workflowId = request.WorkflowId ?? Guid.NewGuid();

        var plan = new List<PlanStepDto>
        {
            new() { Sequence = 1, AgentRole = "shift_analysis", Description = "Read understaffed shift requirements and staffing deficit", Status = "pending" },
            new() { Sequence = 2, AgentRole = "candidate_search", Description = "Query active qualified staff and filter by availability and leave", Status = "pending" },
            new() { Sequence = 3, AgentRole = "cascading_solver", Description = "Inspect surplus wards for cascading cross-ward swap candidates", Status = "pending" },
            new() { Sequence = 4, AgentRole = "constraint_validator", Description = "Execute deterministic C# validation of constraints and minimums", Status = "pending" },
            new() { Sequence = 5, AgentRole = "human_gate", Description = "Prepare proposal for human administrator approval", Status = "pending" }
        };

        var toolCalls = new List<ToolCallDto>();
        var errors = new List<RosterProposalErrorDto>();
        var proposedChanges = new List<RosterProposedChangeDto>();
        var changeEntities = new List<AgentProposedChange>();
        var validationResults = new List<RosterValidationResult>();

        try
        {
            // Step 1: read_shift_requirements
            plan[0].Status = "running";
            plan[0].StartedAt = _clock.GetUtcNow();

            var shiftInfo = await CallToolAsync(
                toolCalls,
                "list_shift_requirements",
                new { shift_id = request.ShiftId },
                () => _tools.GetUnderstaffedShiftAsync(request.ShiftId, cancellationToken));

            if (shiftInfo is null)
            {
                plan[0].Status = "failed";
                plan[0].CompletedAt = _clock.GetUtcNow();
                for (var i = 1; i < plan.Count; i++)
                {
                    plan[i].Status = "skipped";
                }

                errors.Add(new RosterProposalErrorDto
                {
                    Step = ReadRequirements,
                    Message = $"Shift with ID '{request.ShiftId}' was not found.",
                    OccurredAt = _clock.GetUtcNow()
                });

                return BuildRun(
                    workflowId, request, null, "Unknown Ward", DateOnly.FromDateTime(startedAt.UtcDateTime),
                    plan, toolCalls, proposedChanges, validationResults, errors,
                    AgentOutcome.Failed, AgentWorkflowStatus.Failed, false,
                    "Shift not found.", null, null, null, null, null,
                    changeEntities, startedAt);
            }

            plan[0].Status = "completed";
            plan[0].CompletedAt = _clock.GetUtcNow();

            // Step 2: find_free_qualified_staff
            plan[1].Status = "running";
            plan[1].StartedAt = _clock.GetUtcNow();

            var eligibleStaff = await CallToolAsync(
                toolCalls,
                "list_candidate_staff",
                new { shift_id = request.ShiftId, exclude_staff_ids = request.ExcludeStaffIds },
                () => _tools.FindEligibleStaffAsync(request.ShiftId, request.ExcludeStaffIds, cancellationToken));

            var freeStaff = await CallToolAsync(
                toolCalls,
                "list_approved_leave",
                new { candidate_count = eligibleStaff.Count, shift_id = request.ShiftId },
                () => _tools.FindFreeStaffAsync(eligibleStaff, shiftInfo.Shift, cancellationToken));

            plan[1].Status = "completed";
            plan[1].CompletedAt = _clock.GetUtcNow();

            // Branch A: Free qualified staff member exists
            if (freeStaff.Count > 0)
            {
                var bestFree = freeStaff[0];

                // Step 3 is skipped since a completely free staff member is found
                plan[2].Status = "skipped";
                plan[2].StartedAt = _clock.GetUtcNow();
                plan[2].CompletedAt = _clock.GetUtcNow();

                // Step 4: validate_constraints
                plan[3].Status = "running";
                plan[3].StartedAt = _clock.GetUtcNow();

                validationResults = _validator
                    .ValidateDirectAllocation(bestFree, shiftInfo.Shift, shiftInfo.ConfirmedCount)
                    .ToList();

                var allPassed = validationResults.All(v => v.Passed);

                if (allPassed)
                {
                    plan[3].Status = "completed";
                    plan[3].CompletedAt = _clock.GetUtcNow();

                    // Step 5: pause_for_human_approval
                    plan[4].Status = "completed";
                    plan[4].StartedAt = _clock.GetUtcNow();
                    plan[4].CompletedAt = _clock.GetUtcNow();

                    var rationale = $"{bestFree.FullName} is free, holds required {shiftInfo.Shift.RequiredRole} qualifications, and can directly cover {shiftInfo.WardName}.";

                    var changeId = Guid.NewGuid();
                    var payload = RosterWorkflowJson.Write(new
                    {
                        shift_id = shiftInfo.Shift.Id,
                        staff_member_id = bestFree.Id,
                        staff_name = bestFree.FullName,
                        ward_name = shiftInfo.WardName,
                        created_by_staff_id = request.InitiatedByStaffId,
                        rationale
                    });

                    proposedChanges.Add(new RosterProposedChangeDto
                    {
                        Id = changeId,
                        Sequence = 1,
                        ChangeType = RosterProposedChangeType.CreateAllocation,
                        ProposedStaffMemberId = bestFree.Id,
                        ProposedStaffName = bestFree.FullName,
                        ProposedShiftId = shiftInfo.Shift.Id,
                        ToWardName = shiftInfo.WardName,
                        Rationale = rationale,
                        ValidationStatus = ProposedChangeValidationStatus.Passed,
                        ValidationMessage = "Deterministic validation passed."
                    });

                    changeEntities.Add(new AgentProposedChange
                    {
                        Id = changeId,
                        AgentWorkflowId = workflowId,
                        Sequence = 1,
                        ChangeType = ProposedChangeType.CreateAllocation,
                        TargetEntityType = "Shift",
                        TargetEntityId = shiftInfo.Shift.Id,
                        ProposedStaffMemberId = bestFree.Id,
                        ProposedWardId = shiftInfo.Shift.WardId,
                        Payload = payload,
                        ValidationStatus = ProposedChangeValidationStatus.Passed,
                        ValidationMessage = "Deterministic validation passed."
                    });

                    return BuildRun(
                        workflowId, request, shiftInfo.Shift.Id, shiftInfo.WardName, shiftInfo.Shift.Date,
                        plan, toolCalls, proposedChanges, validationResults, errors,
                        AgentOutcome.FreeStaffProposed, AgentWorkflowStatus.PendingApproval, false,
                        rationale, bestFree.Id, bestFree.FullName, null, null, null,
                        changeEntities, startedAt);
                }

                plan[3].Status = "failed";
                plan[3].CompletedAt = _clock.GetUtcNow();
                plan[4].Status = "skipped";

                return BuildRun(
                    workflowId, request, shiftInfo.Shift.Id, shiftInfo.WardName, shiftInfo.Shift.Date,
                    plan, toolCalls, proposedChanges, validationResults, errors,
                    AgentOutcome.Failed, AgentWorkflowStatus.Failed, false,
                    "Deterministic validation failed for direct candidate.", bestFree.Id, bestFree.FullName, null, null, null,
                    changeEntities, startedAt);
            }

            // Branch B: No free staff found -> Step 3: evaluate_cross_ward_swaps
            plan[2].Status = "running";
            plan[2].StartedAt = _clock.GetUtcNow();

            if (!request.AllowCascadingSwap)
            {
                plan[2].Status = "skipped";
                plan[2].CompletedAt = _clock.GetUtcNow();
                plan[3].Status = "skipped";
                plan[4].Status = "skipped";

                var noSwapRationale = "No free qualified staff available, and cascading cross-ward swaps were not permitted for this request.";

                return BuildRun(
                    workflowId, request, shiftInfo.Shift.Id, shiftInfo.WardName, shiftInfo.Shift.Date,
                    plan, toolCalls, proposedChanges, validationResults, errors,
                    AgentOutcome.NoCandidateFound, AgentWorkflowStatus.Failed, false,
                    noSwapRationale, null, null, null, null, null,
                    changeEntities, startedAt);
            }

            var swapCandidates = await CallToolAsync(
                toolCalls,
                "get_ward_coverage",
                new { target_shift_id = request.ShiftId, exclude_ward_ids = request.ExcludeWardIds },
                () => _tools.FindCascadingSwapCandidatesAsync(shiftInfo.Shift, request.ExcludeStaffIds, request.ExcludeWardIds, cancellationToken));

            if (swapCandidates.Count > 0)
            {
                var bestSwap = swapCandidates[0];

                plan[2].Status = "completed";
                plan[2].CompletedAt = _clock.GetUtcNow();

                // Step 4: validate_constraints
                plan[3].Status = "running";
                plan[3].StartedAt = _clock.GetUtcNow();

                validationResults = _validator
                    .ValidateCascadingSwap(bestSwap, shiftInfo.Shift, shiftInfo.ConfirmedCount)
                    .ToList();

                var allPassed = validationResults.All(v => v.Passed);

                if (allPassed)
                {
                    plan[3].Status = "completed";
                    plan[3].CompletedAt = _clock.GetUtcNow();

                    // Step 5: pause_for_human_approval
                    plan[4].Status = "completed";
                    plan[4].StartedAt = _clock.GetUtcNow();
                    plan[4].CompletedAt = _clock.GetUtcNow();

                    var rationale = $"Cascading swap proposed: Reassign {bestSwap.Staff.FullName} from {bestSwap.SourceWardName} (surplus: {bestSwap.SurplusCount} above minimum) to {shiftInfo.WardName}.";

                    var endChangeId = Guid.NewGuid();
                    var endPayload = RosterWorkflowJson.Write(new
                    {
                        allocation_id = bestSwap.SourceAllocation.Id,
                        staff_member_id = bestSwap.Staff.Id,
                        staff_name = bestSwap.Staff.FullName,
                        from_ward = bestSwap.SourceWardName,
                        is_cascading_swap = true,
                        rationale
                    });

                    proposedChanges.Add(new RosterProposedChangeDto
                    {
                        Id = endChangeId,
                        Sequence = 1,
                        ChangeType = RosterProposedChangeType.EndAllocation,
                        TargetAllocationId = bestSwap.SourceAllocation.Id,
                        ProposedStaffMemberId = bestSwap.Staff.Id,
                        ProposedStaffName = bestSwap.Staff.FullName,
                        FromShiftId = bestSwap.SourceShift.Id,
                        FromWardName = bestSwap.SourceWardName,
                        Rationale = rationale,
                        ValidationStatus = ProposedChangeValidationStatus.Passed,
                        ValidationMessage = "Deterministic validation passed."
                    });

                    changeEntities.Add(new AgentProposedChange
                    {
                        Id = endChangeId,
                        AgentWorkflowId = workflowId,
                        Sequence = 1,
                        ChangeType = ProposedChangeType.EndAllocation,
                        TargetEntityType = "Allocation",
                        TargetEntityId = bestSwap.SourceAllocation.Id,
                        ProposedStaffMemberId = bestSwap.Staff.Id,
                        ProposedWardId = bestSwap.SourceShift.WardId,
                        Payload = endPayload,
                        ValidationStatus = ProposedChangeValidationStatus.Passed,
                        ValidationMessage = "Deterministic validation passed."
                    });

                    var createChangeId = Guid.NewGuid();
                    var createPayload = RosterWorkflowJson.Write(new
                    {
                        shift_id = shiftInfo.Shift.Id,
                        staff_member_id = bestSwap.Staff.Id,
                        staff_name = bestSwap.Staff.FullName,
                        from_ward = bestSwap.SourceWardName,
                        to_ward = shiftInfo.WardName,
                        target_allocation_id = bestSwap.SourceAllocation.Id,
                        is_cascading_swap = true,
                        created_by_staff_id = request.InitiatedByStaffId,
                        rationale
                    });

                    proposedChanges.Add(new RosterProposedChangeDto
                    {
                        Id = createChangeId,
                        Sequence = 2,
                        ChangeType = RosterProposedChangeType.CreateAllocation,
                        TargetAllocationId = bestSwap.SourceAllocation.Id,
                        ProposedStaffMemberId = bestSwap.Staff.Id,
                        ProposedStaffName = bestSwap.Staff.FullName,
                        ProposedShiftId = shiftInfo.Shift.Id,
                        FromWardName = bestSwap.SourceWardName,
                        ToWardName = shiftInfo.WardName,
                        Rationale = rationale,
                        ValidationStatus = ProposedChangeValidationStatus.Passed,
                        ValidationMessage = "Deterministic validation passed."
                    });

                    changeEntities.Add(new AgentProposedChange
                    {
                        Id = createChangeId,
                        AgentWorkflowId = workflowId,
                        Sequence = 2,
                        ChangeType = ProposedChangeType.CreateAllocation,
                        TargetEntityType = "Shift",
                        TargetEntityId = shiftInfo.Shift.Id,
                        ProposedStaffMemberId = bestSwap.Staff.Id,
                        ProposedWardId = shiftInfo.Shift.WardId,
                        Payload = createPayload,
                        ValidationStatus = ProposedChangeValidationStatus.Passed,
                        ValidationMessage = "Deterministic validation passed."
                    });

                    return BuildRun(
                        workflowId, request, shiftInfo.Shift.Id, shiftInfo.WardName, shiftInfo.Shift.Date,
                        plan, toolCalls, proposedChanges, validationResults, errors,
                        AgentOutcome.SwapProposed, AgentWorkflowStatus.PendingApproval, true,
                        rationale, bestSwap.Staff.Id, bestSwap.Staff.FullName,
                        bestSwap.SourceAllocation.Id, bestSwap.SourceShift.Id, bestSwap.SourceWardName,
                        changeEntities, startedAt);
                }

                plan[3].Status = "failed";
                plan[3].CompletedAt = _clock.GetUtcNow();
                plan[4].Status = "skipped";

                return BuildRun(
                    workflowId, request, shiftInfo.Shift.Id, shiftInfo.WardName, shiftInfo.Shift.Date,
                    plan, toolCalls, proposedChanges, validationResults, errors,
                    AgentOutcome.Failed, AgentWorkflowStatus.Failed, true,
                    "Deterministic validation failed for cascading swap candidate.", bestSwap.Staff.Id, bestSwap.Staff.FullName,
                    bestSwap.SourceAllocation.Id, bestSwap.SourceShift.Id, bestSwap.SourceWardName,
                    changeEntities, startedAt);
            }

            // No swap candidates found
            plan[2].Status = "completed";
            plan[2].CompletedAt = _clock.GetUtcNow();
            plan[3].Status = "skipped";
            plan[4].Status = "skipped";

            var noCandidateRationale = "No eligible free staff or cross-ward surplus swap candidates available.";

            return BuildRun(
                workflowId, request, shiftInfo.Shift.Id, shiftInfo.WardName, shiftInfo.Shift.Date,
                plan, toolCalls, proposedChanges, validationResults, errors,
                AgentOutcome.NoCandidateFound, AgentWorkflowStatus.Failed, false,
                noCandidateRationale, null, null, null, null, null,
                changeEntities, startedAt);
        }
        catch (Exception ex)
        {
            errors.Add(new RosterProposalErrorDto
            {
                Step = "agent_execution",
                Message = ex.Message,
                OccurredAt = _clock.GetUtcNow()
            });

            return BuildRun(
                workflowId, request, request.ShiftId, "Unknown Ward", DateOnly.FromDateTime(startedAt.UtcDateTime),
                plan, toolCalls, proposedChanges, validationResults, errors,
                AgentOutcome.Failed, AgentWorkflowStatus.Failed, false,
                ex.Message, null, null, null, null, null,
                changeEntities, startedAt);
        }
    }

    private async Task<TResult> CallToolAsync<TResult>(
        List<ToolCallDto> toolCalls,
        string toolName,
        object arguments,
        Func<Task<TResult>> toolFunc)
    {
        var sw = Stopwatch.StartNew();
        var calledAt = _clock.GetUtcNow();

        try
        {
            var result = await toolFunc();
            sw.Stop();

            toolCalls.Add(new ToolCallDto
            {
                ToolName = toolName,
                Arguments = ToDictionary(arguments),
                Succeeded = true,
                DurationMs = (int)sw.ElapsedMilliseconds,
                CalledAt = calledAt
            });

            return result;
        }
        catch (Exception ex)
        {
            sw.Stop();

            toolCalls.Add(new ToolCallDto
            {
                ToolName = toolName,
                Arguments = ToDictionary(arguments),
                Succeeded = false,
                DurationMs = (int)sw.ElapsedMilliseconds,
                Error = ex.Message,
                CalledAt = calledAt
            });

            throw;
        }
    }

    private static IReadOnlyDictionary<string, object?> ToDictionary(object value)
    {
        if (value is IReadOnlyDictionary<string, object?> dict)
        {
            return dict;
        }

        var json = JsonSerializer.Serialize(value);
        return JsonSerializer.Deserialize<Dictionary<string, object?>>(json) ?? new Dictionary<string, object?>();
    }

    private StaffAllocationAgentRun BuildRun(
        Guid workflowId,
        StaffAllocationAgentRequest request,
        Guid? shiftId,
        string wardName,
        DateOnly shiftDate,
        IReadOnlyList<PlanStepDto> plan,
        IReadOnlyList<ToolCallDto> toolCalls,
        IReadOnlyList<RosterProposedChangeDto> proposedChanges,
        IReadOnlyList<RosterValidationResult> validation,
        IReadOnlyList<RosterProposalErrorDto> errors,
        AgentOutcome outcome,
        AgentWorkflowStatus status,
        bool isCascadingSwap,
        string? rationale,
        Guid? proposedStaffId,
        string? proposedStaffName,
        Guid? donorAllocationId,
        Guid? donorShiftId,
        string? donorWardName,
        List<AgentProposedChange> changeEntities,
        DateTimeOffset startedAt)
    {
        var completedAt = _clock.GetUtcNow();

        var workflow = new AgentWorkflow
        {
            Id = workflowId,
            AgentType = AgentType.StaffAllocation,
            EntityType = "Shift",
            EntityId = shiftId ?? request.ShiftId,
            CorrelationId = request.CorrelationId ?? Guid.NewGuid(),
            ParentWorkflowId = request.ParentWorkflowId,
            Objective = request.Objective ?? $"Restore minimum coverage for {wardName} shift on {shiftDate:yyyy-MM-dd}.",
            Plan = RosterWorkflowJson.Write(plan),
            CompletedSteps = RosterWorkflowJson.Write(plan.Where(p => p.Status == "completed").ToList()),
            ToolResults = RosterWorkflowJson.Write(toolCalls),
            ValidationResults = RosterWorkflowJson.Write(validation),
            Errors = errors.Count > 0 ? RosterWorkflowJson.Write(errors) : null,
            Status = status,
            RequiredApproverRole = StaffRole.HospitalAdministrator,
            StartedAt = startedAt,
            CompletedAt = completedAt,
            AttemptCount = 1,
            FinalOutcome = EnumWire.ToWire(outcome),
            ReviewNotes = rationale,
            ProposedChanges = changeEntities
        };

        return new StaffAllocationAgentRun(
            plan,
            toolCalls,
            proposedChanges,
            validation,
            errors,
            outcome,
            status,
            isCascadingSwap,
            rationale,
            shiftId ?? request.ShiftId,
            wardName,
            shiftDate,
            proposedStaffId,
            proposedStaffName,
            donorAllocationId,
            donorShiftId,
            donorWardName,
            workflow,
            changeEntities);
    }
}
