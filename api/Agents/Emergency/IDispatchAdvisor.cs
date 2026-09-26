using CareLanka.Api.Data.Enums;

namespace CareLanka.Api.Agents.Emergency;

public interface IDispatchAdvisor
{
    Task<DispatchAdvice> ChooseAsync(DispatchChoiceContext context, CancellationToken ct = default);

    // Divertibility is a hard rule, so the model only explains the run the rules chose.
    Task<DispatchAdvice> ExplainDiversionAsync(DiversionContext context, CancellationToken ct = default);
}

public sealed record DispatchCallFacts(CallPriority Priority, string? Details, bool PatientKnown);

public sealed record RankedAmbulance(
    Guid Id,
    string RegistrationNumber,
    int? RouteMinutes,
    int CrewCount,
    int LocationAgeSeconds);

public sealed record DispatchChoiceContext(DispatchCallFacts Call, IReadOnlyList<RankedAmbulance> Shortlist);

public sealed record DiversionContext(
    DispatchCallFacts Call,
    DivertibleDispatchCandidate Source,
    int SourceWaitingMinutes);

public sealed record DispatchAdvice(
    Guid AmbulanceId,
    string Rationale,
    DispatchRecommendationSource Source = DispatchRecommendationSource.Model,
    string? SourceNote = null);
