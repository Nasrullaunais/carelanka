using CareLanka.Api.Common.Exceptions;
using CareLanka.Api.Data.Enums;
using CareLanka.Api.DTOs.Patient;
using CareLanka.Api.Services.Patient;

namespace CareLanka.Api.Agents.Patient;

/// <summary>
/// Steps 4, 5 and 6 of the run: drop every bed that fails a hard rule, rank what survives on the
/// soft rules, and decide a best pick with selectable alternatives - or, when nothing survives,
/// say which wall the run hit. All of it is ordinary C# against the database. The model's only job
/// is the sentence on the end of a choice that has already been made and checked.
/// </summary>
public static class BedSuggestionPlanner
{
    /// <summary>
    /// How much continuity is worth against ward load. A quarter of a ward's fill: enough that a
    /// ward the patient has been in before wins between two comparably busy wards, not enough to
    /// send them back to a ward that is nearly full.
    /// </summary>
    private const double ContinuityBonus = 0.25;

    public static BedFilterResult Filter(
        AdmissionRequirements requirements, IReadOnlyList<CandidateBed> candidates)
    {
        var survivors = new List<PlacedBed>();
        var dropped = new List<DroppedBed>();

        foreach (var candidate in candidates)
        {
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

    public static IReadOnlyList<RankedBed> Rank(
        IReadOnlyList<PlacedBed> survivors,
        IReadOnlyDictionary<Guid, WardLoad> load,
        IReadOnlyCollection<Guid> previousWards)
        => survivors
            .Select(placed => new RankedBed(
                placed.Candidate,
                placed.IsDowngrade,
                Score(placed, load, previousWards),
                previousWards.Contains(placed.Candidate.Ward.Id),
                load.TryGetValue(placed.Candidate.Ward.Id, out var wardLoad) ? wardLoad : null))
            .OrderBy(ranked => ranked.Score)
            .ThenBy(ranked => ranked.Candidate.Ward.Name, StringComparer.Ordinal)
            .ThenBy(ranked => ranked.Candidate.Bed.BedNumber, StringComparer.Ordinal)
            .ToList();

    private static double Score(
        PlacedBed placed,
        IReadOnlyDictionary<Guid, WardLoad> load,
        IReadOnlyCollection<Guid> previousWards)
    {
        var wardLoad = load.TryGetValue(placed.Candidate.Ward.Id, out var found) ? found.Load : 1d;
        var continuity = previousWards.Contains(placed.Candidate.Ward.Id) ? ContinuityBonus : 0d;

        return wardLoad - continuity;
    }

    public static BedSuggestionAnswer Decide(
        AdmissionRequirements requirements,
        BedFilterResult filtered,
        IReadOnlyList<RankedBed> ranked)
    {
        var matches = ranked.Where(bed => !bed.IsDowngrade).ToList();

        var downgrades = ranked
            .Where(bed => bed.IsDowngrade)
            .OrderBy(bed => RungsBelow(requirements.Category, bed.Candidate.Ward.WardType))
            .ThenBy(bed => bed.Score)
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
    WardLoad? Load)
{
    /// <summary>
    /// A downgrade is the duty manager's whoever suggested it. An upgrade never reaches here - the
    /// agent refuses to rank one at all, so a manager who wants to place a patient in a more acute
    /// ward does it by hand, with their own name on it.
    /// </summary>
    public bool RequiresDutyManager => IsDowngrade;

    /// <summary>
    /// Filled in after the decision, by the one part of the run a model is allowed to touch. Null
    /// is an ordinary answer: a bed with no sentence on it is still a bed the nurse can pick.
    /// </summary>
    public string? Rationale { get; set; }
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
