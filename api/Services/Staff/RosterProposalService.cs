using System.Text.Json;
using CareLanka.Api.Agents.Staff;
using CareLanka.Api.Common.Errors;
using CareLanka.Api.Common.Exceptions;
using CareLanka.Api.Common.Persistence;
using CareLanka.Api.Data;
using CareLanka.Api.Data.Entities.Common;
using CareLanka.Api.Data.Entities.Staff;
using CareLanka.Api.Data.Enums;
using CareLanka.Api.DTOs.Common;
using CareLanka.Api.DTOs.Staff;
using CareLanka.Api.Services.Common;
using Microsoft.EntityFrameworkCore;

namespace CareLanka.Api.Services.Staff;

/// <summary>
/// Service managing the lifecycle of Staff Roster Proposals: creation, query listing,
/// detail retrieval, transactional approval with pre-commit re-validation, rejection,
/// and replanning with revised constraints.
/// </summary>
public sealed class RosterProposalService : IRosterProposalService
{
    private readonly CareLankaDbContext _db;
    private readonly IStaffAllocationAgent _agent;
    private readonly ICurrentUser _currentUser;
    private readonly TimeProvider _clock;

    public RosterProposalService(
        CareLankaDbContext db,
        IStaffAllocationAgent agent,
        ICurrentUser currentUser,
        TimeProvider? clock = null)
    {
        _db = db;
        _agent = agent;
        _currentUser = currentUser;
        _clock = clock ?? TimeProvider.System;
    }

    public async Task<PagedResult<RosterProposalSummary>> ListProposalsAsync(
        ListRosterProposalsQueryParameters parameters,
        CancellationToken cancellationToken = default)
    {
        var query = _db.AgentWorkflows
            .AsNoTracking()
            .Include(w => w.ProposedChanges)
            .Where(w => w.AgentType == AgentType.StaffAllocation);

        if (parameters.Status.HasValue)
        {
            var targetWorkflowStatus = parameters.Status.Value switch
            {
                RosterProposalStatus.Pending => AgentWorkflowStatus.Pending,
                RosterProposalStatus.PendingApproval => AgentWorkflowStatus.PendingApproval,
                RosterProposalStatus.Approved => AgentWorkflowStatus.Approved,
                RosterProposalStatus.Executed => AgentWorkflowStatus.Executed,
                RosterProposalStatus.Rejected => AgentWorkflowStatus.Rejected,
                RosterProposalStatus.RevisionRequested => AgentWorkflowStatus.RevisionRequested,
                RosterProposalStatus.Failed => AgentWorkflowStatus.Failed,
                _ => AgentWorkflowStatus.Pending
            };

            query = query.Where(w => w.Status == targetWorkflowStatus);
        }

        if (parameters.ShiftId.HasValue)
        {
            query = query.Where(w => w.EntityId == parameters.ShiftId.Value);
        }

        if (parameters.WardId.HasValue)
        {
            var shiftIdsForWard = _db.Shifts
                .Where(s => s.WardId == parameters.WardId.Value)
                .Select(s => s.Id);

            query = query.Where(w => shiftIdsForWard.Contains(w.EntityId));
        }

        var totalItems = await query.CountAsync(cancellationToken);
        var page = Math.Max(1, parameters.Page);
        var pageSize = Math.Clamp(parameters.PageSize, 1, 100);

        var workflows = await query
            .OrderByDescending(w => w.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var shiftIds = workflows.Select(w => w.EntityId).Distinct().ToList();
        var shifts = await _db.Shifts.AsNoTracking()
            .Where(s => shiftIds.Contains(s.Id))
            .ToListAsync(cancellationToken);

        var wardIds = shifts.Select(s => s.WardId).Distinct().ToList();
        var wards = await _db.Wards.AsNoTracking()
            .Where(w => wardIds.Contains(w.Id))
            .ToDictionaryAsync(w => w.Id, w => w.Name, cancellationToken);

        var shiftMap = shifts.ToDictionary(s => s.Id);

        var items = workflows.Select(w =>
        {
            shiftMap.TryGetValue(w.EntityId, out var shift);
            var wardName = shift is not null && wards.TryGetValue(shift.WardId, out var wn) ? wn : "Ward";
            return ToSummary(w, shift, wardName);
        }).ToList();

        return PagedResult<RosterProposalSummary>.From(items, page, pageSize, totalItems);
    }

    public async Task<RosterProposalDetail> GetProposalDetailAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var workflow = await _db.AgentWorkflows
            .AsNoTracking()
            .Include(w => w.ProposedChanges)
            .FirstOrDefaultAsync(w => w.Id == id && w.AgentType == AgentType.StaffAllocation, cancellationToken);

        if (workflow is null)
        {
            throw new NotFoundException("RosterProposal", id);
        }

        var shift = await _db.Shifts.AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == workflow.EntityId, cancellationToken);

