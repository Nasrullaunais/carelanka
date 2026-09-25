using CareLanka.Api.Agents;
using CareLanka.Api.Agents.Patient;
using CareLanka.Api.Common.Errors;
using CareLanka.Api.Common.Exceptions;
using CareLanka.Api.Common.Persistence;
using CareLanka.Api.Data;
using CareLanka.Api.Data.Entities.Common;
using CareLanka.Api.Data.Enums;
using CareLanka.Api.DTOs.Common;
using CareLanka.Api.DTOs.Patient;
using Microsoft.EntityFrameworkCore;
using CareRecommendationEntity = CareLanka.Api.Data.Entities.Patient.CareRecommendation;
using CareRecommendationResponse = CareLanka.Api.DTOs.Patient.CareRecommendation;

namespace CareLanka.Api.Services.Patient;

/// <summary>
/// The care advisory agent's entry point and its record - guards the run, persists the
/// recommendation and the plan before anything executes, hands the work to the background worker,
/// and answers every read the review queue and the patient's own list need.
/// </summary>
public sealed class CareRecommendationService : ICareRecommendationService
{
    public const string Objective = "draft_care_recommendation";

    /// <summary>
    /// The states in which a patient is physically on a ward with staff responsible for them
    /// right now. <c>awaiting_bed</c> and <c>bed_reserved</c> are not yet a stay to advise on;
    /// <c>discharged</c> and <c>cancelled</c> are over.
    /// </summary>
    private static readonly AdmissionStatus[] CurrentlyAdmittedStatuses =
    [
        AdmissionStatus.Admitted,
        AdmissionStatus.ReadyForDischarge
    ];

    private readonly CareLankaDbContext _db;
    private readonly ICareRunQueue _queue;

    public CareRecommendationService(CareLankaDbContext db, ICareRunQueue queue)
    {
        _db = db;
        _queue = queue;
    }

    public async Task<CareWorkflowAccepted> SubmitAsync(
        Guid patientId, CareQueryRequest request, CancellationToken ct = default)
    {
        var admission = await _db.Admissions
            .AsNoTracking()
            .Where(row => row.PatientId == patientId && CurrentlyAdmittedStatuses.Contains(row.Status))
            .OrderByDescending(row => row.AdmittedAt)
            .FirstOrDefaultAsync(ct);

        if (admission is null)
        {
            throw new ConflictException(MessageCode.NotCurrentlyAdmittedForCareQuery);
        }

        var recommendation = new CareRecommendationEntity
        {
            Id = Guid.NewGuid(),
            PatientId = patientId,
            AdmissionId = admission.Id,
            ReportedText = request.ReportedText.Trim(),
            ReportedAt = DateTimeOffset.UtcNow,
            Status = CareRecommendationStatus.PendingReview
        };

        _db.CareRecommendations.Add(recommendation);

        var workflow = new AgentWorkflow
        {
            Id = Guid.NewGuid(),
            AgentType = AgentType.PatientCareAdvisory,
            EntityType = CareAgentExecutor.WorkflowEntityType,
            EntityId = recommendation.Id,
            CorrelationId = Guid.NewGuid(),
            Objective = Objective,
            Plan = CareWorkflowJson.Write(CareAgent.Plan),
            Status = AgentWorkflowStatus.Pending,
            StartedAt = DateTimeOffset.UtcNow,
            AttemptCount = 0
        };

        _db.AgentWorkflows.Add(workflow);

        // Both rows are on disk before a single tool runs, so a run that dies mid-flight leaves a
        // trace rather than nothing, and the caller gets a recommendation_id immediately.
        await _db.SaveChangesAsync(ct);

        _queue.Enqueue(workflow.Id);

        return new CareWorkflowAccepted
        {
            WorkflowId = workflow.Id,
            RecommendationId = recommendation.Id,
            Status = "running",
            PollUrl = $"/api/care-workflows/{workflow.Id}"
        };
    }

    public async Task<CareWorkflowAccepted> RedraftAsync(Guid id, CancellationToken ct = default)
    {
        var recommendation = await _db.CareRecommendations
            .FirstOrDefaultAsync(row => row.Id == id, ct)
            ?? throw new NotFoundException("CareRecommendation", id);

        if (recommendation.Status != CareRecommendationStatus.PendingReview)
        {
            throw new ConflictException(MessageCode.CareRecommendationNotPendingReview);
        }

        // A fresh workflow rather than a reset of the old one: the first run is a real thing that
        // happened, and overwriting it would erase the record of why a redraft was needed.
        var workflow = new AgentWorkflow
        {
            Id = Guid.NewGuid(),
            AgentType = AgentType.PatientCareAdvisory,
            EntityType = CareAgentExecutor.WorkflowEntityType,
            EntityId = recommendation.Id,
            CorrelationId = Guid.NewGuid(),
            Objective = Objective,
            Plan = CareWorkflowJson.Write(CareAgent.Plan),
            Status = AgentWorkflowStatus.Pending,
            StartedAt = DateTimeOffset.UtcNow,
            AttemptCount = 0
        };

        _db.AgentWorkflows.Add(workflow);

        // Cleared so the screen shows the run in progress rather than the draft being replaced.
        recommendation.AgentMessage = null;
        recommendation.UrgencyFlag = null;

        await _db.SaveChangesAsync(ct);

        _queue.Enqueue(workflow.Id);

        return new CareWorkflowAccepted
        {
            WorkflowId = workflow.Id,
            RecommendationId = recommendation.Id,
            Status = "running",
            PollUrl = $"/api/care-workflows/{workflow.Id}"
        };
    }

