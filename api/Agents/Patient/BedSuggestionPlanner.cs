using CareLanka.Api.Common.Exceptions;
using CareLanka.Api.Data.Enums;
using CareLanka.Api.DTOs.Patient;
using CareLanka.Api.Services.Patient;

namespace CareLanka.Api.Agents.Patient;

/// <summary>
/// Steps 4, 5 and 6 of the run: drop every bed that fails a hard rule, weigh what survives against
/// this patient, and decide a best pick with selectable alternatives - or, when nothing survives,
/// say which wall the run hit. All of it is ordinary C# against the database, so the ranking is
/// reproducible and every position in it can be explained. What the weights are is
/// <see cref="BedFitScoring"/>; the model never sees this step.
/// </summary>
public static class BedSuggestionPlanner
{
    public static BedFilterResult Filter(
        AdmissionRequirements requirements, IReadOnlyList<CandidateBed> candidates)
    {
        var survivors = new List<PlacedBed>();
        var dropped = new List<DroppedBed>();

        foreach (var candidate in candidates)
        {
            if (IsSpecialistWard(candidate.Ward.WardType))
            {
                // Maternity and Mental Health beds are never proposed automatically - there is no
                // field anywhere recording that this admission is actually a maternity or mental
                // health case, so an empty specialist ward would otherwise win on load alone. A
                // nurse who knows the patient belongs there still places them by hand.
                dropped.Add(new DroppedBed(candidate, BedRuleNames.SpecialistWard));
                continue;
            }

            try
            {
                var isDowngrade = BedPlacementRules.EnsurePlaceable(
                    requirements.Category,
                    requirements.Gender,
                    requirements.DateOfBirth,
                    requirements.IsInfectious,
                    candidate.Ward,
                    candidate.Bed);

                survivors.Add(new PlacedBed(candidate, isDowngrade));
            }
            catch (ConflictException refusal)
            {
                dropped.Add(new DroppedBed(candidate, BedRuleNames.ForRefusal(refusal.Code)));
            }
        }

        return new BedFilterResult(survivors, dropped, candidates.Count);
    }

    /// <summary>
    /// Best first. A higher score is a better fit, which is the opposite of the old ordering and
    /// worth saying out loud: the number now reads the way the screen does.
    /// </summary>
    public static IReadOnlyList<RankedBed> Rank(
        AdmissionRequirements requirements,
        IReadOnlyList<PlacedBed> survivors,
        IReadOnlyDictionary<Guid, WardLoad> load,
        IReadOnlyCollection<Guid> previousWards)
        => survivors
            .Select(placed =>
            {
                var wardLoad = load.TryGetValue(placed.Candidate.Ward.Id, out var found)
                    ? found
                    : null;
                var seenBefore = previousWards.Contains(placed.Candidate.Ward.Id);
                var factors = BedFitScoring.Weigh(requirements, placed, wardLoad, seenBefore);

                return new RankedBed(
                    placed.Candidate,
                    placed.IsDowngrade,
                    BedFitScoring.Total(factors),
                    seenBefore,
                    wardLoad,
                    factors);
            })
            .OrderByDescending(ranked => ranked.Score)
            .ThenBy(ranked => ranked.Candidate.Ward.Name, StringComparer.Ordinal)
            .ThenBy(ranked => ranked.Candidate.Bed.BedNumber, StringComparer.Ordinal)
            .ToList();

    public static BedSuggestionAnswer Decide(
        AdmissionRequirements requirements,
        BedFilterResult filtered,
        IReadOnlyList<RankedBed> ranked)
    {
        var matches = ranked.Where(bed => !bed.IsDowngrade).ToList();

        var downgrades = ranked
            .Where(bed => bed.IsDowngrade)
            .OrderBy(bed => RungsBelow(requirements.Category, bed.Candidate.Ward.WardType))
            .ThenByDescending(bed => bed.Score)
            .ToList();

        if (matches.Count > 0)
        {
            var best = matches[0];

            return new BedSuggestionAnswer(
                BedAgentOutcome.Proposed,
                best,
                [.. matches.Skip(1), .. downgrades],
                Blocker: null,
                Approver(best));
        }

        if (downgrades.Count > 0)
        {
            var best = downgrades[0];

            return new BedSuggestionAnswer(
                BedAgentOutcome.ProposedWithDowngrade,
                best,
                downgrades.Skip(1).ToList(),
                BedBlockers.DowngradeNeeded(requirements.Category, best),
                BedApproverRole.DutyManager);
        }

        if (filtered.RefusedBy(BedRuleNames.CategoryMatch) is { Count: > 0 } upgrades)
        {
            return new BedSuggestionAnswer(
                BedAgentOutcome.NeedsDutyManager,
                Best: null,
                Array.Empty<RankedBed>(),
                BedBlockers.UpgradeOnly(upgrades),
                BedApproverRole.DutyManager);
        }

        return new BedSuggestionAnswer(
            BedAgentOutcome.NoBedAvailable,
            Best: null,
            Array.Empty<RankedBed>(),
            BedBlockers.NothingFree(requirements, filtered),
            RequiresApprovalBy: null);
    }

    private static bool IsSpecialistWard(WardType wardType)
        => wardType is WardType.Maternity or WardType.MentalHealth;

    public static int RungsBelow(AdmissionCategory category, WardType wardType)
        => BedPlacementRules.Rung(wardType) - BedPlacementRules.Rung(category);

    private static BedApproverRole Approver(RankedBed best)
        => best.RequiresDutyManager ? BedApproverRole.DutyManager : BedApproverRole.WardNurse;
}

public sealed record PlacedBed(CandidateBed Candidate, bool IsDowngrade);

public sealed record DroppedBed(CandidateBed Candidate, string Rule);

public sealed record RankedBed(
    CandidateBed Candidate,
    bool IsDowngrade,
    double Score,
    bool SeenThisWardBefore,
    WardLoad? Load,
    IReadOnlyList<BedFitFactor> Factors)
{
    /// <summary>
    /// A downgrade is the duty manager's whoever suggested it. An upgrade never reaches here - the
    /// agent refuses to rank one at all, so a manager who wants to place a patient in a more acute
    /// ward does it by hand, with their own name on it.
    /// </summary>
    public bool RequiresDutyManager => IsDowngrade;

    /// <summary>
    /// Filled in after the decision. Null is an ordinary answer: a bed with no sentence on it is
    /// still a bed the nurse can pick.
    /// </summary>
    public string? Rationale { get; set; }

    /// <summary>The plain sentences behind <see cref="Score"/>, best reason first.</summary>
    public IReadOnlyList<string> Reasons
        => Factors.OrderByDescending(factor => factor.Weight).Select(factor => factor.Detail).ToList();
}

public sealed record BedFilterResult(
    IReadOnlyList<PlacedBed> Survivors,
    IReadOnlyList<DroppedBed> Dropped,
    int FreeBedsSeen)
{
    public IReadOnlyList<DroppedBed> RefusedBy(string rule)
        => Dropped.Where(bed => bed.Rule == rule).ToList();
}

public sealed record BedSuggestionAnswer(
    BedAgentOutcome Outcome,
    RankedBed? Best,
    IReadOnlyList<RankedBed> Alternatives,
    BedSuggestionBlocker? Blocker,
    BedApproverRole? RequiresApprovalBy);
