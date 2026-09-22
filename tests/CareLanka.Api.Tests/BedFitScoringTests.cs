using CareLanka.Api.Agents.Patient;
using CareLanka.Api.Data.Enums;
using CareLanka.Api.Services.Patient;
using Xunit;
using WardEntity = CareLanka.Api.Data.Entities.Patient.Ward;

namespace CareLanka.Api.Tests;

/// <summary>
/// The soft rules S1-S7. These decide which bed a nurse is shown first, so every weight here is a
/// judgement worth pinning down - and every one of them carries a sentence that ends up on screen.
/// </summary>
public sealed class BedFitScoringTests
{
    private static readonly DateOnly Child = DateOnly.FromDateTime(DateTime.UtcNow.AddYears(-9));
    private static readonly DateOnly Adult = DateOnly.FromDateTime(DateTime.UtcNow.AddYears(-40));

    [Fact]
    public void A_child_scores_higher_in_the_childrens_ward_than_on_a_general_ward()
    {
        var pediatric = Score(Bed(WardType.Pediatric), Requirements(dateOfBirth: Child));
        var general = Score(Bed(WardType.General), Requirements(dateOfBirth: Child));

        Assert.True(pediatric > general);
    }

    [Fact]
    public void An_adult_is_not_pushed_towards_a_general_ward_by_their_age()
    {
        var factors = BedFitScoring.Weigh(
            Requirements(dateOfBirth: Adult), Bed(WardType.General), load: null, seenWardBefore: false);

        Assert.DoesNotContain(factors, factor => factor.Rule == BedRuleNames.AgeFit);
    }

    [Fact]
    public void An_isolation_bed_is_worth_taking_for_an_infectious_patient_and_saving_for_anyone_else()
    {
        var infectious = Score(
            Bed(WardType.General, hasIsolation: true), Requirements(isInfectious: true));
        var ordinary = Score(
            Bed(WardType.General, hasIsolation: true), Requirements(isInfectious: false));

        Assert.True(infectious > ordinary);
    }

    [Fact]
    public void A_downgrade_never_outranks_a_match_however_empty_the_ward()
    {
        var emptyHdu = Score(
            Bed(WardType.Hdu, isDowngrade: true),
            Requirements(category: AdmissionCategory.Icu),
            Load(usable: 20, claimed: 0));

        var nearlyFullIcu = Score(
            Bed(WardType.Icu),
            Requirements(category: AdmissionCategory.Icu),
            Load(usable: 20, claimed: 19));

        Assert.True(nearlyFullIcu > emptyHdu);
    }

    [Fact]
    public void Taking_a_wards_last_bed_costs_something_unless_it_is_an_emergency()
    {
        var routine = BedFitScoring.Weigh(
            Requirements(urgency: AdmissionUrgency.Routine),
            Bed(WardType.General),
            Load(usable: 10, claimed: 9),
            seenWardBefore: false);

        var emergency = BedFitScoring.Weigh(
            Requirements(urgency: AdmissionUrgency.Emergency),
            Bed(WardType.General),
            Load(usable: 10, claimed: 9),
            seenWardBefore: false);

        Assert.Contains(routine, factor => factor.Rule == BedRuleNames.Headroom);
        Assert.DoesNotContain(emergency, factor => factor.Rule == BedRuleNames.Headroom);
    }

    [Fact]
    public void Every_factor_carries_a_sentence_a_nurse_can_read()
    {
        var factors = BedFitScoring.Weigh(
            Requirements(dateOfBirth: Child, isInfectious: true),
            Bed(WardType.General, hasIsolation: true),
            Load(usable: 10, claimed: 4),
            seenWardBefore: true);

        Assert.NotEmpty(factors);
        Assert.All(factors, factor => Assert.False(string.IsNullOrWhiteSpace(factor.Detail)));
    }

    private static double Score(PlacedBed bed, AdmissionRequirements requirements, WardLoad? load = null)
        => BedFitScoring.Total(
            BedFitScoring.Weigh(requirements, bed, load, seenWardBefore: false));

    private static PlacedBed Bed(
        WardType wardType, bool hasIsolation = false, bool isDowngrade = false)
    {
        var wardId = Guid.NewGuid();

        var ward = new WardEntity
        {
            Id = wardId,
            Name = $"{wardType} Ward",
            WardType = wardType,
            GenderPolicy = GenderPolicy.Mixed
        };

        var bed = new RegisteredBed(
            Guid.NewGuid(),
            wardId,
            $"{wardType}-01",
            hasIsolation,
            BedCondition.Usable,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow);

        return new PlacedBed(new CandidateBed(bed, ward), isDowngrade);
    }

    private static AdmissionRequirements Requirements(
        AdmissionCategory category = AdmissionCategory.Inpatient,
        DateOnly? dateOfBirth = null,
        bool isInfectious = false,
        AdmissionUrgency urgency = AdmissionUrgency.Routine)
        => new(
            Guid.NewGuid(),
            Guid.NewGuid(),
            category,
            Gender.Male,
            dateOfBirth ?? Adult,
            isInfectious,
            urgency,
            AdmissionStatus.AwaitingBed,
            ExpectedArrivalAt: null);

    private static WardLoad Load(int usable, int claimed)
        => new(Guid.NewGuid(), "Ward", usable, usable, claimed);
}
