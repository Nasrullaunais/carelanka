using CareLanka.Api.Common.Errors;
using CareLanka.Api.Common.Exceptions;
using CareLanka.Api.Data.Enums;
using CareLanka.Api.DTOs.Patient;
using CareLanka.Api.Services.Patient;

namespace CareLanka.Api.Agents.Patient;

/// <summary>
/// The Bed and Patient Details Agent. Given a patient - found by NIC, by patient code, or from an
/// admission already on the board - it says who that patient is and suggests which bed to put them
/// in.
/// </summary>
/// <remarks>
/// It writes nothing. All of its tools read, every hard rule is checked in C# through the same
/// <see cref="BedPlacementRules"/> the manual endpoint runs, and the language model only reorders
/// beds that have already passed. The pause assignment §9.1 asks for is a workflow row at
/// <c>awaiting_approval</c> - not a half-finished write in the domain. The admission stays at
/// <c>awaiting_bed</c> and the bed stays free for anybody until a human presses a button.
/// </remarks>
public sealed class BedAgent
{
    public static readonly IReadOnlyList<string> Plan =
    [
        "resolve_patient",
        "gather_requirements",
        "gather_beds",
        "filter_hard_rules",
        "rank_soft_rules",
        "decide",
        "validate",
        "pause_for_approval"
    ];

    private readonly IBedAgentTools _tools;
    private readonly ILanguageModel _model;
    private readonly TimeProvider _time;
    private readonly ILogger<BedAgent> _log;

    public BedAgent(
        IBedAgentTools tools,
        ILanguageModel model,
        TimeProvider time,
        ILogger<BedAgent> log)
    {
        _tools = tools;
        _model = model;
        _time = time;
        _log = log;
    }

