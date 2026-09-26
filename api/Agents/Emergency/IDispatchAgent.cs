using CareLanka.Api.Data.Enums;
using CareLanka.Api.DTOs.Emergency;

namespace CareLanka.Api.Agents.Emergency;

public sealed record DispatchAgentRequest(
    Guid EmergencyCallId,
    CallPriority CallPriority,
    decimal Latitude,
    decimal Longitude,
    bool AllowDiversion,
    IReadOnlyList<Guid> ExcludeAmbulanceIds,
    string? CallDetails = null,
    bool PatientKnown = false);

public sealed record DispatchAgentRun(
    IReadOnlyList<DispatchPlanStep> Plan,
    IReadOnlyList<DispatchToolCall> ToolCalls,
    IReadOnlyList<DispatchValidationResult> Validation,
    DispatchOutcome Outcome,
    bool IsDiversion,
    Guid? ProposedAmbulanceId,
    string? ProposedAmbulanceRegistration,
    int? EstimatedMinutesToScene,
    string? Rationale,
    DiversionImpact? DiversionImpact,
    Guid? SourceDispatchId,
    IReadOnlyList<string> Errors,
    DispatchRecommendationSource? RecommendationSource = null,
    string? RecommendationNote = null);

public interface IDispatchAgent
{
    Task<DispatchAgentRun> RunAsync(DispatchAgentRequest request, CancellationToken cancellationToken = default);
}
