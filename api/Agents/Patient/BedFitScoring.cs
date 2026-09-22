using CareLanka.Api.Data.Enums;
using CareLanka.Api.Services.Patient;

namespace CareLanka.Api.Agents.Patient;

/// <summary>
/// Step 5 of the run: weigh one bed that has already passed every hard rule against this
/// particular patient. The hard rules only say a bed is allowed; these say whether it is a good
/// idea, and each one carries the sentence a nurse reads on screen.
/// <para>
/// Every weight is a judgement we are making on purpose, so they are all in one table rather than
/// scattered through the ranking code. A positive weight is a reason to pick this bed, a negative
/// one a reason to look further down the list.
/// </para>
/// </summary>
public static class BedFitScoring
{
    /// <summary>Exactly the ward the care level asks for.</summary>
    private const double CareLevelMatch = 2.0;

    /// <summary>
    /// One rung below the care level. Large and negative so a downgrade never outranks a match on
    /// the strength of an emptier ward - the ladder is a last resort, not a preference.
    /// </summary>
    private const double CareLevelDowngrade = -3.0;

    /// <summary>A child in the children's ward, which nothing else can make up for.</summary>
    private const double ChildInPediatric = 2.5;

    /// <summary>A child on an adult general ward. Allowed by H6, still not where they belong.</summary>
    private const double ChildOnAdultWard = -1.5;

    /// <summary>An infectious patient in a bed that can isolate them.</summary>
    private const double IsolationNeeded = 1.5;

    /// <summary>
    /// Spending an isolation bed on somebody who does not need one. The hospital has a handful and
    /// the next infectious arrival cannot be planned for, so it costs something to use one up.
    /// </summary>
    private const double IsolationSpent = -1.0;

    /// <summary>A single-sex ward that matches the patient, over an open mixed bay.</summary>
    private const double GenderPrivacy = 0.5;

    /// <summary>How far an empty ward can carry a bed. Full scale for an empty ward, none for a full one.</summary>
    private const double LoadSpread = 1.5;

    /// <summary>A ward the patient has been on before - the staff already know them.</summary>
    private const double Continuity = 0.75;

    /// <summary>
    /// Taking a ward's last free bed. Waived for an emergency: the point of keeping one in hand is
    /// the patient who cannot wait, and that patient is this one.
    /// </summary>
    private const double LastBedInWard = -0.5;

    public static IReadOnlyList<BedFitFactor> Weigh(
        AdmissionRequirements requirements,
        PlacedBed placed,
        WardLoad? load,
        bool seenWardBefore)
    {
        var ward = placed.Candidate.Ward;
        var bed = placed.Candidate.Bed;
        var age = BedPlacementRules.AgeOn(
            requirements.DateOfBirth, DateOnly.FromDateTime(DateTime.UtcNow));

        var factors = new List<BedFitFactor>
        {
            placed.IsDowngrade
                ? new BedFitFactor(
                    BedRuleNames.CareLevelFit,
                    CareLevelDowngrade,
                    $"{ward.Name} is one level below the "
                        + $"{BedRuleNames.Spoken(requirements.Category)} care this patient needs.")
                : new BedFitFactor(
                    BedRuleNames.CareLevelFit,
                    CareLevelMatch,
                    $"{ward.Name} is at the right care level for a "
                        + $"{BedRuleNames.Spoken(requirements.Category)} patient.")
        };

        AddAge(factors, ward.WardType, age);
        AddIsolation(factors, requirements.IsInfectious, bed.HasIsolation);
        AddPrivacy(factors, ward.GenderPolicy, requirements.Gender);
        AddLoad(factors, ward.Name, load, requirements.Urgency);

        if (seenWardBefore)
        {
            factors.Add(new BedFitFactor(
                BedRuleNames.Continuity,
                Continuity,
                $"The patient has been on {ward.Name} before, so the staff already know them."));
        }

        return factors;
    }

    public static double Total(IReadOnlyList<BedFitFactor> factors)
        => factors.Sum(factor => factor.Weight);

    private static void AddAge(List<BedFitFactor> factors, WardType wardType, int? age)
    {
        if (age is not { } years || years >= BedPlacementRules.PediatricAgeLimit)
        {
            return;
        }

        if (wardType == WardType.Pediatric)
        {
            factors.Add(new BedFitFactor(
                BedRuleNames.AgeFit,
                ChildInPediatric,
                $"The children's ward, and the patient is {years}."));

            return;
        }

        // ICU and HDU take a child when they need that level of care, and a paediatric bed would
        // not be a safe swap for it. Only a general ward is the wrong room for the right reason.
        if (BedPlacementRules.Rung(wardType) == 2)
        {
            factors.Add(new BedFitFactor(
                BedRuleNames.AgeFit,
                ChildOnAdultWard,
                $"An adult ward for a {years}-year-old."));
        }
    }

    private static void AddIsolation(
        List<BedFitFactor> factors, bool isInfectious, bool hasIsolation)
    {
        if (!hasIsolation)
        {
            return;
        }

        factors.Add(isInfectious
            ? new BedFitFactor(
                BedRuleNames.Isolation, IsolationNeeded, "The bed can isolate, which this patient needs.")
            : new BedFitFactor(
                BedRuleNames.Isolation,
                IsolationSpent,
                "An isolation bed, which the admission record does not ask for - "
                    + "using it leaves one fewer for an infectious arrival."));
    }

    private static void AddPrivacy(List<BedFitFactor> factors, GenderPolicy policy, Gender gender)
    {
        var matches = (policy == GenderPolicy.Male && gender == Gender.Male)
            || (policy == GenderPolicy.Female && gender == Gender.Female);

        if (matches)
        {
            factors.Add(new BedFitFactor(
                BedRuleNames.GenderPrivacy,
                GenderPrivacy,
                $"A {BedRuleNames.Spoken(gender)}-only ward rather than an open bay."));
        }
    }

    private static void AddLoad(
        List<BedFitFactor> factors, string wardName, WardLoad? load, AdmissionUrgency urgency)
    {
        if (load is null || load.UsableBeds == 0)
        {
            return;
        }

        var free = Math.Max(0, load.UsableBeds - load.ClaimedBeds);

        factors.Add(new BedFitFactor(
            BedRuleNames.WardLoad,
            (1d - load.Load) * LoadSpread,
            $"{free} of {load.UsableBeds} beds on {wardName} are free."));

        if (free <= 1 && urgency != AdmissionUrgency.Emergency)
        {
            factors.Add(new BedFitFactor(
                BedRuleNames.Headroom,
                LastBedInWard,
                $"It is the last free bed on {wardName}, which leaves the ward nothing in hand."));
        }
    }
}

/// <summary>
/// One reason, with what it is worth. The sentence is the whole point: a score on its own tells a
/// nurse nothing, and a bed suggested without a reason is a bed they have to check by hand anyway.
/// </summary>
public sealed record BedFitFactor(string Rule, double Weight, string Detail);