        var wardName = shift is not null
            ? await _db.Wards.AsNoTracking().Where(w => w.Id == shift.WardId).Select(w => w.Name).FirstOrDefaultAsync(cancellationToken) ?? "Ward"
            : "Ward";

        var staffIds = workflow.ProposedChanges
            .Where(c => c.ProposedStaffMemberId.HasValue)
            .Select(c => c.ProposedStaffMemberId!.Value)
            .Distinct()
            .ToList();

        var staffNames = await _db.StaffMembers.AsNoTracking()
            .Where(s => staffIds.Contains(s.Id))
            .ToDictionaryAsync(s => s.Id, s => s.FullName, cancellationToken);

        var wardIds = workflow.ProposedChanges
            .Where(c => c.ProposedWardId.HasValue)
            .Select(c => c.ProposedWardId!.Value)
            .Distinct()
            .ToList();

        if (shift is not null && !wardIds.Contains(shift.WardId))
        {
            wardIds.Add(shift.WardId);
        }

        var wardNames = await _db.Wards.AsNoTracking()
            .Where(w => wardIds.Contains(w.Id))
            .ToDictionaryAsync(w => w.Id, w => w.Name, cancellationToken);

        var plan = RosterWorkflowJson.Read<List<PlanStepDto>>(workflow.Plan) ?? [];
        var validation = RosterWorkflowJson.Read<List<RosterValidationResult>>(workflow.ValidationResults) ?? [];
        var toolCalls = RosterWorkflowJson.Read<List<ToolCallDto>>(workflow.ToolResults) ?? [];
        var errors = RosterWorkflowJson.Read<List<RosterProposalErrorDto>>(workflow.Errors) ?? [];

        var proposedChanges = workflow.ProposedChanges
            .OrderBy(c => c.Sequence)
            .Select(c => MapProposedChange(c, staffNames, wardNames))
            .ToList();

        var rejectionReason = workflow.Status == AgentWorkflowStatus.Rejected
            ? ParseRejectionReason(workflow.FinalOutcome) ?? ParseRejectionReason(workflow.ReviewNotes)
            : null;

