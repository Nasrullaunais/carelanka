using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using CareLanka.Api.Data;
using CareLanka.Api.Data.Entities.Patient;
using CareLanka.Api.Data.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CareLanka.Api.Tests;

/// <summary>
/// Step 6 — manual bed assignment. <c>GET /api/bed-availability</c>,
/// <c>POST /api/admissions/{id}/assign-bed</c> and <c>GET /api/beds/{id}/occupancy</c>.
/// </summary>
/// <remarks>
/// The fixture's database is shared by the whole collection, so every test makes its own ward
/// and its own beds and asserts on those rather than on the shape of a whole list.
/// </remarks>
[Collection(ApiCollection.Name)]
public sealed class BedAssignmentEndpointTests
{
    private readonly ApiApplication _application;

    public BedAssignmentEndpointTests(ApiApplication application) => _application = application;

    // ---------- the candidate list ----------

    [Fact]
    public async Task An_empty_usable_bed_is_free()
    {
        using var client = await ClientAsync(ApiApplication.NurseEmail);
        var ward = await NewWardAsync();
        var beds = await AddBedsAsync(ward, 1);

        var bed = await BedRowAsync(client, ward, beds[0]);

        Assert.Equal("free", bed.GetProperty("availability").GetString());
        Assert.Equal(JsonValueKind.Null, bed.GetProperty("occupied_by_admission_id").ValueKind);
        Assert.Equal(ward.Name, bed.GetProperty("ward_name").GetString());
    }

    [Fact]
    public async Task A_held_bed_reports_reserved_and_names_the_visit_holding_it()
    {
        using var client = await ClientAsync(ApiApplication.NurseEmail);
        var ward = await NewWardAsync();
        var beds = await AddBedsAsync(ward, 1);
        var admissionId = await AssignAsync(beds[0]);

        var bed = await BedRowAsync(client, ward, beds[0]);

        // Named for a hold as well as an occupancy: a bed board has to show who is coming, not
        // only who is here, and `availability` already says which of the two this is.
        Assert.Equal("reserved", bed.GetProperty("availability").GetString());
        Assert.Equal(admissionId, bed.GetProperty("occupied_by_admission_id").GetString());
    }

    [Fact]
    public async Task A_lapsed_hold_reports_the_bed_free_with_nobody_having_released_it()
    {
        using var client = await ClientAsync(ApiApplication.NurseEmail);
        var ward = await NewWardAsync();
        var beds = await AddBedsAsync(ward, 1);
        await AssignAsync(beds[0]);
        await ExpireHoldAsync(beds[0]);

        var bed = await BedRowAsync(client, ward, beds[0]);

        // The whole point of expiry: an ambulance that never arrives must not keep a bed off
        // the list, and freeing it takes nobody's approval and no background job.
        Assert.Equal("free", bed.GetProperty("availability").GetString());
    }

    [Fact]
    public async Task A_bed_out_of_service_is_listed_and_never_as_free()
    {
        using var client = await ClientAsync(ApiApplication.NurseEmail);
        var ward = await NewWardAsync();
        var beds = await AddBedsAsync(ward, 1);
        await WithdrawAsync(beds[0]);

        var bed = await BedRowAsync(client, ward, beds[0]);

        // Listed, not hidden. A nurse looking at a ward needs to see that the bed exists and is
        // broken; dropping it makes the ward look smaller than it is.
        Assert.Equal("out_of_service", bed.GetProperty("availability").GetString());
        Assert.Equal("out_of_service", bed.GetProperty("condition").GetString());
    }

    [Fact]
    public async Task Filtering_by_free_leaves_out_the_broken_bed_and_the_held_one()
    {
        using var client = await ClientAsync(ApiApplication.NurseEmail);
        var ward = await NewWardAsync();
        var beds = await AddBedsAsync(ward, 3);
        await WithdrawAsync(beds[0]);
        await AssignAsync(beds[1]);

        var free = await BedIdsAsync(client, ward, "availability=free");

        Assert.Equal(new[] { beds[2] }, free);
    }

    [Fact]
    public async Task Filtering_by_isolation_answers_only_the_beds_that_have_it()
    {
        using var client = await ClientAsync(ApiApplication.NurseEmail);
        var ward = await NewWardAsync();
        var plain = await AddBedsAsync(ward, 1);
        var isolating = await AddBedsAsync(ward, 1, hasIsolation: true, firstNumber: 2);

        var withIsolation = await BedIdsAsync(client, ward, "needsIsolation=true");
        var without = await BedIdsAsync(client, ward, "needsIsolation=false");

        Assert.Equal(isolating, withIsolation);
        Assert.Equal(plain, without);
    }

    [Fact]
    public async Task A_retired_ward_contributes_no_candidates_at_all()
    {
        using var client = await ClientAsync(ApiApplication.NurseEmail);
        var ward = await NewWardAsync(isActive: false);
        await AddBedsAsync(ward, 2);

        var listed = await BedIdsAsync(client, ward, "availability=all");

        // Hard rule H5 arriving for free: the global query filter hides a retired ward, so its
        // beds never reach a candidate list and there is no second check to forget.
        Assert.Empty(listed);
    }

    [Fact]
    public async Task Filtering_by_ward_type_is_how_the_agent_will_walk_the_downgrade_ladder()
    {
        using var client = await ClientAsync(ApiApplication.NurseEmail);
        var icu = await NewWardAsync(wardType: "icu");
        var general = await NewWardAsync();
        var icuBeds = await AddBedsAsync(icu, 1);
        await AddBedsAsync(general, 1);

        var listed = await BedIdsAsync(client, icu, "wardType=icu");

        Assert.Equal(icuBeds, listed);
    }