    public async Task<BedAgentResult> RunAsync(
        Guid? admissionId, string? patientIdentifier, CancellationToken ct = default)
    {
        var trace = new AgentTrace(_time, Plan);

        try
        {
            return await ExecuteAsync(trace, admissionId, patientIdentifier, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            // A recorded safe failure, which assignment §9.1 asks for by name. Nothing was written
            // on the way here, so there is nothing to undo - the nurse assigns a bed by hand.
            _log.LogError(exception, "The bed agent failed.");

            trace.Error(exception.Message);

            return Blocked(trace, BedAgentOutcome.Failed, BedSuggestionBlockers.AgentFailed());
        }
    }

    private async Task<BedAgentResult> ExecuteAsync(
        AgentTrace trace, Guid? admissionId, string? patientIdentifier, CancellationToken ct)
    {
        var resolved = await ResolveAsync(trace, patientIdentifier, ct);

        if (patientIdentifier is not null)
        {
            if (resolved is null)
            {
                return Blocked(
                    trace, BedAgentOutcome.PatientNotFound, BedSuggestionBlockers.NoSuchPatient());
            }

            if (resolved.OpenAdmissionId is null)
            {
                return Blocked(
                    trace,
                    BedAgentOutcome.NoBedAvailable,
                    BedSuggestionBlockers.NoOpenAdmission(resolved.FullName),
                    patientId: resolved.PatientId);
            }

            admissionId = resolved.OpenAdmissionId;
        }

        var requirements = await trace.ToolAsync(
            "get_admission_requirements",
            $"admission_id={admissionId}",
            () => _tools.GetAdmissionRequirementsAsync(admissionId!.Value, ct),
            found => found is null ? "not found" : $"category={found.Category}, status={found.Status}")
            ?? throw new NotFoundException("Admission", admissionId!.Value);

        // H0 first, and before any bed is listed: "they do not need a bed" is a different answer
        // from "the hospital is full", and a screen that cannot tell them apart sends a nurse
        // hunting for a bed that was never required.
        if (!BedPlacementRules.RequiresBed(requirements.Category))
        {
            trace.Validate("requires_bed", false, $"{requirements.Category} needs no bed.");

            return BlockedFor(
                trace,
                BedAgentOutcome.VisitNeedsNoBed,
                BedSuggestionBlockers.NoBedRequired(requirements.Category),
                requirements);
        }

        trace.Validate("requires_bed", true);

        var candidates = await trace.ToolAsync(
            "list_available_beds",
            "ward_type=any",
            () => _tools.ListAvailableBedsAsync(null, ct),
            beds => $"{beds.Count} free usable beds");

        var loads = await trace.ToolAsync(
            "get_ward_occupancy",
            "all wards",
            () => _tools.GetWardOccupancyAsync(ct),
            wards => $"{wards.Count} wards");

        var previousWards = await trace.ToolAsync(
            "list_previous_wards",
            $"patient_id={requirements.PatientId}",
            () => _tools.ListPreviousWardsAsync(requirements.PatientId, ct),
            wards => $"{wards.Count} previous wards");

        var (placeable, rejected) = Filter(trace, requirements, candidates, loads, previousWards);

        if (placeable.Count == 0)
        {
            return BlockedFor(
                trace,
                candidates.Count == 0 || rejected.All(bed => bed.Reason == BedSuggestionBlockerCode.WardFull)
                    ? BedAgentOutcome.NoBedAvailable
                    : Outcome(rejected),
                Blocker(requirements, candidates.Count, rejected),
                requirements);
        }

        var ranked = await RankAsync(trace, requirements, placeable, ct);

        ranked = Validate(trace, requirements, ranked);

        if (ranked.Count == 0)
        {
            // Everything the ranking offered failed the second, deterministic check. The answer is
            // the same as having found nothing, which is the point of running it twice.
            return BlockedFor(
                trace,
                BedAgentOutcome.NoBedAvailable,
                BedSuggestionBlockers.WardFull(requirements.Category),
                requirements);
        }

        using (trace.Step("decide"))
        {
        }

        var best = ranked[0];

        var outcome = best.IsDowngrade
            ? BedAgentOutcome.ProposedWithDowngrade
            : BedAgentOutcome.Proposed;

        var blocker = best.IsDowngrade
            ? BedSuggestionBlockers.DowngradeNeeded(requirements.Category, best.Bed.BedNumber)
            : null;

        using (trace.Step("pause_for_approval"))
        {
        }

        return new BedAgentResult(
            outcome,
            requirements.PatientId,
            requirements.AdmissionId,
            requirements,
            ranked,
            blocker,
            best.RequiresDutyManager ? StaffRole.DutyManager : StaffRole.WardNurse,
            trace);
    }

    private async Task<ResolvedPatient?> ResolveAsync(
        AgentTrace trace, string? patientIdentifier, CancellationToken ct)
    {
        if (patientIdentifier is null)
        {
            return null;
        }

        return await trace.ToolAsync(
            "find_patient",
            // The identifier is echoed because it is what the nurse typed and what a trace has to
            // explain. It is never concatenated into a prompt - see BedAgentRanking.ToModelPayload.
            $"identifier={patientIdentifier}",
            () => _tools.FindPatientAsync(patientIdentifier, ct),
            found => found is null
                ? "no match"
                : $"patient={found.PatientId}, open_admission={found.OpenAdmissionId?.ToString() ?? "none"}");
    }

    /// <summary>
    /// Hard rules H0-H6, run through the one <see cref="BedPlacementRules.EnsurePlaceable"/> the
    /// manual endpoint runs. "The AI cannot break rule H4" is true because there is exactly one H4
    /// in the codebase and everybody goes through it.
    /// </summary>
    private (List<PlaceableBed> Placeable, List<RejectedBed> Rejected) Filter(
        AgentTrace trace,
        AdmissionRequirements requirements,
        IReadOnlyList<CandidateBed> candidates,
        IReadOnlyList<WardLoad> loads,
        IReadOnlyCollection<Guid> previousWards)
    {
        using var step = trace.Step("filter_hard_rules");

        var occupancyByWard = loads.ToDictionary(load => load.WardId, load => load.OccupancyRatio);

        var placeable = new List<PlaceableBed>();
        var rejected = new List<RejectedBed>();

        foreach (var candidate in candidates)
        {
            try
            {
                // mayPlaceMoreAcute stays false for the agent even though a duty manager may
                // overrule it by hand. A human overruling a rule they can see is not the same act
                // as a model deciding the rule did not apply.
                var isDowngrade = BedPlacementRules.EnsurePlaceable(
                    requirements.Category,
                    requirements.Gender,
                    requirements.DateOfBirth,
                    requirements.IsInfectious,
                    candidate.Ward,
                    candidate.Bed,
                    mayPlaceMoreAcute: false);

                placeable.Add(new PlaceableBed(
                    candidate,
                    isDowngrade,
                    BedPlacementRules.NeedsDutyManager(requirements.Category, candidate.WardType),
                    RulesSatisfied,
                    occupancyByWard.GetValueOrDefault(candidate.WardId, 1),
                    previousWards.Contains(candidate.WardId)));
            }
            catch (ConflictException exception)
            {
                rejected.Add(new RejectedBed(candidate, ReasonFor(exception.Code)));
            }
        }

        trace.Validate(
            "hard_rules_h0_h6",
            placeable.Count > 0,
            $"{placeable.Count} of {candidates.Count} free beds passed H0-H6.");

        return (placeable, rejected);
    }

    private static readonly string[] RulesSatisfied =
        ["requires_bed", "bed_usable", "category_match", "gender_policy", "isolation", "age_policy", "ward_active"];

    private async Task<List<PlaceableBed>> RankAsync(
        AgentTrace trace,
        AdmissionRequirements requirements,
        List<PlaceableBed> placeable,
        CancellationToken ct)
    {
        using var step = trace.Step("rank_soft_rules");

        var ranked = BedAgentRanking.Deterministic(placeable, requirements.Category);

        var payload = BedAgentRanking.ToModelPayload(requirements, ranked);

        var answer = await _model.CompleteJsonAsync(BedAgentRanking.ModelInstruction, payload, ct);

        if (!answer.Ok || answer.Json is null)
        {
            // Not a failure of the run. The deterministic ranking is a complete answer on its own;
            // the model only ever made it a better-explained one.
            trace.Error($"rank_soft_rules: {answer.Error}");
            return ranked;
        }

        var merged = BedAgentRanking.Merge(ranked, answer.Json, out var mergeError);

        if (mergeError is not null)
        {
            trace.Error($"rank_soft_rules: {mergeError}");
        }

        return merged;
    }

    /// <summary>
    /// The deterministic re-check, step 7 of §8.7. Every bed that reaches a human is run through
    /// the hard rules a second time, and anything failing here is removed from the answer before
    /// anybody sees it. It runs a third time, under a row lock, at <c>assign-bed</c>.
    /// </summary>
    private static List<PlaceableBed> Validate(
        AgentTrace trace, AdmissionRequirements requirements, List<PlaceableBed> ranked)
    {
        using var step = trace.Step("validate");

        var survivors = new List<PlaceableBed>();

        foreach (var bed in ranked)
        {
            try
            {
                BedPlacementRules.EnsurePlaceable(
                    requirements.Category,
                    requirements.Gender,
                    requirements.DateOfBirth,
                    requirements.IsInfectious,
                    bed.Bed.Ward,
                    bed.Bed.Bed,
                    mayPlaceMoreAcute: false);

                survivors.Add(bed);
            }
            catch (ConflictException exception)
            {
                trace.Validate(
                    $"revalidate_{bed.Bed.BedNumber}", false, exception.Code.ToWire());
            }
        }

        trace.Validate(
            "revalidate_all",
            survivors.Count == ranked.Count,
            $"{survivors.Count} of {ranked.Count} ranked beds still place.");

        return survivors;
    }

    private static BedAgentOutcome Outcome(IReadOnlyList<RejectedBed> rejected)
        => rejected.Count > 0
            && rejected.All(bed => bed.Reason == BedSuggestionBlockerCode.UpgradeOnly)
                ? BedAgentOutcome.NeedsDutyManager
                : BedAgentOutcome.NoBedAvailable;

    /// <summary>
    /// The most actionable wall, not the most common one. A nurse told "no isolation bed is free"
    /// knows what to do next; a nurse told "ward full" does not.
    /// </summary>
    private static BedSuggestionBlocker Blocker(
        AdmissionRequirements requirements, int freeBeds, IReadOnlyList<RejectedBed> rejected)
    {
        if (freeBeds == 0)
        {
            return BedSuggestionBlockers.WardFull(requirements.Category);
        }

        var reasons = rejected.Select(bed => bed.Reason).ToHashSet();

        if (reasons.Contains(BedSuggestionBlockerCode.NeedsIsolation))
        {
            return BedSuggestionBlockers.NeedsIsolation(freeBeds);
        }

        if (reasons.Contains(BedSuggestionBlockerCode.GenderPolicy))
        {
            return BedSuggestionBlockers.GenderPolicy(freeBeds, requirements.Gender);
        }

        if (reasons.Contains(BedSuggestionBlockerCode.PediatricOnly))
        {
            return BedSuggestionBlockers.PediatricOnly(
                PatientAge.InYears(requirements.DateOfBirth));
        }

        if (reasons.Contains(BedSuggestionBlockerCode.UpgradeOnly))
        {
            return BedSuggestionBlockers.UpgradeOnly(requirements.Category);
        }

        return BedSuggestionBlockers.WardFull(requirements.Category);
    }

    private static BedSuggestionBlockerCode ReasonFor(MessageCode code) => code switch
    {
        MessageCode.BedWardTooAcute => BedSuggestionBlockerCode.UpgradeOnly,
        MessageCode.BedWardGenderPolicy => BedSuggestionBlockerCode.GenderPolicy,
        MessageCode.BedNeedsIsolation => BedSuggestionBlockerCode.NeedsIsolation,
        MessageCode.BedWardPediatricAdult => BedSuggestionBlockerCode.PediatricOnly,
        _ => BedSuggestionBlockerCode.WardFull
    };

    private static BedAgentResult BlockedFor(
        AgentTrace trace,
        BedAgentOutcome outcome,
        BedSuggestionBlocker blocker,
        AdmissionRequirements requirements)
        => new(
            outcome,
            requirements.PatientId,
            requirements.AdmissionId,
            requirements,
            [],
            blocker,
            null,
            trace);

    private static BedAgentResult Blocked(
        AgentTrace trace,
        BedAgentOutcome outcome,
        BedSuggestionBlocker blocker,
        Guid? patientId = null)
        => new(outcome, patientId, null, null, [], blocker, null, trace);
}

/// <summary>
/// What one run decided. Nothing in it has been written anywhere - persisting it is the caller's
/// job, and committing it is a human's.
/// </summary>
public sealed record BedAgentResult(
    BedAgentOutcome Outcome,
    Guid? PatientId,
    Guid? AdmissionId,
    AdmissionRequirements? Requirements,
    IReadOnlyList<PlaceableBed> Ranked,
    BedSuggestionBlocker? Blocker,
    StaffRole? RequiredApproverRole,
    AgentTrace Trace);
