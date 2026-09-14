using CareLanka.Api.Common.Errors;
using CareLanka.Api.Common.Exceptions;
using CareLanka.Api.Common.Persistence;
using CareLanka.Api.Data.Enums;
using CareLanka.Api.Services.Patient;
using Xunit;

namespace CareLanka.Api.Tests;

public sealed class AdmissionStatusMachineTests
{
    private const string Workflow = """
        awaiting_bed        -> awaiting_approval, cancelled
        awaiting_approval   -> bed_reserved, awaiting_bed, cancelled
        bed_reserved        -> admitted, awaiting_bed, cancelled
        admitted            -> ready_for_discharge
        ready_for_discharge -> discharged, admitted
        discharged          ->
        cancelled           ->
        """;

    [Fact]
    public void Every_pair_of_statuses_is_a_legal_move_or_not_exactly_as_the_workflow_prints_it()
    {
        var expected = Parse();
        var wrong = new List<string>();

        foreach (var from in Enum.GetValues<AdmissionStatus>())
        {
            foreach (var to in Enum.GetValues<AdmissionStatus>())
            {
                var shouldBeLegal = expected[from].Contains(to);

                if (AdmissionStatusMachine.IsLegal(from, to) != shouldBeLegal)
                {
                    wrong.Add($"{EnumWire.ToWire(from)} -> {EnumWire.ToWire(to)} "
                        + $"should be {(shouldBeLegal ? "legal" : "refused")}");
                }
            }
        }

        Assert.Empty(wrong);
    }

    [Fact]
    public void Every_published_status_has_a_row_in_the_workflow()
    {
        foreach (var status in Enum.GetValues<AdmissionStatus>())
        {
            Assert.NotNull(AdmissionStatusMachine.MovesFrom(status));
        }
    }

    [Fact]
    public void Standing_still_is_not_a_move()
    {
        foreach (var status in Enum.GetValues<AdmissionStatus>())
        {
            Assert.False(AdmissionStatusMachine.IsLegal(status, status), EnumWire.ToWire(status));
        }
    }

    [Fact]
    public void Discharged_and_cancelled_are_the_only_statuses_nothing_leaves()
    {
        var terminal = Enum.GetValues<AdmissionStatus>()
            .Where(AdmissionStatusMachine.IsTerminal)
            .ToArray();

        Assert.Equal(
            new[] { AdmissionStatus.Discharged, AdmissionStatus.Cancelled },
            terminal);
    }

    [Fact]
    public void An_admitted_patient_cannot_be_cancelled()
    {
        Assert.False(AdmissionStatusMachine.IsLegal(AdmissionStatus.Admitted, AdmissionStatus.Cancelled));
        Assert.Equal(
            new[] { AdmissionStatus.ReadyForDischarge },
            AdmissionStatusMachine.MovesFrom(AdmissionStatus.Admitted));
    }

    [Fact]
    public void Both_ways_a_bed_can_fall_through_lead_back_to_the_same_waiting_state()
    {
        Assert.True(AdmissionStatusMachine.IsLegal(
            AdmissionStatus.AwaitingApproval, AdmissionStatus.AwaitingBed));
        Assert.True(AdmissionStatusMachine.IsLegal(
            AdmissionStatus.BedReserved, AdmissionStatus.AwaitingBed));
    }

    [Fact]
    public void A_discharge_flag_can_be_taken_back()
    {
        Assert.True(AdmissionStatusMachine.IsLegal(
            AdmissionStatus.ReadyForDischarge, AdmissionStatus.Admitted));
    }

    [Fact]
    public void A_refused_move_names_both_statuses_in_the_words_the_API_publishes()
    {
        var refused = Assert.Throws<IllegalTransitionException>(() =>
            AdmissionStatusMachine.EnsureMove(
                AdmissionStatus.Admitted, AdmissionStatus.Cancelled, AdmissionStatus.Admitted));

        Assert.Equal("cl_err_409_transition", refused.Code.ToWire());
        Assert.Contains("admitted", refused.Message);
        Assert.Contains("cancelled", refused.Message);
        Assert.DoesNotContain("Admitted", refused.Message);
    }

    [Fact]
    public void An_endpoint_is_refused_a_move_it_does_not_start_from_even_when_the_workflow_allows_it()
    {
        Assert.True(AdmissionStatusMachine.IsLegal(
            AdmissionStatus.ReadyForDischarge, AdmissionStatus.Admitted));

        Assert.Throws<IllegalTransitionException>(() =>
            AdmissionStatusMachine.EnsureMove(
                AdmissionStatus.ReadyForDischarge,
                AdmissionStatus.Admitted,
                AdmissionStatus.BedReserved));
    }

    [Fact]
    public void A_move_the_workflow_does_not_have_is_refused_however_an_endpoint_asks_for_it()
    {
        Assert.Throws<IllegalTransitionException>(() =>
            AdmissionStatusMachine.EnsureMove(
                AdmissionStatus.BedReserved,
                AdmissionStatus.Discharged,
                AdmissionStatus.BedReserved));
    }

    [Theory]
    [InlineData(AdmissionStatus.BedReserved, AdmissionStatus.Admitted)]
    [InlineData(AdmissionStatus.AwaitingBed, AdmissionStatus.Cancelled)]
    [InlineData(AdmissionStatus.ReadyForDischarge, AdmissionStatus.Discharged)]
    public void A_legal_move_an_endpoint_does_start_from_is_allowed_through(
        AdmissionStatus from, AdmissionStatus to)
        => AdmissionStatusMachine.EnsureMove(from, to, from);

    private static Dictionary<AdmissionStatus, HashSet<AdmissionStatus>> Parse()
    {
        var table = new Dictionary<AdmissionStatus, HashSet<AdmissionStatus>>();

        foreach (var line in Workflow.Split('\n'))
        {
            var halves = line.Split("->");
            var from = EnumWire.FromWire<AdmissionStatus>(halves[0].Trim());

            table[from] = halves[1]
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(EnumWire.FromWire<AdmissionStatus>)
                .ToHashSet();
        }

        return table;
    }
}
