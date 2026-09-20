using CareLanka.Api.Common.Errors;
using CareLanka.Api.Data.Enums;

namespace CareLanka.Api.Agents.Patient;

/// <summary>
/// The hard rules H0-H6 under the names the screen shows, and the translation from the refusal
/// the one shared rulebook throws back into the rule that refused. The agent gets no rulebook of
/// its own: it calls the same <c>BedPlacementRules.EnsurePlaceable</c> a nurse's manual pick
/// calls, and reads the rule off the refusal rather than re-deciding it.
/// </summary>
public static class BedRuleNames
{
    public const string RequiresBed = "requires_bed";
    public const string BedUsable = "bed_usable";
    public const string CategoryMatch = "category_match";
    public const string CategoryDowngrade = "category_downgrade";
    public const string GenderPolicy = "gender_policy";
    public const string Isolation = "isolation";
    public const string WardActive = "ward_active";
    public const string AgePolicy = "age_policy";

    public static string ForRefusal(MessageCode code) => code switch
    {
        MessageCode.BedWardNotInService => WardActive,
        MessageCode.BedOutOfService => BedUsable,
        MessageCode.BedWardTooAcute => CategoryMatch,
        MessageCode.BedWardGenderPolicy => GenderPolicy,
        MessageCode.BedWardPediatricAdult => AgePolicy,
        MessageCode.BedNeedsIsolation => Isolation,
        _ => "unknown_rule"
    };

    public static IReadOnlyList<string> Satisfied(bool isDowngrade) =>
    [
        RequiresBed,
        BedUsable,
        WardActive,
        isDowngrade ? CategoryDowngrade : CategoryMatch,
        GenderPolicy,
        Isolation,
        AgePolicy
    ];

    /// <summary>
    /// What a person calls this care level out loud. "inpatient" on a screen reads as jargon where
    /// "general" reads as the ward they are standing in.
    /// </summary>
    public static string Spoken(AdmissionCategory category) => category switch
    {
        AdmissionCategory.Icu => "ICU",
        AdmissionCategory.Hdu => "HDU",
        AdmissionCategory.Inpatient => "general",
        AdmissionCategory.DayCase => "day case",
        _ => "outpatient"
    };

    public static string Spoken(WardType wardType) => wardType switch
    {
        WardType.Icu => "ICU",
        WardType.Hdu => "HDU",
        WardType.MentalHealth => "mental health",
        _ => wardType.ToString().ToLowerInvariant()
    };

    public static string Spoken(Gender gender) => gender switch
    {
        Gender.Male => "male",
        Gender.Female => "female",
        Gender.Other => "non-binary",
        _ => "unidentified"
    };
}