    public async Task<CareWorkflowSummary> GetWorkflowAsync(Guid workflowId, CancellationToken ct = default)
    {
        var workflow = await _db.AgentWorkflows
            .AsNoTracking()
            .FirstOrDefaultAsync(
                row => row.Id == workflowId && row.AgentType == AgentType.PatientCareAdvisory, ct)
            ?? throw new NotFoundException("AgentWorkflow", workflowId);

        var recommendation = await _db.CareRecommendations
            .AsNoTracking()
            .FirstOrDefaultAsync(row => row.Id == workflow.EntityId, ct);

        var validation = CareWorkflowJson.Read<CareWorkflowValidationRecord>(workflow.ValidationResults)
            ?? new CareWorkflowValidationRecord();

        return new CareWorkflowSummary
        {
            WorkflowId = workflow.Id,
            RecommendationId = workflow.EntityId,
            Objective = workflow.Objective,
            Status = Status(workflow, recommendation),
            Outcome = workflow.FinalOutcome is null ? null : EnumWire.FromWire<CareAgentOutcome>(workflow.FinalOutcome),
            Plan = CareWorkflowJson.Read<List<string>>(workflow.Plan) ?? [],
            Steps = CareWorkflowJson.Read<List<CareAgentStep>>(workflow.CompletedSteps) ?? [],
            RedFlag = recommendation?.RedFlag ?? false,
            Validation = new CareWorkflowValidation
            {
                Passed = validation.Passed,
                FailedRules = validation.FailedRules
            },
            DraftSource = string.IsNullOrWhiteSpace(validation.DraftSource)
                ? CareDraftSource.Model
                : EnumWire.FromWire<CareDraftSource>(validation.DraftSource),
            DraftNote = validation.DraftNote,
            Retries = Math.Max(0, workflow.AttemptCount - 1)
        };
    }

    public async Task<PagedResult<CareRecommendationSummary>> ListAsync(
        CareRecommendationStatus? status,
        int page,
        int pageSize,
        DTOs.Patient.SortDirection sortDir,
        CancellationToken ct = default)
    {
        var query = _db.CareRecommendations.AsNoTracking().AsQueryable();

        if (status is { } wanted)
        {
            query = query.Where(row => row.Status == wanted);
        }

        var totalItems = await query.CountAsync(ct);

        query = sortDir == DTOs.Patient.SortDirection.Asc
            ? query.OrderBy(row => row.ReportedAt)
            : query.OrderByDescending(row => row.ReportedAt);

        var rows = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(row => new CareRecommendationSummary
            {
                Id = row.Id,
                PatientId = row.PatientId,
                ReportedAt = row.ReportedAt,
                RedFlag = row.RedFlag,
                UrgencyFlag = row.UrgencyFlag,
                Status = row.Status
            })
            .ToListAsync(ct);

        return PagedResult<CareRecommendationSummary>.From(rows, page, pageSize, totalItems);
    }

    public async Task<CareRecommendationResponse> GetAsync(Guid id, CancellationToken ct = default)
    {
        var recommendation = await _db.CareRecommendations
            .AsNoTracking()
            .FirstOrDefaultAsync(row => row.Id == id, ct)
            ?? throw new NotFoundException("CareRecommendation", id);

        // Newest first: a redraft adds a second workflow for the same report, and the screen has
        // to follow the run happening now rather than the one that produced the draft being replaced.
        var workflowId = await _db.AgentWorkflows
            .AsNoTracking()
            .Where(row => row.AgentType == AgentType.PatientCareAdvisory
                && row.EntityType == CareAgentExecutor.WorkflowEntityType
                && row.EntityId == id)
            .OrderByDescending(row => row.CreatedAt)
            .Select(row => (Guid?)row.Id)
            .FirstOrDefaultAsync(ct);

        return ToResponse(recommendation, workflowId);
    }

