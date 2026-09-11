using CareLanka.Api.Common.Errors;
using CareLanka.Api.Common.Exceptions;
using CareLanka.Api.Common.Persistence;
using CareLanka.Api.Data.Enums;
using CareLanka.Api.Services.Patient;
using Xunit;

namespace CareLanka.Api.Tests;

// The admission workflow is the backbone of this component, and docs/build/patient.md says to
// test it hardest. These are plain unit tests with no database and no HTTP, so every one of the
// 49 from/to pairs gets checked — including the ones no endpoint can reach yet, which is
// exactly where an unnoticed mistake would sit until step 7 went to build on it.
public sealed class AdmissionStatusMachineTests
{
    // The workflow as patient-management-plan.md 4.2 prints it, in the wire names the API
    // publishes. Written in this shape on purpose: transcribing the machine's own C# dictionary
    // into a test produces a test that agrees with whatever the code says, including its bugs.
    // A status with nothing after the arrow is terminal.
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

        // All 49, not just the interesting ones. A matrix tested only where it is used is a
        // matrix with untested holes, and the holes are where step 6 and step 7 will arrive.
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
        // Adding a status to the enum and forgetting the table would not fail to compile. It
        // would throw KeyNotFoundException the first time a patient reached the new status,
        // which is the worst possible moment to find out.
        foreach (var status in Enum.GetValues<AdmissionStatus>())
        {
            Assert.NotNull(AdmissionStatusMachine.MovesFrom(status));
        }
    }

    [Fact]
    public void Standing_still_is_not_a_move()
    {
        // So cancelling an already-cancelled visit is a 409 rather than a quiet second success.
        // Nothing in the table lists a status as its own destination, and this is what keeps it
        // that way if somebody adds a row later.
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

        // A visit that ended stays ended. Reopening one rewrites history, and it would also slip
        // a second open admission past ux_admissions_open_patient for the same patient.
        Assert.Equal(
            new[] { AdmissionStatus.Discharged, AdmissionStatus.Cancelled },
            terminal);
    }

    [Fact]
    public void An_admitted_patient_cannot_be_cancelled()
    {
        // You cannot call off somebody lying in your ward. They get discharged instead, which is
        // the only move admitted has.
        Assert.False(AdmissionStatusMachine.IsLegal(AdmissionStatus.Admitted, AdmissionStatus.Cancelled));
        Assert.Equal(
            new[] { AdmissionStatus.ReadyForDischarge },
            AdmissionStatusMachine.MovesFrom(AdmissionStatus.Admitted));
    }

    [Fact]
    public void Both_ways_a_bed_can_fall_through_lead_back_to_the_same_waiting_state()
    {
        // A rejected proposal and an expired hold are different events with one destination.
        // One waiting state, not one error state per kind of failure.
        Assert.True(AdmissionStatusMachine.IsLegal(
            AdmissionStatus.AwaitingApproval, AdmissionStatus.AwaitingBed));
        Assert.True(AdmissionStatusMachine.IsLegal(
            AdmissionStatus.BedReserved, AdmissionStatus.AwaitingBed));
    }

    [Fact]
    public void A_discharge_flag_can_be_taken_back()
    {
        // The checklist rule flagged them and a nurse looked and said no. That reversal is what
        // keeps the flag advisory rather than binding.
        Assert.True(AdmissionStatusMachine.IsLegal(
            AdmissionStatus.ReadyForDischarge, AdmissionStatus.Admitted));
    }

    [Fact]
    public void A_refused_move_names_both_statuses_in_the_words_the_API_publishes()
    {
        var refused = Assert.Throws<IllegalTransitionException>(() =>
            AdmissionStatusMachine.EnsureMove(
                AdmissionStatus.Admitted, AdmissionStatus.Cancelled, AdmissionStatus.Admitted));

        // cl_err_409_transition is "{0} cannot move from {1} to {2}", and the arguments are the
        // wire names. A client reading "cannot move from Admitted" is reading a C# member name
        // that no part of the published contract uses.
        Assert.Equal("cl_err_409_transition", refused.Code.ToWire());
        Assert.Contains("admitted", refused.Message);
        Assert.Contains("cancelled", refused.Message);
        Assert.DoesNotContain("Admitted", refused.Message);
    }

    [Fact]
    public void An_endpoint_is_refused_a_move_it_does_not_start_from_even_when_the_workflow_allows_it()
    {
        // ready_for_discharge -> admitted is a legal move in the workflow, but POST /arrive is
        // not the endpoint that makes it: arriving stamps admitted_at and un-flagging a
        // discharge must not. Checking only the table would let one endpoint do another's job.
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
        // The safety net on the argument list: bed_reserved -> discharged is not in the table,
        // so naming bed_reserved as a starting state does not make it happen.
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

    /// <summary>The workflow block above, turned into a lookup of wire name to wire names.</summary>
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
