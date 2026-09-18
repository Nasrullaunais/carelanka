using CareLanka.Api.Common.Persistence;
using CareLanka.Api.Data.Enums;
using CareLanka.Api.DTOs.Patient;

namespace CareLanka.Api.Agents.Patient;

/// <summary>
/// The sentence a nurse reads when the agent could not suggest a bed, built in C# from the rule
/// that actually stopped it.
/// </summary>
/// <remarks>
/// Never written by the model, and that is the whole point: a model asked to explain its own
/// failure writes something plausible rather than something true. A counter is a filter result.
/// "No bed available" is accurate and almost useless - it sends a nurse to go and look for
/// themselves. Naming the wall tells them which other bed to try.
/// </remarks>
public static class BedSuggestionBlockers
{
    public static BedSuggestionBlocker NoSuchPatient() => Build(
        BedSuggestionBlockerCode.NoSuchPatient,
        "No patient matches that NIC or patient code. Check the slip, or register them.");

    public static BedSuggestionBlocker NoOpenAdmission(string fullName) => Build(
        BedSuggestionBlockerCode.NoOpenAdmission,
        $"{fullName} has no open visit. Open one before asking where to put them.");

    public static BedSuggestionBlocker NoBedRequired(AdmissionCategory category) => Build(
        BedSuggestionBlockerCode.NoBedRequired,
        $"This is an {EnumWire.ToWire(category).Replace('_', ' ')} visit. They do not need a bed.");

    public static BedSuggestionBlocker DowngradeNeeded(
        AdmissionCategory category, string bedNumber) => Build(
        BedSuggestionBlockerCode.DowngradeNeeded,
        $"No {Care(category)} beds are free. The nearest option is {bedNumber}, one level down. "
        + "A Duty Manager has to approve a downgrade.");

    public static BedSuggestionBlocker UpgradeOnly(AdmissionCategory category) => Build(
        BedSuggestionBlockerCode.UpgradeOnly,
        $"The only free beds are above this patient's {Care(category)} care level. "
        + "A Duty Manager can place them there by hand; the agent will not suggest it.");

    public static BedSuggestionBlocker WardFull(AdmissionCategory category) => Build(
        BedSuggestionBlockerCode.WardFull,
        $"No bed is free anywhere for a {Care(category)} patient, at that level or below. "
        + "They stay on the waiting list.");

    public static BedSuggestionBlocker GenderPolicy(int freeBeds, Gender gender) => Build(
        BedSuggestionBlockerCode.GenderPolicy,
        $"{Beds(freeBeds)} free, but every one is in a ward that does not take a patient "
        + $"recorded as {EnumWire.ToWire(gender)}.");

    public static BedSuggestionBlocker NeedsIsolation(int freeBeds) => Build(
        BedSuggestionBlockerCode.NeedsIsolation,
        $"This patient is infectious and none of the {Beds(freeBeds).ToLowerInvariant()} "
        + "has isolation.");

    public static BedSuggestionBlocker PediatricOnly(int? age) => Build(
        BedSuggestionBlockerCode.PediatricOnly,
        "The only free beds are in the children's ward, and this patient is "
        + (age is { } years
            ? $"{years}."
            : "recorded with no date of birth, which counts as an adult."));

    public static BedSuggestionBlocker AgentFailed() => Build(
        BedSuggestionBlockerCode.AgentFailed,
        "The suggestion could not be completed. Assign a bed by hand.");

    private static BedSuggestionBlocker Build(BedSuggestionBlockerCode code, string message)
        => new() { Code = code, Message = message };

    private static string Beds(int count) => count == 1 ? "1 bed is" : $"{count} beds are";

    private static string Care(AdmissionCategory category) => category switch
    {
        AdmissionCategory.Icu => "ICU",
        AdmissionCategory.Hdu => "high-dependency",
        _ => EnumWire.ToWire(category).Replace('_', ' ')
    };
}
