using CareLanka.Api.Data.Enums;
using CareLanka.Api.DTOs.Patient;
using CareLanka.Api.Services.Patient;

namespace CareLanka.Api.Agents.Patient;

/// <summary>
/// One plain sentence naming the wall the run hit, built here in C# from the filter result. Never
/// written by the model: a model asked to explain its own failure writes something plausible
/// rather than something true, and "no bed available" on its own sends a nurse to go and look for
/// themselves.
/// </summary>
public static class BedBlockers
{
    public static BedSuggestionBlocker DowngradeNeeded(AdmissionCategory category, RankedBed best)
    {
        var levels = BedSuggestionPlanner.RungsBelow(category, best.Candidate.Ward.WardType);

        return Blocker(
            BedSuggestionBlockerCode.DowngradeNeeded,
            $"No {BedRuleNames.Spoken(category)} beds are free. The nearest option is "
            + $"{best.Candidate.Bed.BedNumber}, {Levels(levels)} down. "
            + "A Duty Manager has to approve a downgrade.");
    }

    public static BedSuggestionBlocker UpgradeOnly(IReadOnlyList<DroppedBed> upgrades)
    {
        var wards = upgrades
            .Select(bed => BedRuleNames.Spoken(bed.Candidate.Ward.WardType))
            .Distinct()
            .ToList();

        return Blocker(
            BedSuggestionBlockerCode.UpgradeOnly,
            $"The only free beds are {Join(wards)} beds, which are above this patient's care "
            + "level. A Duty Manager can place them there by hand.");
    }

    public static BedSuggestionBlocker NothingFree(
        AdmissionRequirements requirements, BedFilterResult filtered)
    {
        if (requirements.IsInfectious
            && filtered.RefusedBy(BedRuleNames.Isolation) is { Count: > 0 })
        {
            return Blocker(
                BedSuggestionBlockerCode.NeedsIsolation,
                "This patient is infectious and no isolation bed is free.");
        }

        if (filtered.RefusedBy(BedRuleNames.GenderPolicy) is { Count: > 0 } wrongGender)
        {
            return Blocker(
                BedSuggestionBlockerCode.GenderPolicy,
                $"{Beds(wrongGender.Count)} free, but none is in a ward that takes a "
                + $"{BedRuleNames.Spoken(requirements.Gender)} patient.");
        }

        if (filtered.RefusedBy(BedRuleNames.AgePolicy) is { Count: > 0 })
        {
            return Blocker(
                BedSuggestionBlockerCode.PediatricOnly,
                "The only free beds are in the children's ward, and "
                + $"{Age(requirements.DateOfBirth)}.");
        }

        return Blocker(
            BedSuggestionBlockerCode.WardFull,
            $"No {BedRuleNames.Spoken(requirements.Category)} bed, and nothing one level down, is "
            + "free anywhere. The patient stays on the waiting list.");
    }

    public static BedSuggestionBlocker NoBedRequired(AdmissionCategory category)
        => Blocker(
            BedSuggestionBlockerCode.NoBedRequired,
            $"This is an {BedRuleNames.Spoken(category)} visit. They do not need a bed.");

    public static BedSuggestionBlocker NoSuchPatient()
        => Blocker(
            BedSuggestionBlockerCode.NoSuchPatient,
            "No patient matches that NIC or patient code.");

    public static BedSuggestionBlocker NoOpenAdmission(string fullName)
        => Blocker(
            BedSuggestionBlockerCode.NoOpenAdmission,
            $"{fullName} has no open visit, so there is no bed to suggest. "
            + "Register the visit first.");

    public static BedSuggestionBlocker AgentFailed()
        => Blocker(
            BedSuggestionBlockerCode.AgentFailed,
            "The suggestion could not be completed. Assign a bed by hand.");

    private static BedSuggestionBlocker Blocker(BedSuggestionBlockerCode code, string message)
        => new() { Code = code, Message = message };

    private static string Levels(int rungs)
        => rungs <= 1 ? "one level" : $"{rungs} levels";

    private static string Beds(int count)
        => count == 1 ? "One bed is" : $"{count} beds are";

    private static string Join(IReadOnlyList<string> words) => words.Count switch
    {
        0 => "other",
        1 => words[0],
        _ => string.Join(" and ", string.Join(", ", words.Take(words.Count - 1)), words[^1])
    };

    private static string Age(DateOnly? dateOfBirth)
    {
        if (dateOfBirth is not { } born)
        {
            return "this patient has no date of birth on record";
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var age = today.Year - born.Year;

        if (born > today.AddYears(-age))
        {
            age--;
        }

        return $"this patient is {age}";
    }
}