        return new RosterProposalDetail
        {
            Id = workflow.Id,
            WorkflowId = workflow.Id,
            ShiftId = workflow.EntityId,
            WardName = wardName,
            ShiftDate = shift?.Date ?? DateOnly.FromDateTime(workflow.CreatedAt.UtcDateTime),
            Objective = workflow.Objective,
            Status = MapStatus(workflow.Status),
            Outcome = ParseOutcome(workflow.FinalOutcome),
            IsCascadingSwap = CheckIsCascadingSwap(workflow),
            ChangeCount = workflow.ProposedChanges.Count,
            CreatedAt = workflow.CreatedAt,
            Plan = plan,
            ProposedChanges = proposedChanges,
            Validation = validation,
            ToolCalls = toolCalls,
            Errors = errors,
            AttemptCount = workflow.AttemptCount,
            StartedAt = workflow.StartedAt,
            CompletedAt = workflow.CompletedAt,
            ReviewedByStaffId = workflow.ReviewedByStaffMemberId,
            ReviewedAt = workflow.ReviewedAt,
            ReviewNotes = workflow.ReviewNotes,
            RejectionReason = rejectionReason,
            FinalOutcome = workflow.FinalOutcome
        };
    }

    public async Task<RosterProposalSummary> CreateProposalAsync(
        CreateRosterProposalRequest request,
        CancellationToken cancellationToken = default)
    {
        var shift = await _db.Shifts.AsNoTracking()
            .Include(s => s.Allocations)
            .FirstOrDefaultAsync(s => s.Id == request.ShiftId, cancellationToken);

        if (shift is null)
        {
            throw new NotFoundException("Shift", request.ShiftId);
        }

        var hasOpenProposal = await _db.AgentWorkflows.AnyAsync(
            w => w.AgentType == AgentType.StaffAllocation
              && w.EntityId == request.ShiftId
              && (w.Status == AgentWorkflowStatus.Pending || w.Status == AgentWorkflowStatus.PendingApproval),
            cancellationToken);

        if (hasOpenProposal)
        {
            throw new ConflictException(MessageCode.Conflict, "This shift already has an open proposal.");
        }

        var initiatedBy = _currentUser.IsAuthenticated && _currentUser.Id != Guid.Empty
            ? _currentUser.Id
            : (Guid?)null;

        var agentRequest = new StaffAllocationAgentRequest(
            request.ShiftId,
            request.Objective,
            request.AllowCascadingSwap,
            InitiatedByStaffId: initiatedBy);

        var run = await _agent.RunAsync(agentRequest, cancellationToken);

        _db.AgentWorkflows.Add(run.Workflow);
        await _db.SaveChangesAsync(cancellationToken);

        return ToSummary(run.Workflow, shift, run.TargetWardName);
    }

    public async Task<RosterProposalDetail> ApproveProposalAsync(
        Guid id,
        ApproveRosterProposalRequest request,
        CancellationToken cancellationToken = default)
    {
        var workflow = await _db.AgentWorkflows
            .Include(w => w.ProposedChanges)
            .FirstOrDefaultAsync(w => w.Id == id && w.AgentType == AgentType.StaffAllocation, cancellationToken);

        if (workflow is null)
        {
            throw new NotFoundException("RosterProposal", id);
        }

        if (workflow.Status != AgentWorkflowStatus.PendingApproval && workflow.Status != AgentWorkflowStatus.Pending)
        {
            throw new IllegalTransitionException("RosterProposal", workflow.Status.ToString(), "Approved");
        }

        // The approving administrator cannot be the user who initiated the workflow run (Assignment §9.1)
        var initiatedBy = GetInitiatedByStaffId(workflow);
        if (_currentUser.IsAuthenticated && _currentUser.Id != Guid.Empty &&
            initiatedBy.HasValue && initiatedBy.Value == _currentUser.Id)
        {
            throw new ForbiddenException(MessageCode.Forbidden, "The approving administrator cannot be the user who initiated the proposal.");
        }

        var targetShift = await _db.Shifts
            .Include(s => s.RequiredSkill)
            .Include(s => s.Allocations)
            .FirstOrDefaultAsync(s => s.Id == workflow.EntityId, cancellationToken);

        if (targetShift is null)
        {
            throw new NotFoundException("Shift", workflow.EntityId);
        }

        var checkedAt = _clock.GetUtcNow();
        var failedChecks = new List<RosterValidationResult>();

        var createChange = workflow.ProposedChanges
            .FirstOrDefault(c => c.ChangeType == ProposedChangeType.CreateAllocation);

        var endChange = workflow.ProposedChanges
            .FirstOrDefault(c => c.ChangeType == ProposedChangeType.EndAllocation);

        if (createChange?.ProposedStaffMemberId is not { } proposedStaffId)
        {
            throw new ConflictException(MessageCode.Conflict, "Proposal has no valid proposed staff member.");
        }

        var staffMember = await _db.StaffMembers
            .FirstOrDefaultAsync(s => s.Id == proposedStaffId, cancellationToken);

        if (staffMember is null || !staffMember.IsActive || staffMember.DeletedAt != null)
        {
            failedChecks.Add(new RosterValidationResult
            {
                Check = "staff_is_active",
                Passed = false,
                Detail = "Staff member is no longer active or has been deactivated.",
                CheckedAt = checkedAt
            });
        }
        else
        {
            if (staffMember.Role != targetShift.RequiredRole)
            {
                failedChecks.Add(new RosterValidationResult
                {
                    Check = "staff_holds_required_skill_on_shift_date",
                    Passed = false,
                    Detail = $"Staff member role '{staffMember.Role}' does not match required role '{targetShift.RequiredRole}'.",
                    CheckedAt = checkedAt
                });
            }

            if (targetShift.RequiredSkillId.HasValue)
            {
                var hasValidSkill = await _db.StaffMemberSkills.AsNoTracking()
                    .AnyAsync(s => s.StaffMemberId == staffMember.Id
                                   && s.SkillId == targetShift.RequiredSkillId.Value
                                   && (s.ValidFrom == null || s.ValidFrom.Value <= targetShift.Date)
                                   && (s.ExpiresAt == null || s.ExpiresAt.Value >= targetShift.Date),
                              cancellationToken);

                if (!hasValidSkill)
                {
                    failedChecks.Add(new RosterValidationResult
                    {
                        Check = "staff_holds_required_skill_on_shift_date",
                        Passed = false,
                        Detail = $"Staff member does not hold active certification for required skill on {targetShift.Date:yyyy-MM-dd}.",
                        CheckedAt = checkedAt
                    });
                }
            }

            var (targetStart, targetEnd) = StaffAllocationAgentTools.GetShiftDateTimeRange(targetShift);
            var leaveEndDateInclusive = DateOnly.FromDateTime(targetEnd.Date);

            var conflictingLeave = await _db.LeaveRequests.AsNoTracking()
                .FirstOrDefaultAsync(l => l.StaffMemberId == staffMember.Id
                                          && l.Status == LeaveStatus.Approved
                                          && l.StartDate <= leaveEndDateInclusive
                                          && l.EndDate >= targetShift.Date,
                                     cancellationToken);

            if (conflictingLeave is not null)
            {
                failedChecks.Add(new RosterValidationResult
                {
                    Check = "staff_not_on_approved_leave",
                    Passed = false,
                    Detail = $"Staff member has approved leave from {conflictingLeave.StartDate:yyyy-MM-dd} to {conflictingLeave.EndDate:yyyy-MM-dd}.",
                    CheckedAt = checkedAt
                });
            }

            var nearbyAllocations = await _db.Allocations.AsNoTracking()
                .Include(a => a.Shift)
                .Where(a => a.StaffMemberId == staffMember.Id
                            && a.Status == AllocationStatus.Confirmed
                            && a.EndedAt == null
                            && a.Shift.Date >= targetShift.Date.AddDays(-2)
                            && a.Shift.Date <= targetShift.Date.AddDays(2))
                .ToListAsync(cancellationToken);

            foreach (var existing in nearbyAllocations)
            {
                if (endChange is not null && existing.Id == endChange.TargetEntityId)
                {
                    continue;
                }

                var (otherStart, otherEnd) = StaffAllocationAgentTools.GetShiftDateTimeRange(existing.Shift);
                if (targetStart < otherEnd && otherStart < targetEnd)
                {
                    failedChecks.Add(new RosterValidationResult
                    {
                        Check = "staff_not_double_booked",
                        Passed = false,
                        Detail = $"Staff member is already allocated to an overlapping shift on {existing.Shift.Date:yyyy-MM-dd}.",
                        CheckedAt = checkedAt
                    });
                    break;
                }
            }
        }

        Allocation? donorAllocation = null;
        Shift? donorShift = null;

        if (endChange is not null)
        {
            if (endChange.TargetEntityId is not { } donorAllocId)
            {
                throw new ConflictException(MessageCode.Conflict, "Cascading swap proposal missing source allocation ID.");
            }

            donorAllocation = await _db.Allocations
                .Include(a => a.Shift)
                .FirstOrDefaultAsync(a => a.Id == donorAllocId, cancellationToken);

            if (donorAllocation is null || donorAllocation.Status != AllocationStatus.Confirmed || donorAllocation.EndedAt != null)
            {
                failedChecks.Add(new RosterValidationResult
                {
                    Check = "source_ward_stays_at_or_above_minimum",
                    Passed = false,
                    Detail = "Source allocation is no longer active or confirmed.",
                    CheckedAt = checkedAt
                });
            }
            else
            {
                donorShift = donorAllocation.Shift;
                var donorConfirmedCount = await _db.Allocations.CountAsync(
                    a => a.ShiftId == donorShift.Id
                      && a.Status == AllocationStatus.Confirmed
                      && a.EndedAt == null,
                    cancellationToken);

                if (donorConfirmedCount - 1 < donorShift.MinimumHeadcount)
                {
                    failedChecks.Add(new RosterValidationResult
                    {
                        Check = "source_ward_stays_at_or_above_minimum",
                        Passed = false,
                        Detail = $"Source ward would fall below minimum headcount of {donorShift.MinimumHeadcount}.",
                        CheckedAt = checkedAt
                    });
                }
            }
        }

        if (failedChecks.Count > 0)
        {
            throw new AllocationRejectedException(failedChecks, "Proposal re-validation failed: real-world hospital state changed.");
        }

        // Apply all proposed changes atomically inside a database transaction
        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);

        var now = _clock.GetUtcNow();
        var currentStaffId = _currentUser.IsAuthenticated && _currentUser.Id != Guid.Empty ? _currentUser.Id : (Guid?)null;

        var newAllocation = new Allocation
        {
            Id = Guid.NewGuid(),
            ShiftId = targetShift.Id,
            StaffMemberId = staffMember!.Id,
            Status = AllocationStatus.Confirmed,
            Source = AllocationSource.AgentProposal,
            RosterProposalId = workflow.Id,
            CreatedByStaffId = currentStaffId,
            CreatedAt = now,
            UpdatedAt = now
        };
        _db.Allocations.Add(newAllocation);

        createChange.AppliedAt = now;
        createChange.AppliedEntityId = newAllocation.Id;

        if (endChange is not null && donorAllocation is not null)
        {
            donorAllocation.EndedAt = now;
            donorAllocation.EndedReason = AllocationEndReason.SwappedOut;
            donorAllocation.ReplacedByAllocationId = newAllocation.Id;
            donorAllocation.UpdatedAt = now;

            endChange.AppliedAt = now;
            endChange.AppliedEntityId = donorAllocation.Id;
        }

        workflow.Status = AgentWorkflowStatus.Approved;
        workflow.ReviewedByStaffMemberId = currentStaffId;
        workflow.ReviewedAt = now;
        workflow.ReviewNotes = request.Notes;
        workflow.FinalOutcome = "approved";

        await _db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return await GetProposalDetailAsync(workflow.Id, cancellationToken);
    }

    public async Task<RosterProposalDetail> RejectProposalAsync(
        Guid id,
        RejectRosterProposalRequest request,
        CancellationToken cancellationToken = default)
    {
        var workflow = await _db.AgentWorkflows
            .Include(w => w.ProposedChanges)
            .FirstOrDefaultAsync(w => w.Id == id && w.AgentType == AgentType.StaffAllocation, cancellationToken);

        if (workflow is null)
        {
            throw new NotFoundException("RosterProposal", id);
        }

        if (workflow.Status != AgentWorkflowStatus.PendingApproval && workflow.Status != AgentWorkflowStatus.Pending)
        {
            throw new IllegalTransitionException("RosterProposal", workflow.Status.ToString(), "Rejected");
        }

        var now = _clock.GetUtcNow();
        var currentStaffId = _currentUser.IsAuthenticated && _currentUser.Id != Guid.Empty ? _currentUser.Id : (Guid?)null;

        workflow.Status = AgentWorkflowStatus.Rejected;
        workflow.ReviewedByStaffMemberId = currentStaffId;
        workflow.ReviewedAt = now;
        workflow.ReviewNotes = request.Notes;
        workflow.FinalOutcome = EnumWire.ToWire(request.Reason);

        await _db.SaveChangesAsync(cancellationToken);

        return await GetProposalDetailAsync(workflow.Id, cancellationToken);
    }

    public async Task<RosterProposalSummary> RequestRevisionAsync(
        Guid id,
        RequestRosterProposalRevisionRequest request,
        CancellationToken cancellationToken = default)
    {
        var workflow = await _db.AgentWorkflows
            .Include(w => w.ProposedChanges)
            .FirstOrDefaultAsync(w => w.Id == id && w.AgentType == AgentType.StaffAllocation, cancellationToken);

        if (workflow is null)
        {
            throw new NotFoundException("RosterProposal", id);
        }

        if (workflow.Status != AgentWorkflowStatus.PendingApproval && workflow.Status != AgentWorkflowStatus.Pending)
        {
            throw new IllegalTransitionException("RosterProposal", workflow.Status.ToString(), "RevisionRequested");
        }

        const int MaxAttempts = 3;
        var now = _clock.GetUtcNow();
        var currentStaffId = _currentUser.IsAuthenticated && _currentUser.Id != Guid.Empty ? _currentUser.Id : (Guid?)null;

        workflow.ReviewedByStaffMemberId = currentStaffId;
        workflow.ReviewedAt = now;
        workflow.ReviewNotes = !string.IsNullOrWhiteSpace(request.Guidance) ? request.Guidance : request.Notes;

        if (workflow.AttemptCount >= MaxAttempts)
        {
            workflow.Status = AgentWorkflowStatus.Failed;
            workflow.FinalOutcome = "retry_limit_reached";
            await _db.SaveChangesAsync(cancellationToken);

            return await ToSummaryAsync(workflow, cancellationToken);
        }

        workflow.AttemptCount++;

        _db.AgentProposedChanges.RemoveRange(workflow.ProposedChanges.Where(c => c.AppliedAt == null));

        var targetShift = await _db.Shifts
            .FirstOrDefaultAsync(s => s.Id == workflow.EntityId, cancellationToken);

        if (targetShift is null)
        {
            workflow.Status = AgentWorkflowStatus.Failed;
            workflow.FinalOutcome = "shift_not_found";
            await _db.SaveChangesAsync(cancellationToken);
            return await ToSummaryAsync(workflow, cancellationToken);
        }

        var agentRequest = new StaffAllocationAgentRequest(
            workflow.EntityId,
            workflow.Objective,
            AllowCascadingSwap: true,
            ExcludeStaffIds: request.ExcludeStaffIds,
            ExcludeWardIds: request.ExcludeWardIds,
            WorkflowId: workflow.Id,
            ParentWorkflowId: workflow.ParentWorkflowId,
            CorrelationId: workflow.CorrelationId,
            InitiatedByStaffId: GetInitiatedByStaffId(workflow));

        var run = await _agent.RunAsync(agentRequest, cancellationToken);

        workflow.Plan = RosterWorkflowJson.Write(run.Plan);
        workflow.CompletedSteps = RosterWorkflowJson.Write(run.Plan.Where(p => p.Status == "completed").ToList());
        workflow.ToolResults = RosterWorkflowJson.Write(run.ToolCalls);
        workflow.ValidationResults = RosterWorkflowJson.Write(run.Validation);
        workflow.Errors = run.Errors.Count > 0 ? RosterWorkflowJson.Write(run.Errors) : null;
        workflow.Status = run.Status == AgentWorkflowStatus.PendingApproval
            ? AgentWorkflowStatus.PendingApproval
            : AgentWorkflowStatus.RevisionRequested;
        workflow.FinalOutcome = EnumWire.ToWire(run.Outcome);

        foreach (var change in run.ProposedChangeEntities)
        {
            workflow.ProposedChanges.Add(change);
        }

        await _db.SaveChangesAsync(cancellationToken);

        return await ToSummaryAsync(workflow, cancellationToken);
    }

    public async Task<Guid?> TriggerProposalIfUnderstaffedAsync(
        Guid shiftId,
        CancellationToken cancellationToken = default)
    {
        var shift = await _db.Shifts.AsNoTracking()
            .Include(s => s.Allocations)
            .FirstOrDefaultAsync(s => s.Id == shiftId, cancellationToken);

        if (shift is null)
        {
            return null;
        }

        var confirmedCount = shift.Allocations
            .Count(a => a.Status == AllocationStatus.Confirmed && a.EndedAt == null);

        if (confirmedCount >= shift.MinimumHeadcount)
        {
            return null;
        }

        var hasOpenProposal = await _db.AgentWorkflows.AnyAsync(
            w => w.AgentType == AgentType.StaffAllocation
              && w.EntityId == shiftId
              && (w.Status == AgentWorkflowStatus.Pending || w.Status == AgentWorkflowStatus.PendingApproval),
            cancellationToken);

        if (hasOpenProposal)
        {
            return null;
        }

        var proposal = await CreateProposalAsync(new CreateRosterProposalRequest
        {
            ShiftId = shiftId,
            Objective = $"Automatic proposal to restore minimum staffing level for shift {shift.Date:yyyy-MM-dd}.",
            AllowCascadingSwap = true
        }, cancellationToken);

        return proposal.Id;
    }

    private async Task<RosterProposalSummary> ToSummaryAsync(AgentWorkflow workflow, CancellationToken ct)
    {
        var shift = await _db.Shifts.AsNoTracking().FirstOrDefaultAsync(s => s.Id == workflow.EntityId, ct);
        var wardName = shift is not null
            ? await _db.Wards.AsNoTracking().Where(w => w.Id == shift.WardId).Select(w => w.Name).FirstOrDefaultAsync(ct) ?? "Ward"
            : "Ward";

        return ToSummary(workflow, shift, wardName);
    }

    private static RosterProposalSummary ToSummary(AgentWorkflow workflow, Shift? shift, string wardName)
    {
        return new RosterProposalSummary
        {
            Id = workflow.Id,
            WorkflowId = workflow.Id,
            ShiftId = workflow.EntityId,
            WardName = wardName,
            ShiftDate = shift?.Date ?? DateOnly.FromDateTime(workflow.CreatedAt.UtcDateTime),
            Objective = workflow.Objective,
            Status = MapStatus(workflow.Status),
            Outcome = ParseOutcome(workflow.FinalOutcome),
            IsCascadingSwap = CheckIsCascadingSwap(workflow),
            ChangeCount = workflow.ProposedChanges.Count,
            CreatedAt = workflow.CreatedAt
        };
    }

    private static RosterProposalStatus MapStatus(AgentWorkflowStatus status) => status switch
    {
        AgentWorkflowStatus.Pending => RosterProposalStatus.Pending,
        AgentWorkflowStatus.PendingApproval => RosterProposalStatus.PendingApproval,
        AgentWorkflowStatus.Approved => RosterProposalStatus.Approved,
        AgentWorkflowStatus.AutoApproved => RosterProposalStatus.Approved,
        AgentWorkflowStatus.Executed => RosterProposalStatus.Executed,
        AgentWorkflowStatus.Rejected => RosterProposalStatus.Rejected,
        AgentWorkflowStatus.RevisionRequested => RosterProposalStatus.RevisionRequested,
        AgentWorkflowStatus.Failed => RosterProposalStatus.Failed,
        _ => RosterProposalStatus.Pending
    };

    private static AgentOutcome? ParseOutcome(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var trimmed = value.Trim();
        foreach (var o in Enum.GetValues<AgentOutcome>())
        {
            if (string.Equals(trimmed, EnumWire.ToWire(o), StringComparison.OrdinalIgnoreCase) ||
                string.Equals(trimmed, o.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                return o;
            }
        }
        return null;
    }

    private static RejectionReason? ParseRejectionReason(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var trimmed = value.Trim();
        foreach (var r in Enum.GetValues<RejectionReason>())
        {
            if (string.Equals(trimmed, EnumWire.ToWire(r), StringComparison.OrdinalIgnoreCase) ||
                string.Equals(trimmed, r.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                return r;
            }
        }
        return null;
    }

    private static bool CheckIsCascadingSwap(AgentWorkflow workflow)
    {
        if (string.Equals(workflow.FinalOutcome, EnumWire.ToWire(AgentOutcome.SwapProposed), StringComparison.OrdinalIgnoreCase) ||
            string.Equals(workflow.FinalOutcome, nameof(AgentOutcome.SwapProposed), StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        foreach (var change in workflow.ProposedChanges)
        {
            if (change.ChangeType == ProposedChangeType.EndAllocation)
            {
                return true;
            }

            if (!string.IsNullOrWhiteSpace(change.Payload))
            {
                if (change.Payload.Contains("\"from_ward\"", StringComparison.OrdinalIgnoreCase) ||
                    change.Payload.Contains("\"is_cascading_swap\":true", StringComparison.OrdinalIgnoreCase) ||
                    change.Payload.Contains("\"is_cascading_swap\": true", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static RosterProposedChangeDto MapProposedChange(
        AgentProposedChange change,
        Dictionary<Guid, string> staffNames,
        Dictionary<Guid, string> wardNames)
    {
        var changeType = change.ChangeType switch
        {
            ProposedChangeType.EndAllocation => RosterProposedChangeType.EndAllocation,
            _ => RosterProposedChangeType.CreateAllocation
        };

        string? staffName = null;
        if (change.ProposedStaffMemberId.HasValue && staffNames.TryGetValue(change.ProposedStaffMemberId.Value, out var sn))
        {
            staffName = sn;
        }

        string? fromWard = null;
        string? toWard = null;
        Guid? fromShiftId = null;
        Guid? toShiftId = null;
        Guid? targetAllocationId = null;
        string? rationale = null;

        if (!string.IsNullOrWhiteSpace(change.Payload))
        {
            try
            {
                using var doc = JsonDocument.Parse(change.Payload);
                var root = doc.RootElement;
                if (root.TryGetProperty("from_ward", out var fw)) fromWard = fw.GetString();
                if (root.TryGetProperty("to_ward", out var tw)) toWard = tw.GetString();
                if (root.TryGetProperty("staff_name", out var sname) && staffName == null) staffName = sname.GetString();
                if (root.TryGetProperty("rationale", out var rat)) rationale = rat.GetString();
                if (root.TryGetProperty("shift_id", out var sid) && sid.TryGetGuid(out var parsedShiftId)) toShiftId = parsedShiftId;
                if (root.TryGetProperty("target_allocation_id", out var taid) && taid.TryGetGuid(out var parsedTaid)) targetAllocationId = parsedTaid;
                if (root.TryGetProperty("allocation_id", out var aid) && aid.TryGetGuid(out var parsedAid)) targetAllocationId = parsedAid;
            }
            catch
            {
                // Fallback to entity properties
            }
        }

        if (changeType == RosterProposedChangeType.EndAllocation)
        {
            targetAllocationId ??= change.TargetEntityId;
            if (change.ProposedWardId.HasValue && wardNames.TryGetValue(change.ProposedWardId.Value, out var wn))
            {
                fromWard ??= wn;
            }
        }
        else
        {
            toShiftId ??= change.TargetEntityId;
            if (change.ProposedWardId.HasValue && wardNames.TryGetValue(change.ProposedWardId.Value, out var wn))
            {
                toWard ??= wn;
            }
        }

        return new RosterProposedChangeDto
        {
            Id = change.Id,
            Sequence = change.Sequence,
            ChangeType = changeType,
            TargetAllocationId = targetAllocationId,
            ProposedStaffMemberId = change.ProposedStaffMemberId,
            ProposedStaffName = staffName,
            ProposedShiftId = toShiftId,
            FromShiftId = fromShiftId,
            FromWardName = fromWard,
            ToWardName = toWard,
            Rationale = rationale,
            ValidationStatus = change.ValidationStatus,
            ValidationMessage = change.ValidationMessage,
            AppliedAt = change.AppliedAt,
            AppliedEntityId = change.AppliedEntityId
        };
    }

    private static Guid? GetInitiatedByStaffId(AgentWorkflow workflow)
    {
        foreach (var change in workflow.ProposedChanges)
        {
            if (!string.IsNullOrWhiteSpace(change.Payload))
            {
                try
                {
                    using var doc = JsonDocument.Parse(change.Payload);
                    if (doc.RootElement.TryGetProperty("created_by_staff_id", out var prop) && prop.TryGetGuid(out var id))
                    {
                        return id;
                    }
                }
                catch
                {
                }
            }
        }

        return null;
    }
}