    public async Task<CareRecommendationResponse> ApproveAsync(
        Guid id, ApproveCareRecommendationRequest? request, Guid reviewerStaffId, CancellationToken ct = default)
    {
        var recommendation = await GetPendingAsync(id, ct);

        var doctorMessage = string.IsNullOrWhiteSpace(request?.DoctorMessage)
            ? recommendation.AgentMessage
            : request.DoctorMessage.Trim();

        // A run that produced no draft, approved with no override, still has to leave the patient
        // with something to read - "the review happened" is always true even when the draft is not.
        recommendation.DoctorMessage = string.IsNullOrWhiteSpace(doctorMessage)
            ? "A member of staff has reviewed your report and will follow up with you."
            : doctorMessage;

        recommendation.Status = CareRecommendationStatus.Approved;
        recommendation.ReviewedByStaffMemberId = reviewerStaffId;
        recommendation.ReviewedAt = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync(ct);

        return await GetAsync(id, ct);
    }

    public async Task<CareRecommendationResponse> RejectAsync(
        Guid id, RejectCareRecommendationRequest request, Guid reviewerStaffId, CancellationToken ct = default)
    {
        var recommendation = await GetPendingAsync(id, ct);

        recommendation.Status = CareRecommendationStatus.Rejected;
        recommendation.RejectionReason = request.Reason.Trim();
        recommendation.ReviewedByStaffMemberId = reviewerStaffId;
        recommendation.ReviewedAt = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync(ct);

        return await GetAsync(id, ct);
    }

    public async Task<PagedResult<MyCareRecommendation>> GetMyRecommendationsAsync(
        Guid patientId, int page, int pageSize, CancellationToken ct = default)
    {
        var query = _db.CareRecommendations
            .AsNoTracking()
            .Where(row => row.PatientId == patientId);

        var totalItems = await query.CountAsync(ct);

        var rows = await query
            .OrderByDescending(row => row.ReportedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(row => new MyCareRecommendation
            {
                Id = row.Id,
                ReportedText = row.ReportedText,
                ReportedAt = row.ReportedAt,
                Status = row.Status,
                DoctorMessage = row.Status == CareRecommendationStatus.Approved ? row.DoctorMessage : null,
                ReviewedAt = row.Status == CareRecommendationStatus.Approved ? row.ReviewedAt : null,
                ReviewedByName = row.Status == CareRecommendationStatus.Approved
                    ? _db.StaffMembers
                        .Where(staff => staff.Id == row.ReviewedByStaffMemberId)
                        .Select(staff => staff.FirstName + " " + staff.LastName)
                        .FirstOrDefault()
                    : null,
                ReviewedByRole = row.Status == CareRecommendationStatus.Approved
                    ? _db.StaffMembers
                        .Where(staff => staff.Id == row.ReviewedByStaffMemberId)
                        .Select(staff => staff.Role == StaffRole.Doctor
                            ? CareReviewerRole.Doctor
                            : (CareReviewerRole?)CareReviewerRole.WardNurse)
                        .FirstOrDefault()
                    : null
            })
            .ToListAsync(ct);

        return PagedResult<MyCareRecommendation>.From(rows, page, pageSize, totalItems);
    }

    private async Task<CareRecommendationEntity> GetPendingAsync(Guid id, CancellationToken ct)
    {
        var recommendation = await _db.CareRecommendations
            .FirstOrDefaultAsync(row => row.Id == id, ct)
            ?? throw new NotFoundException("CareRecommendation", id);

        if (recommendation.Status != CareRecommendationStatus.PendingReview)
        {
            throw new ConflictException(MessageCode.CareRecommendationNotPendingReview);
        }

        return recommendation;
    }

    private static CareWorkflowStatus Status(AgentWorkflow workflow, CareRecommendationEntity? recommendation)
    {
        if (workflow.Status == AgentWorkflowStatus.Failed)
        {
            return CareWorkflowStatus.Failed;
        }

        if (workflow.CompletedAt is null)
        {
            return CareWorkflowStatus.Running;
        }

        return recommendation?.Status == CareRecommendationStatus.PendingReview
            ? CareWorkflowStatus.PendingReview
            : CareWorkflowStatus.Completed;
    }

    private static CareRecommendationResponse ToResponse(CareRecommendationEntity row, Guid? workflowId)
        => new()
        {
            Id = row.Id,
            PatientId = row.PatientId,
            AdmissionId = row.AdmissionId,
            ReportedText = row.ReportedText,
            ReportedAt = row.ReportedAt,
            RedFlag = row.RedFlag,
            UrgencyFlag = row.UrgencyFlag,
            AgentMessage = row.AgentMessage,
            Status = row.Status,
            WorkflowId = workflowId,
            ReviewedByStaffId = row.ReviewedByStaffMemberId,
            ReviewedAt = row.ReviewedAt,
            DoctorMessage = row.DoctorMessage,
            RejectionReason = row.RejectionReason,
            CreatedAt = row.CreatedAt,
            UpdatedAt = row.UpdatedAt
        };
}