    [Fact]
    public async Task Page_zero_is_a_400_rather_than_a_silently_empty_list()
    {
        using var client = await ClientAsync(ApiApplication.NurseEmail);

        var response = await client.GetAsync("/api/bed-availability?page=0");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ---------- assigning by hand ----------

    [Fact]
    public async Task A_nurse_assigns_a_matching_bed_and_the_visit_moves_to_bed_reserved()
    {
        using var nurse = await ClientAsync(ApiApplication.NurseEmail);
        var ward = await NewWardAsync();
        var beds = await AddBedsAsync(ward, 1);
        var admissionId = await NewAdmissionAsync(nurse);

        var assigned = await nurse.PostAsJsonAsync(
            $"/api/admissions/{admissionId}/assign-bed", new { bed_id = beds[0] });
        using var body = await ReadJsonAsync(assigned);

        Assert.Equal(HttpStatusCode.OK, assigned.StatusCode);
        Assert.Equal("reserved", body.RootElement.GetProperty("status").GetString());
        Assert.Equal(ward.Name, body.RootElement.GetProperty("ward_name").GetString());
        Assert.Equal("B1", body.RootElement.GetProperty("bed_number").GetString());

        // A human picked it, which is what the agent-performance report measures itself
        // against, and the person who picked it is the approver on the record.
        Assert.Equal("user", body.RootElement.GetProperty("assigned_by").GetString());
        Assert.False(body.RootElement.GetProperty("is_downgrade").GetBoolean());
        Assert.Equal(JsonValueKind.Null, body.RootElement.GetProperty("workflow_id").ValueKind);
        Assert.Equal(await NurseIdAsync(), body.RootElement.GetProperty("approved_by_staff_id").GetString());

        using var admission = await ReadJsonAsync(
            await nurse.GetAsync($"/api/admissions/{admissionId}"));

        Assert.Equal("bed_reserved", admission.RootElement.GetProperty("status").GetString());
        Assert.Equal(ward.Name, admission.RootElement.GetProperty("ward_name").GetString());
        Assert.Equal("B1", admission.RootElement.GetProperty("bed_number").GetString());
    }

    [Fact]
    public async Task The_hold_is_thirty_minutes_from_now_when_nobody_is_expected_at_a_time()
    {
        using var nurse = await ClientAsync(ApiApplication.NurseEmail);
        var ward = await NewWardAsync();
        var beds = await AddBedsAsync(ward, 1);
        var admissionId = await NewAdmissionAsync(nurse);

        var before = DateTimeOffset.UtcNow;
        using var body = await ReadJsonAsync(await nurse.PostAsJsonAsync(
            $"/api/admissions/{admissionId}/assign-bed", new { bed_id = beds[0] }));

        var reservedUntil = body.RootElement.GetProperty("reserved_until").GetDateTimeOffset();

        // Long enough for a human to decide, short enough that a bed held for a patient who is
        // not coming goes back to the pool without anyone having to notice.
        Assert.InRange(
            reservedUntil,
            before.AddMinutes(30),
            DateTimeOffset.UtcNow.AddMinutes(31));
    }

    [Fact]
    public async Task An_ambulance_due_in_an_hour_gets_a_hold_that_outlives_the_journey()
    {
        using var nurse = await ClientAsync(ApiApplication.NurseEmail);
        var ward = await NewWardAsync();
        var beds = await AddBedsAsync(ward, 1);
        var arrival = DateTimeOffset.UtcNow.AddHours(1);
        var admissionId = await NewAdmissionAsync(nurse, expectedArrival: arrival);

        using var body = await ReadJsonAsync(await nurse.PostAsJsonAsync(
            $"/api/admissions/{admissionId}/assign-bed", new { bed_id = beds[0] }));

        // Thirty minutes from *now* would expire before they got here, and the bed would be
        // given away while the ambulance was still on the road.
        Assert.InRange(
            body.RootElement.GetProperty("reserved_until").GetDateTimeOffset(),
            arrival.AddMinutes(29),
            arrival.AddMinutes(31));
    }

    [Fact]
    public async Task Somebody_running_late_gets_a_fresh_hold_and_not_an_expired_one()
    {
        using var nurse = await ClientAsync(ApiApplication.NurseEmail);
        var ward = await NewWardAsync();
        var beds = await AddBedsAsync(ward, 1);
        var admissionId = await NewAdmissionAsync(
            nurse, expectedArrival: DateTimeOffset.UtcNow.AddHours(-2));

        using var body = await ReadJsonAsync(await nurse.PostAsJsonAsync(
            $"/api/admissions/{admissionId}/assign-bed", new { bed_id = beds[0] }));

        // Arrival plus thirty minutes would already have passed, so the bed would be reserved
        // and free in the same instant.
        Assert.True(
            body.RootElement.GetProperty("reserved_until").GetDateTimeOffset() > DateTimeOffset.UtcNow);
    }

    [Fact]
    public async Task The_second_nurse_to_pick_the_same_bed_is_refused()
    {
        using var nurse = await ClientAsync(ApiApplication.NurseEmail);
        var ward = await NewWardAsync();
        var beds = await AddBedsAsync(ward, 1);
        var first = await NewAdmissionAsync(nurse);
        var second = await NewAdmissionAsync(nurse);

        var one = await nurse.PostAsJsonAsync(
            $"/api/admissions/{first}/assign-bed", new { bed_id = beds[0] });
        var two = await nurse.PostAsJsonAsync(
            $"/api/admissions/{second}/assign-bed", new { bed_id = beds[0] });
        using var body = await ReadJsonAsync(two);

        // The guarantee is ux_bed_assignments_live_bed, not a read: two nurses assigning bed 12
        // in the same instant both pass any "is it free?" check, however carefully written.
        Assert.Equal(HttpStatusCode.OK, one.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, two.StatusCode);
        Assert.Equal("cl_pat_014", body.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task A_bed_whose_hold_has_lapsed_can_be_given_to_somebody_else()
    {
        using var nurse = await ClientAsync(ApiApplication.NurseEmail);
        var ward = await NewWardAsync();
        var beds = await AddBedsAsync(ward, 1);
        await AssignAsync(beds[0]);
        await ExpireHoldAsync(beds[0]);

        var next = await NewAdmissionAsync(nurse);
        var assigned = await nurse.PostAsJsonAsync(
            $"/api/admissions/{next}/assign-bed", new { bed_id = beds[0] });

        // The index counts only live rows, so a lapsed hold does not stand in the way — which
        // is what makes expiry cost nothing when we guess wrong about an arrival.
        Assert.Equal(HttpStatusCode.OK, assigned.StatusCode);
    }

    [Fact]
    public async Task A_visit_already_holding_a_bed_is_not_given_a_second_one()
    {
        using var nurse = await ClientAsync(ApiApplication.NurseEmail);
        var ward = await NewWardAsync();
        var beds = await AddBedsAsync(ward, 2);
        var admissionId = await NewAdmissionAsync(nurse);

        await nurse.PostAsJsonAsync(
            $"/api/admissions/{admissionId}/assign-bed", new { bed_id = beds[0] });
        var again = await nurse.PostAsJsonAsync(
            $"/api/admissions/{admissionId}/assign-bed", new { bed_id = beds[1] });
        using var body = await ReadJsonAsync(again);

        // bed_reserved -> bed_reserved is not a move, so the workflow refuses it before the
        // index has to. Two beds held for one patient is a bed lost to everybody else.
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
        Assert.Equal("cl_err_409_transition", body.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task A_cancelled_visit_cannot_be_given_a_bed()
    {
        using var nurse = await ClientAsync(ApiApplication.NurseEmail);
        using var manager = await ClientAsync(ApiApplication.ManagerEmail);
        var ward = await NewWardAsync();
        var beds = await AddBedsAsync(ward, 1);
        var admissionId = await NewAdmissionAsync(nurse);

        await manager.PostAsJsonAsync(
            $"/api/admissions/{admissionId}/cancel", new { reason = "no_show" });

        var assigned = await nurse.PostAsJsonAsync(
            $"/api/admissions/{admissionId}/assign-bed", new { bed_id = beds[0] });

        Assert.Equal(HttpStatusCode.Conflict, assigned.StatusCode);
    }

    [Fact]
    public async Task A_bed_that_is_not_in_the_register_is_a_404()
    {
        using var nurse = await ClientAsync(ApiApplication.NurseEmail);
        var admissionId = await NewAdmissionAsync(nurse);

        var assigned = await nurse.PostAsJsonAsync(
            $"/api/admissions/{admissionId}/assign-bed", new { bed_id = Guid.NewGuid() });

        // A 404 and not a 409: nothing about the admission is wrong, the caller named a bed
        // Equipment Management has never had.
        Assert.Equal(HttpStatusCode.NotFound, assigned.StatusCode);
    }

    [Fact]
    public async Task An_admission_that_does_not_exist_is_a_404()
    {
        using var nurse = await ClientAsync(ApiApplication.NurseEmail);
        var ward = await NewWardAsync();
        var beds = await AddBedsAsync(ward, 1);

        var assigned = await nurse.PostAsJsonAsync(
            $"/api/admissions/{Guid.NewGuid()}/assign-bed", new { bed_id = beds[0] });

        Assert.Equal(HttpStatusCode.NotFound, assigned.StatusCode);
    }

    [Fact]
    public async Task A_body_with_no_bed_id_is_a_400()
    {
        using var nurse = await ClientAsync(ApiApplication.NurseEmail);
        var admissionId = await NewAdmissionAsync(nurse);

        var assigned = await nurse.PostAsJsonAsync(
            $"/api/admissions/{admissionId}/assign-bed", new { });

        Assert.Equal(HttpStatusCode.BadRequest, assigned.StatusCode);
    }

    [Fact]
    public async Task The_override_reason_is_stored_when_a_human_gives_one()
    {
        using var nurse = await ClientAsync(ApiApplication.NurseEmail);
        var ward = await NewWardAsync();
        var beds = await AddBedsAsync(ward, 1);
        var admissionId = await NewAdmissionAsync(nurse);

        using var body = await ReadJsonAsync(await nurse.PostAsJsonAsync(
            $"/api/admissions/{admissionId}/assign-bed",
            new { bed_id = beds[0], override_reason = "Family are already on this ward." }));

        // Accepting it and dropping it would be a field that looks saved and is not. It is
        // what the agent-performance report at step 11 reads to say why a human said no.
        Assert.Equal(
            "Family are already on this ward.",
            body.RootElement.GetProperty("override_reason").GetString());
    }

    // ---------- who may approve which bed ----------

    [Fact]
    public async Task A_nurse_may_put_an_icu_patient_in_an_icu_bed()
    {
        using var nurse = await ClientAsync(ApiApplication.NurseEmail);
        var ward = await NewWardAsync(wardType: "icu");
        var beds = await AddBedsAsync(ward, 1);
        var admissionId = await NewAdmissionAsync(nurse, category: "icu");

        var assigned = await nurse.PostAsJsonAsync(
            $"/api/admissions/{admissionId}/assign-bed", new { bed_id = beds[0] });

        // Changed 2026-09-12: this used to be a 403. The bed matches the care level exactly, so
        // there is no decision for anybody to make - and sending a nurse to find a duty manager
        // held up the most urgent admission in the hospital for a signature on the obvious.
        Assert.Equal(HttpStatusCode.OK, assigned.StatusCode);
    }

    [Fact]
    public async Task Reception_may_put_an_icu_patient_in_an_icu_bed()
    {
        using var nurse = await ClientAsync(ApiApplication.NurseEmail);
        using var reception = await ClientAsync(ApiApplication.ReceptionEmail);
        var ward = await NewWardAsync(wardType: "icu");
        var beds = await AddBedsAsync(ward, 1);
        var admissionId = await NewAdmissionAsync(nurse, category: "icu");

        var assigned = await reception.PostAsJsonAsync(
            $"/api/admissions/{admissionId}/assign-bed", new { bed_id = beds[0] });

        Assert.Equal(HttpStatusCode.OK, assigned.StatusCode);
    }

    [Fact]
    public async Task A_duty_manager_may()
    {
        using var nurse = await ClientAsync(ApiApplication.NurseEmail);
        using var manager = await ClientAsync(ApiApplication.ManagerEmail);
        var ward = await NewWardAsync(wardType: "icu");
        var beds = await AddBedsAsync(ward, 1);
        var admissionId = await NewAdmissionAsync(nurse, category: "icu");

        var assigned = await manager.PostAsJsonAsync(
            $"/api/admissions/{admissionId}/assign-bed", new { bed_id = beds[0] });
        using var body = await ReadJsonAsync(assigned);

        Assert.Equal(HttpStatusCode.OK, assigned.StatusCode);
        Assert.False(body.RootElement.GetProperty("is_downgrade").GetBoolean());
    }

    [Fact]
    public async Task A_nurse_may_not_downgrade_an_icu_patient_into_a_general_bed()
    {
        using var nurse = await ClientAsync(ApiApplication.NurseEmail);
        var ward = await NewWardAsync();
        var beds = await AddBedsAsync(ward, 1);
        var admissionId = await NewAdmissionAsync(nurse, category: "icu");

        var assigned = await nurse.PostAsJsonAsync(
            $"/api/admissions/{admissionId}/assign-bed", new { bed_id = beds[0] });
        using var body = await ReadJsonAsync(assigned);

        Assert.Equal(HttpStatusCode.Forbidden, assigned.StatusCode);
        Assert.Equal("cl_pat_013", body.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task A_duty_manager_downgrading_gets_it_recorded_as_a_downgrade()
    {
        using var nurse = await ClientAsync(ApiApplication.NurseEmail);
        using var manager = await ClientAsync(ApiApplication.ManagerEmail);
        var ward = await NewWardAsync();
        var beds = await AddBedsAsync(ward, 1);
        var admissionId = await NewAdmissionAsync(nurse, category: "icu");

        using var body = await ReadJsonAsync(await manager.PostAsJsonAsync(
            $"/api/admissions/{admissionId}/assign-bed", new { bed_id = beds[0] }));

        // Nobody may put an ICU patient in a general bed *without it being recorded* as a
        // downgrade. The flag is a column and not a note, because it routes the approval.
        Assert.True(body.RootElement.GetProperty("is_downgrade").GetBoolean());
    }

    [Fact]
    public async Task An_ordinary_patient_is_not_put_into_an_icu_bed_by_a_nurse()
    {
        using var nurse = await ClientAsync(ApiApplication.NurseEmail);
        var ward = await NewWardAsync(wardType: "icu");
        var beds = await AddBedsAsync(ward, 1);
        var admissionId = await NewAdmissionAsync(nurse, category: "inpatient");

        var assigned = await nurse.PostAsJsonAsync(
            $"/api/admissions/{admissionId}/assign-bed", new { bed_id = beds[0] });
        using var body = await ReadJsonAsync(assigned);

        // A 403 and not a 409. Spending an intensive-care bed on somebody who does not need one
        // is a decision, and it is the duty manager's - so the objection is to who is asking.
        Assert.Equal(HttpStatusCode.Forbidden, assigned.StatusCode);
        Assert.Equal("cl_pat_012", body.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task A_duty_manager_may_put_an_ordinary_patient_into_an_icu_bed()
    {
        using var nurse = await ClientAsync(ApiApplication.NurseEmail);
        using var manager = await ClientAsync(ApiApplication.ManagerEmail);
        var ward = await NewWardAsync(wardType: "icu");
        var beds = await AddBedsAsync(ward, 1);
        var admissionId = await NewAdmissionAsync(nurse, category: "inpatient");

        var assigned = await manager.PostAsJsonAsync(
            $"/api/admissions/{admissionId}/assign-bed", new { bed_id = beds[0] });
        using var body = await ReadJsonAsync(assigned);

        // Hard rule H2 upward, overruled. The person who carries the cost of an empty
        // intensive-care bed is the person who may spend one - an overflowing general ward with
        // an empty ICU next to it is a real night in a real hospital.
        Assert.Equal(HttpStatusCode.OK, assigned.StatusCode);

        // Not a downgrade: they are getting more care than assessed, not less.
        Assert.False(body.RootElement.GetProperty("is_downgrade").GetBoolean());
    }

    // ---------- who may place a patient at all ----------

    [Fact]
    public async Task Reception_may_assign_a_bed_that_matches_the_care_level()
    {
        using var nurse = await ClientAsync(ApiApplication.NurseEmail);
        using var reception = await ClientAsync(ApiApplication.ReceptionEmail);
        var ward = await NewWardAsync();
        var beds = await AddBedsAsync(ward, 1);
        var admissionId = await NewAdmissionAsync(nurse, category: "inpatient");

        var assigned = await reception.PostAsJsonAsync(
            $"/api/admissions/{admissionId}/assign-bed", new { bed_id = beds[0] });

        // A walk-in is registered, admitted and bedded by the person standing at the desk.
        Assert.Equal(HttpStatusCode.OK, assigned.StatusCode);
    }

    [Fact]
    public async Task Reception_may_not_assign_a_bed_that_does_not_match_the_care_level()
    {
        using var nurse = await ClientAsync(ApiApplication.NurseEmail);
        using var reception = await ClientAsync(ApiApplication.ReceptionEmail);
        var ward = await NewWardAsync();
        var beds = await AddBedsAsync(ward, 1);
        var admissionId = await NewAdmissionAsync(nurse, category: "icu");

        var assigned = await reception.PostAsJsonAsync(
            $"/api/admissions/{admissionId}/assign-bed", new { bed_id = beds[0] });
        using var body = await ReadJsonAsync(assigned);

        // Reaching the route is not permission to choose any bed on it. Widening the policy
        // widened who may place a patient, not where - and an ICU patient in a general bed is
        // a decision about giving somebody less care than a clinician asked for.
        Assert.Equal(HttpStatusCode.Forbidden, assigned.StatusCode);
        Assert.Equal("cl_pat_013", body.RootElement.GetProperty("code").GetString());
    }

    // ---------- the hard rules ----------

    [Fact]
    public async Task A_children_ward_takes_a_child()
    {
        using var nurse = await ClientAsync(ApiApplication.NurseEmail);
        var ward = await NewWardAsync(wardType: "pediatric");
        var beds = await AddBedsAsync(ward, 1);

        var admissionId = await NewAdmissionAsync(
            nurse, dateOfBirth: DateTime.UtcNow.AddYears(-9).ToString("yyyy-MM-dd"));

        var assigned = await nurse.PostAsJsonAsync(
            $"/api/admissions/{admissionId}/assign-bed", new { bed_id = beds[0] });

        Assert.Equal(HttpStatusCode.OK, assigned.StatusCode);
    }

    [Fact]
    public async Task A_children_ward_refuses_an_adult()
    {
        using var nurse = await ClientAsync(ApiApplication.NurseEmail);
        using var manager = await ClientAsync(ApiApplication.ManagerEmail);
        var ward = await NewWardAsync(wardType: "pediatric");
        var beds = await AddBedsAsync(ward, 1);

        var admissionId = await NewAdmissionAsync(
            nurse, dateOfBirth: DateTime.UtcNow.AddYears(-34).ToString("yyyy-MM-dd"));

        // Asked as the duty manager on purpose: H6 is a property of the ward, like the gender
        // policy, so there is nobody who may overrule it.
        var assigned = await manager.PostAsJsonAsync(
            $"/api/admissions/{admissionId}/assign-bed", new { bed_id = beds[0] });
        using var body = await ReadJsonAsync(assigned);

        Assert.Equal(HttpStatusCode.Conflict, assigned.StatusCode);
        Assert.Equal("cl_pat_030", body.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task A_children_ward_refuses_a_patient_with_no_date_of_birth()
    {
        using var nurse = await ClientAsync(ApiApplication.NurseEmail);
        var ward = await NewWardAsync(wardType: "pediatric");
        var beds = await AddBedsAsync(ward, 1);
        var admissionId = await NewAdmissionAsync(nurse);

        var assigned = await nurse.PostAsJsonAsync(
            $"/api/admissions/{admissionId}/assign-bed", new { bed_id = beds[0] });
        using var body = await ReadJsonAsync(assigned);

        // Unknown reads as adult, the same way an unknown gender reaches only a mixed ward: the
        // narrower place takes a recorded fact to earn, not the absence of one.
        Assert.Equal(HttpStatusCode.Conflict, assigned.StatusCode);
        Assert.Equal("cl_pat_030", body.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task A_child_may_still_be_placed_outside_a_children_ward()
    {
        using var nurse = await ClientAsync(ApiApplication.NurseEmail);
        var ward = await NewWardAsync();
        var beds = await AddBedsAsync(ward, 1);

        var admissionId = await NewAdmissionAsync(
            nurse, dateOfBirth: DateTime.UtcNow.AddYears(-4).ToString("yyyy-MM-dd"));

        var assigned = await nurse.PostAsJsonAsync(
            $"/api/admissions/{admissionId}/assign-bed", new { bed_id = beds[0] });

        // One-directional. H6 closes the children's ward to adults; it does not confine
        // children to it, or a child needing intensive care could not be given it.
        Assert.Equal(HttpStatusCode.OK, assigned.StatusCode);
    }

    [Fact]
    public async Task A_bed_out_of_service_is_refused()
    {
        using var nurse = await ClientAsync(ApiApplication.NurseEmail);
        var ward = await NewWardAsync();
        var beds = await AddBedsAsync(ward, 1);
        await WithdrawAsync(beds[0]);
        var admissionId = await NewAdmissionAsync(nurse);

        var assigned = await nurse.PostAsJsonAsync(
            $"/api/admissions/{admissionId}/assign-bed", new { bed_id = beds[0] });
        using var body = await ReadJsonAsync(assigned);

        // H1. Equipment Management withdraws beds for repair and we never overrule that.
        Assert.Equal(HttpStatusCode.Conflict, assigned.StatusCode);
        Assert.Equal("cl_pat_015", body.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task A_male_only_ward_does_not_take_a_female_patient()
    {
        using var nurse = await ClientAsync(ApiApplication.NurseEmail);
        var ward = await NewWardAsync(genderPolicy: "male");
        var beds = await AddBedsAsync(ward, 1);
        var admissionId = await NewAdmissionAsync(nurse, gender: "female");

        var assigned = await nurse.PostAsJsonAsync(
            $"/api/admissions/{admissionId}/assign-bed", new { bed_id = beds[0] });
        using var body = await ReadJsonAsync(assigned);

        // H3. Gender separation is a property of the ward, applied to every admission the same
        // way, so there is no emergency exception to make here.
        Assert.Equal(HttpStatusCode.Conflict, assigned.StatusCode);
        Assert.Equal("cl_pat_017", body.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task An_unidentified_arrival_reaches_a_mixed_ward_and_not_a_single_sex_one()
    {
        using var nurse = await ClientAsync(ApiApplication.NurseEmail);
        var single = await NewWardAsync(genderPolicy: "female");
        var mixed = await NewWardAsync();
        var singleBeds = await AddBedsAsync(single, 1);
        var mixedBeds = await AddBedsAsync(mixed, 1);

        var refused = await NewAdmissionAsync(nurse, gender: "unknown");
        var allowed = await NewAdmissionAsync(nurse, gender: "unknown");

        var one = await nurse.PostAsJsonAsync(
            $"/api/admissions/{refused}/assign-bed", new { bed_id = singleBeds[0] });
        var two = await nurse.PostAsJsonAsync(
            $"/api/admissions/{allowed}/assign-bed", new { bed_id = mixedBeds[0] });

        // Exactly the case Gender.Unknown was added for: a patient nobody has identified lands
        // somewhere by rule, rather than on a guess about which single-sex ward they belong in.
        Assert.Equal(HttpStatusCode.Conflict, one.StatusCode);
        Assert.Equal(HttpStatusCode.OK, two.StatusCode);
    }

    [Fact]
    public async Task An_infectious_patient_needs_a_bed_that_can_isolate_them()
    {
        using var nurse = await ClientAsync(ApiApplication.NurseEmail);
        var ward = await NewWardAsync();
        var plain = await AddBedsAsync(ward, 1);
        var isolating = await AddBedsAsync(ward, 1, hasIsolation: true, firstNumber: 2);

        var refused = await NewAdmissionAsync(nurse, isInfectious: true);
        var allowed = await NewAdmissionAsync(nurse, isInfectious: true);

        var one = await nurse.PostAsJsonAsync(
            $"/api/admissions/{refused}/assign-bed", new { bed_id = plain[0] });
        using var body = await ReadJsonAsync(one);
        var two = await nurse.PostAsJsonAsync(
            $"/api/admissions/{allowed}/assign-bed", new { bed_id = isolating[0] });

        // H4, and the flag is on the bed rather than the ward: a side room is not only found in
        // an isolation ward.
        Assert.Equal(HttpStatusCode.Conflict, one.StatusCode);
        Assert.Equal("cl_pat_018", body.RootElement.GetProperty("code").GetString());
        Assert.Equal(HttpStatusCode.OK, two.StatusCode);
    }

    [Fact]
    public async Task A_bed_in_a_retired_ward_is_refused()
    {
        using var nurse = await ClientAsync(ApiApplication.NurseEmail);
        var ward = await NewWardAsync();
        var beds = await AddBedsAsync(ward, 1);
        await RetireWardAsync(ward);
        var admissionId = await NewAdmissionAsync(nurse);

        var assigned = await nurse.PostAsJsonAsync(
            $"/api/admissions/{admissionId}/assign-bed", new { bed_id = beds[0] });
        using var body = await ReadJsonAsync(assigned);

        // H5. The bed is real, so this is not a 404 — its ward has been closed, so nobody can
        // be admitted into it.
        Assert.Equal(HttpStatusCode.Conflict, assigned.StatusCode);
        Assert.Equal("cl_pat_019", body.RootElement.GetProperty("code").GetString());
    }

    // ---------- is anyone in this bed ----------

    [Fact]
    public async Task An_empty_bed_may_be_taken_out_of_service()
    {
        using var equipment = await ClientAsync(ApiApplication.EquipmentEmail);
        var ward = await NewWardAsync();
        var beds = await AddBedsAsync(ward, 1);

        using var body = await ReadJsonAsync(
            await equipment.GetAsync($"/api/beds/{beds[0]}/occupancy"));

        Assert.Equal(beds[0].ToString(), body.RootElement.GetProperty("bed_id").GetString());
        Assert.False(body.RootElement.GetProperty("occupied").GetBoolean());
        Assert.True(body.RootElement.GetProperty("may_take_out_of_service").GetBoolean());
        Assert.Equal(JsonValueKind.Null, body.RootElement.GetProperty("assignment_status").ValueKind);
        Assert.Equal(JsonValueKind.Null, body.RootElement.GetProperty("reserved_until").ValueKind);
    }

    [Fact]
    public async Task A_bed_with_a_patient_in_it_may_not_be()
    {
        using var equipment = await ClientAsync(ApiApplication.EquipmentEmail);
        var ward = await NewWardAsync();
        var beds = await AddBedsAsync(ward, 1);
        await OccupyAsync(beds[0]);

        using var body = await ReadJsonAsync(
            await equipment.GetAsync($"/api/beds/{beds[0]}/occupancy"));

        // The whole reason this endpoint exists: maintenance never evicts a patient.
        Assert.True(body.RootElement.GetProperty("occupied").GetBoolean());
        Assert.False(body.RootElement.GetProperty("may_take_out_of_service").GetBoolean());
        Assert.Equal("occupied", body.RootElement.GetProperty("assignment_status").GetString());

        // No expiry on an occupied row. That is what /arrive strips, so the thirty-minute clock
        // cannot take a bed back from somebody lying in it.
        Assert.Equal(JsonValueKind.Null, body.RootElement.GetProperty("reserved_until").ValueKind);
    }

    [Fact]
    public async Task A_live_hold_blocks_servicing_and_says_when_it_lapses()
    {
        using var equipment = await ClientAsync(ApiApplication.EquipmentEmail);
        var ward = await NewWardAsync();
        var beds = await AddBedsAsync(ward, 1);
        await AssignAsync(beds[0]);

        using var body = await ReadJsonAsync(
            await equipment.GetAsync($"/api/beds/{beds[0]}/occupancy"));

        Assert.True(body.RootElement.GetProperty("occupied").GetBoolean());
        Assert.False(body.RootElement.GetProperty("may_take_out_of_service").GetBoolean());
        Assert.Equal("reserved", body.RootElement.GetProperty("assignment_status").GetString());
        Assert.NotEqual(JsonValueKind.Null, body.RootElement.GetProperty("reserved_until").ValueKind);
    }

    [Fact]
    public async Task A_lapsed_hold_does_not_block_servicing()
    {
        using var equipment = await ClientAsync(ApiApplication.EquipmentEmail);
        var ward = await NewWardAsync();
        var beds = await AddBedsAsync(ward, 1);
        await AssignAsync(beds[0]);
        await ExpireHoldAsync(beds[0]);

        using var body = await ReadJsonAsync(
            await equipment.GetAsync($"/api/beds/{beds[0]}/occupancy"));

        // Nobody is in the bed, so nothing is being evicted. The same expiry rule the candidate
        // list and the capacity counts use, from the same place.
        Assert.False(body.RootElement.GetProperty("occupied").GetBoolean());
        Assert.True(body.RootElement.GetProperty("may_take_out_of_service").GetBoolean());
    }

    [Fact]
    public async Task A_bed_nobody_has_heard_of_is_a_404_and_not_a_confident_free()
    {
        using var equipment = await ClientAsync(ApiApplication.EquipmentEmail);

        var response = await equipment.GetAsync($"/api/beds/{Guid.NewGuid()}/occupancy");

        // "Free" is the one direction this endpoint must never be wrong in, so a bed we cannot
        // find is an error rather than a green light.
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Equipment_can_now_withdraw_an_empty_bed_and_still_not_an_occupied_one()
    {
        using var equipment = await ClientAsync(ApiApplication.EquipmentEmail);
        var ward = await NewWardAsync();
        var beds = await AddBedsAsync(ward, 2);
        await OccupyAsync(beds[1]);

        var empty = await equipment.PatchAsJsonAsync(
            $"/api/beds/{beds[0]}", new { condition = "out_of_service" });
        var occupied = await equipment.PatchAsJsonAsync(
            $"/api/beds/{beds[1]}", new { condition = "out_of_service" });
        using var body = await ReadJsonAsync(occupied);

        // This is what retiring STUBS.md row 3 bought. The stub answered "occupied" for every
        // bed, so *neither* of these worked; a stub answering "free" would have let both, and
        // maintenance would have been booked on a bed with a patient in it.
        Assert.Equal(HttpStatusCode.OK, empty.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, occupied.StatusCode);
        Assert.Equal("cl_equ_003", body.RootElement.GetProperty("code").GetString());
    }

    // ---------- correcting a bed chosen by mistake ----------

    [Fact]
    public async Task Correcting_a_bed_frees_the_wrong_one_and_claims_the_right_one()
    {
        using var nurse = await ClientAsync(ApiApplication.NurseEmail);
        var ward = await NewWardAsync();
        var beds = await AddBedsAsync(ward, 2);
        var admissionId = await AssignAsync(beds[0]);

        using var corrected = await ReadJsonAsync(await nurse.PostAsJsonAsync(
            $"/api/admissions/{admissionId}/correct-bed",
            new { bed_id = beds[1], reason = "picked the row above" }));

        Assert.Equal(beds[1].ToString(), corrected.RootElement.GetProperty("bed_id").GetString());

        // The wrong bed goes back on the board immediately. Leaving it claimed is how a ward
        // ends up with a bed nobody can use and nobody can explain.
        var wrong = await BedRowAsync(nurse, ward, beds[0]);
        var right = await BedRowAsync(nurse, ward, beds[1]);

        Assert.Equal("free", wrong.GetProperty("availability").GetString());
        Assert.Equal("reserved", right.GetProperty("availability").GetString());
        Assert.Equal(admissionId, right.GetProperty("occupied_by_admission_id").GetString());
    }

    [Fact]
    public async Task A_corrected_hold_keeps_being_a_hold_and_an_occupied_bed_keeps_being_occupied()
    {
        using var nurse = await ClientAsync(ApiApplication.NurseEmail);
        var ward = await NewWardAsync();
        var beds = await AddBedsAsync(ward, 2);
        var admissionId = await OccupyAsync(beds[0]);

        using var corrected = await ReadJsonAsync(await nurse.PostAsJsonAsync(
            $"/api/admissions/{admissionId}/correct-bed", new { bed_id = beds[1] }));

        // The patient did not get out of bed because the paperwork was wrong. Status carries
        // over, and so does occupied_at - which is what the bill is priced from.
        Assert.Equal("occupied", corrected.RootElement.GetProperty("status").GetString());
        Assert.Equal(
            JsonValueKind.Null, corrected.RootElement.GetProperty("reserved_until").ValueKind);
        Assert.Equal("admitted", await StatusAsync(admissionId));
    }

    [Fact]
    public async Task A_corrected_bed_is_charged_for_nothing()
    {
        using var nurse = await ClientAsync(ApiApplication.NurseEmail);
        using var reception = await ClientAsync(ApiApplication.ReceptionEmail);
        var ward = await NewWardAsync();
        var beds = await AddBedsAsync(ward, 2);
        var admissionId = await OccupyAsync(beds[0]);

        await nurse.PostAsJsonAsync(
            $"/api/admissions/{admissionId}/correct-bed", new { bed_id = beds[1] });

        using var bill = await ReadJsonAsync(
            await reception.PostAsync($"/api/admissions/{admissionId}/bill", null));

        // The whole point of a correction being its own release reason. Every stay bills a
        // minimum of one day, so two bed lines for one mistake is two nights charged for one.
        var bedLines = bill.RootElement.GetProperty("lines").EnumerateArray()
            .Where(line => line.GetProperty("source").GetString() == "bed_stay")
            .ToList();

        Assert.Single(bedLines);
        Assert.Equal(1m, bedLines[0].GetProperty("quantity").GetDecimal());
    }

    [Fact]
    public async Task Correcting_a_bed_obeys_the_same_rules_as_choosing_one()
    {
        using var nurse = await ClientAsync(ApiApplication.NurseEmail);
        var general = await NewWardAsync();
        var icu = await NewWardAsync("icu");
        var generalBeds = await AddBedsAsync(general, 1);
        var icuBeds = await AddBedsAsync(icu, 1);
        var admissionId = await AssignAsync(generalBeds[0]);

        var refused = await nurse.PostAsJsonAsync(
            $"/api/admissions/{admissionId}/correct-bed", new { bed_id = icuBeds[0] });

        using var problem = await ReadJsonAsync(refused);

        // Correcting a bed is not a side door to a bed this person may not choose. Same code a
        // straight assignment would have answered with.
        Assert.Equal(HttpStatusCode.Forbidden, refused.StatusCode);
        Assert.Equal("cl_pat_012", problem.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task There_is_nothing_to_correct_for_a_patient_who_holds_no_bed()
    {
        using var nurse = await ClientAsync(ApiApplication.NurseEmail);
        var ward = await NewWardAsync();
        var beds = await AddBedsAsync(ward, 1);
        var admissionId = await NewAdmissionAsync(nurse);

        var refused = await nurse.PostAsJsonAsync(
            $"/api/admissions/{admissionId}/correct-bed", new { bed_id = beds[0] });

        using var problem = await ReadJsonAsync(refused);

        // Not a 404 - the admission is real and so is the bed. What this caller wants is
        // /assign-bed, and saying so beats a generic conflict.
        Assert.Equal(HttpStatusCode.Conflict, refused.StatusCode);
        Assert.Equal("cl_pat_028", problem.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Correcting_a_bed_to_the_one_they_are_already_in_is_refused()
    {
        using var nurse = await ClientAsync(ApiApplication.NurseEmail);
        var ward = await NewWardAsync();
        var beds = await AddBedsAsync(ward, 1);
        var admissionId = await AssignAsync(beds[0]);

        var refused = await nurse.PostAsJsonAsync(
            $"/api/admissions/{admissionId}/correct-bed", new { bed_id = beds[0] });

        using var problem = await ReadJsonAsync(refused);

        // Releasing and re-taking the same bed would throw away how long they have been in it
        // for no gain at all.
        Assert.Equal(HttpStatusCode.Conflict, refused.StatusCode);
        Assert.Equal("cl_pat_029", problem.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task A_bed_assignment_says_who_approved_it_by_name_and_not_only_by_id()
    {
        using var nurse = await ClientAsync(ApiApplication.NurseEmail);
        var ward = await NewWardAsync();
        var beds = await AddBedsAsync(ward, 1);
        var admissionId = await NewAdmissionAsync(nurse);

        using var body = await ReadJsonAsync(await nurse.PostAsJsonAsync(
            $"/api/admissions/{admissionId}/assign-bed", new { bed_id = beds[0] }));

        // The id was always there and a screen cannot read it. Accountability nobody can read
        // is not accountability.
        Assert.Equal(
            await NurseIdAsync(), body.RootElement.GetProperty("approved_by_staff_id").GetString());
        Assert.Equal(
            "WardNurse Test", body.RootElement.GetProperty("approved_by_staff_name").GetString());
    }

    // ---------- access ----------

    [Fact]
    public async Task Any_staff_role_may_read_both_lists_because_two_components_depend_on_them()
    {
        using var doctor = await ClientAsync(ApiApplication.DoctorEmail);
        using var equipment = await ClientAsync(ApiApplication.EquipmentEmail);
        var ward = await NewWardAsync();
        var beds = await AddBedsAsync(ward, 1);

        Assert.Equal(
            HttpStatusCode.OK, (await doctor.GetAsync("/api/bed-availability")).StatusCode);
        Assert.Equal(
            HttpStatusCode.OK,
            (await equipment.GetAsync($"/api/beds/{beds[0]}/occupancy")).StatusCode);
    }

    [Fact]
    public async Task Nobody_reads_or_writes_any_of_the_three_without_a_token()
    {
        using var anonymous = _application.CreateClient();

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            (await anonymous.GetAsync("/api/bed-availability")).StatusCode);
        Assert.Equal(
            HttpStatusCode.Unauthorized,
            (await anonymous.GetAsync($"/api/beds/{Guid.NewGuid()}/occupancy")).StatusCode);
        Assert.Equal(
            HttpStatusCode.Unauthorized,
            (await anonymous.PostAsJsonAsync(
                $"/api/admissions/{Guid.NewGuid()}/assign-bed",
                new { bed_id = Guid.NewGuid() })).StatusCode);
    }

    [Fact]
    public async Task A_doctor_reads_the_candidate_list_but_does_not_assign_a_bed()
    {
        using var doctor = await ClientAsync(ApiApplication.DoctorEmail);
        var ward = await NewWardAsync();
        var beds = await AddBedsAsync(ward, 1);

        var assigned = await doctor.PostAsJsonAsync(
            $"/api/admissions/{Guid.NewGuid()}/assign-bed", new { bed_id = beds[0] });

        // A doctor decides the care level, not which bed. Placing patients is the desk's job,
        // and this is the BedAssigner policy refusing on the route rather than on the body.
        Assert.Equal(HttpStatusCode.Forbidden, assigned.StatusCode);
    }

    // ---------- helpers ----------

    private sealed record TestWard(Guid Id, string Name);

    private async Task<TestWard> NewWardAsync(
        string wardType = "general", string genderPolicy = "mixed", bool isActive = true)
    {
        using var administrator = await ClientAsync(ApiApplication.AdministratorEmail);
        var name = $"Bed-Ward-{Guid.NewGuid():N}"[..24];

        var created = await administrator.PostAsJsonAsync("/api/wards", new
        {
            name,
            ward_type = wardType,
            gender_policy = genderPolicy,
            is_active = isActive
        });

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        using var body = await ReadJsonAsync(created);
        return new TestWard(Guid.Parse(body.RootElement.GetProperty("id").GetString()!), name);
    }

    /// <summary>Registers real beds through Equipment Management's own endpoint. Their table, their write.</summary>
    private async Task<IReadOnlyList<Guid>> AddBedsAsync(
        TestWard ward, int count, bool hasIsolation = false, int firstNumber = 1)
    {
        using var equipment = await ClientAsync(ApiApplication.EquipmentEmail);
        var ids = new List<Guid>();

        for (var offset = 0; offset < count; offset++)
        {
            var number = firstNumber + offset;

            var created = await equipment.PostAsJsonAsync("/api/beds", new
            {
                ward_id = ward.Id,
                bed_number = $"B{number}",
                has_isolation = hasIsolation
            });

            Assert.Equal(HttpStatusCode.Created, created.StatusCode);

            using var body = await ReadJsonAsync(created);
            ids.Add(Guid.Parse(body.RootElement.GetProperty("id").GetString()!));
        }

        return ids;
    }

    /// <summary>
    /// Withdraws a bed by writing the column rather than through PATCH /api/beds/{id}.
    /// </summary>
    /// <remarks>
    /// The endpoint works now that row 3 of STUBS.md is retired, and one test above uses it on
    /// purpose. Everywhere else the column is written directly, so a test about *our* rules
    /// does not fail for a reason belonging to Equipment's.
    /// </remarks>
    private async Task WithdrawAsync(Guid bedId)
    {
        using var scope = _application.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CareLankaDbContext>();

        var bed = await db.Beds.FirstAsync(b => b.Id == bedId);
        bed.Condition = BedCondition.OutOfService;

        await db.SaveChangesAsync();
    }

    /// <summary>Retires a ward after its beds exist, which no endpoint offers.</summary>
    /// <remarks>
    /// POST /api/wards can create an inactive ward, but Equipment refuses to register a bed in
    /// a ward its directory cannot see. So a bed in a retired ward has to be built in that
    /// order, and this is the second half of it.
    /// </remarks>
    private async Task RetireWardAsync(TestWard ward)
    {
        using var scope = _application.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CareLankaDbContext>();

        var row = await db.Wards.FirstAsync(w => w.Id == ward.Id);
        row.IsActive = false;

        await db.SaveChangesAsync();
    }

    /// <summary>Pushes a live hold past its expiry, which no endpoint offers and no clock in a test can wait for.</summary>
    private async Task ExpireHoldAsync(Guid bedId)
    {
        using var scope = _application.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CareLankaDbContext>();

        var assignment = await db.BedAssignments
            .Where(a => a.BedId == bedId && a.Status == AssignmentStatus.Reserved)
            .FirstAsync();

        assignment.ReservedUntil = DateTimeOffset.UtcNow.AddMinutes(-1);

        await db.SaveChangesAsync();
    }

    /// <summary>The admission's current status, read back through the API.</summary>
    private async Task<string> StatusAsync(string admissionId)
    {
        using var nurse = await ClientAsync(ApiApplication.NurseEmail);
        using var body = await ReadJsonAsync(await nurse.GetAsync($"/api/admissions/{admissionId}"));

        return body.RootElement.GetProperty("status").GetString()!;
    }

    /// <summary>Holds a bed for a new admission through the endpoint, and answers the admission's id.</summary>
    private async Task<string> AssignAsync(Guid bedId)
    {
        using var nurse = await ClientAsync(ApiApplication.NurseEmail);
        var admissionId = await NewAdmissionAsync(nurse);

        var assigned = await nurse.PostAsJsonAsync(
            $"/api/admissions/{admissionId}/assign-bed", new { bed_id = bedId });

        Assert.Equal(HttpStatusCode.OK, assigned.StatusCode);

        return admissionId;
    }

    /// <summary>Puts a patient physically in a bed: hold it, then mark them arrived.</summary>
    private async Task<string> OccupyAsync(Guid bedId)
    {
        using var nurse = await ClientAsync(ApiApplication.NurseEmail);
        var admissionId = await AssignAsync(bedId);

        var arrived = await nurse.PostAsync($"/api/admissions/{admissionId}/arrive", null);

        Assert.Equal(HttpStatusCode.OK, arrived.StatusCode);

        return admissionId;
    }

    private async Task<string> NewAdmissionAsync(
        HttpClient nurse,
        string category = "inpatient",
        string gender = "male",
        bool isInfectious = false,
        DateTimeOffset? expectedArrival = null,
        string? dateOfBirth = null)
    {
        var patient = await nurse.PostAsJsonAsync("/api/patients", new
        {
            full_name = $"Bed Patient {Guid.NewGuid():N}"[..28],
            gender,
            nic = $"D{Guid.NewGuid():N}"[..12],
            date_of_birth = dateOfBirth
        });

        Assert.Equal(HttpStatusCode.Created, patient.StatusCode);

        using var patientBody = await ReadJsonAsync(patient);

        var created = await nurse.PostAsJsonAsync("/api/admissions", new
        {
            patient_id = patientBody.RootElement.GetProperty("id").GetString(),
            source = "walk_in",
            admission_category = category,
            category_set_by_staff_id = await NurseIdAsync(),
            urgency = "routine",
            is_infectious = isInfectious,
            expected_arrival = expectedArrival
        });

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        using var body = await ReadJsonAsync(created);
        return body.RootElement.GetProperty("id").GetString()!;
    }

    /// <summary>This bed's row in the candidate list. The list is every bed the collection ever made.</summary>
    private static async Task<JsonElement> BedRowAsync(HttpClient client, TestWard ward, Guid bedId)
    {
        var response = await client.GetAsync($"/api/bed-availability?wardId={ward.Id}&pageSize=100");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var body = await ReadJsonAsync(response);

        // Cloned: the JsonDocument dies at the end of this method and every JsonElement taken
        // from it dies with it, which reads as an ObjectDisposedException in the caller.
        return body.RootElement.GetProperty("items").EnumerateArray()
            .Single(row => row.GetProperty("id").GetString() == bedId.ToString())
            .Clone();
    }

    /// <summary>The ids the candidate list answers for one ward, in the order it answers them.</summary>
    private static async Task<IReadOnlyList<Guid>> BedIdsAsync(
        HttpClient client, TestWard ward, string query)
    {
        var response = await client.GetAsync(
            $"/api/bed-availability?wardId={ward.Id}&pageSize=100&{query}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var body = await ReadJsonAsync(response);

        return body.RootElement.GetProperty("items").EnumerateArray()
            .Select(row => Guid.Parse(row.GetProperty("id").GetString()!))
            .ToList();
    }

    // One token per account for the whole class, and one lookup of the nurse's own id.
    // /api/auth/login is rate limited per IP and every test class shares that budget.
    private static readonly SemaphoreSlim TokenLock = new(1, 1);
    private static readonly Dictionary<string, string> Tokens = new();
    private static string? _nurseId;

    private async Task<string> NurseIdAsync()
    {
        if (_nurseId is not null)
        {
            return _nurseId;
        }

        using var client = await ClientAsync(ApiApplication.NurseEmail);
        using var body = await ReadJsonAsync(await client.GetAsync("/api/auth/me"));

        return _nurseId = body.RootElement.GetProperty("id").GetString()!;
    }

    private async Task<HttpClient> ClientAsync(string email)
    {
        var client = _application.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", await TokenAsync(email));

        return client;
    }

    private async Task<string> TokenAsync(string email)
    {
        await TokenLock.WaitAsync();

        try
        {
            if (Tokens.TryGetValue(email, out var cached))
            {
                return cached;
            }

            using var client = _application.CreateClient();

            var response = await client.PostAsJsonAsync("/api/auth/login", new
            {
                email,
                password = ApiApplication.Password
            });

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            using var body = await ReadJsonAsync(response);
            var token = body.RootElement.GetProperty("access_token").GetString()!;

            Tokens[email] = token;
            return token;
        }
        finally
        {
            TokenLock.Release();
        }
    }

    private static async Task<JsonDocument> ReadJsonAsync(HttpResponseMessage response)
        => JsonDocument.Parse(await response.Content.ReadAsStringAsync());
}
