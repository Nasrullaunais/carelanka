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

[Collection(ApiCollection.Name)]
public sealed class AdmissionEndpointTests
{
    private readonly ApiApplication _application;

    public AdmissionEndpointTests(ApiApplication application) => _application = application;

    [Fact]
    public async Task A_nurse_starts_an_admission_and_it_begins_awaiting_a_bed()
    {
        using var client = await ClientAsync(ApiApplication.NurseEmail);
        var patientId = await NewPatientAsync(client, "Awaiting Bed");

        var created = await CreateAsync(client, patientId, await NurseIdAsync());
        using var body = await ReadJsonAsync(created);

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        Assert.Equal("awaiting_bed", body.RootElement.GetProperty("status").GetString());
        Assert.Equal("walk_in", body.RootElement.GetProperty("source").GetString());
        Assert.Equal(
            patientId,
            body.RootElement.GetProperty("patient").GetProperty("id").GetString());
    }

    [Fact]
    public async Task The_clinician_who_chose_the_care_level_is_stamped_on_the_record()
    {
        using var client = await ClientAsync(ApiApplication.NurseEmail);
        var nurseId = await NurseIdAsync();
        var patientId = await NewPatientAsync(client, "Category Stamp");

        using var body = await ReadJsonAsync(await CreateAsync(client, patientId, nurseId));

        Assert.Equal(nurseId, body.RootElement.GetProperty("category_set_by_staff_id").GetString());
        Assert.True(body.RootElement.TryGetProperty("category_set_at", out var setAt));
        Assert.NotEqual(JsonValueKind.Null, setAt.ValueKind);
    }

    [Fact]
    public async Task Outstanding_paperwork_is_named_field_by_field_and_not_merely_counted()
    {
        using var client = await ClientAsync(ApiApplication.NurseEmail);
        var patientId = await NewPatientAsync(client, "Half Registered", nic: NewNic());

        using var body = await ReadJsonAsync(await CreateAsync(client, patientId, await NurseIdAsync()));
        var missing = body.RootElement.GetProperty("missing_fields")
            .EnumerateArray().Select(item => item.GetString()!).ToArray();

        Assert.False(body.RootElement.GetProperty("details_complete").GetBoolean());
        Assert.Contains("phone", missing);
        Assert.Contains("address", missing);
        Assert.Contains("date_of_birth", missing);
        Assert.Contains("emergency_contact_name", missing);
        Assert.DoesNotContain("full_name", missing);
        Assert.DoesNotContain("nic", missing);
    }

    [Fact]
    public async Task A_fully_registered_patient_admits_with_no_outstanding_paperwork()
    {
        using var client = await ClientAsync(ApiApplication.NurseEmail);
        var patientId = await NewCompletePatientAsync(client, "Fully Registered");

        using var body = await ReadJsonAsync(await CreateAsync(client, patientId, await NurseIdAsync()));

        Assert.True(body.RootElement.GetProperty("details_complete").GetBoolean());
        Assert.Empty(body.RootElement.GetProperty("missing_fields").EnumerateArray());
    }

    [Fact]
    public async Task A_second_concurrent_admission_for_the_same_patient_is_a_409()
    {
        using var client = await ClientAsync(ApiApplication.NurseEmail);
        var nurseId = await NurseIdAsync();
        var patientId = await NewPatientAsync(client, "Already Inside");

        await CreateAsync(client, patientId, nurseId);
        var second = await CreateAsync(client, patientId, nurseId);
        using var body = await ReadJsonAsync(second);

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        Assert.Equal("cl_pat_006", body.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Two_admissions_started_at_the_same_instant_still_leave_the_patient_with_one()
    {
        using var client = await ClientAsync(ApiApplication.NurseEmail);
        var nurseId = await NurseIdAsync();
        var patientId = await NewPatientAsync(client, "Two Desks At Once");

        var responses = await Task.WhenAll(
            CreateAsync(client, patientId, nurseId),
            CreateAsync(client, patientId, nurseId));

        var codes = responses.Select(r => r.StatusCode).ToArray();

        Assert.Single(codes, HttpStatusCode.Created);
        Assert.Single(codes, HttpStatusCode.Conflict);

        using var conflict = await ReadJsonAsync(
            responses.Single(r => r.StatusCode == HttpStatusCode.Conflict));

        Assert.Equal("cl_pat_006", conflict.RootElement.GetProperty("code").GetString());

        using var history = await ReadJsonAsync(await client.GetAsync($"/api/patients/{patientId}"));
        Assert.Single(history.RootElement.GetProperty("admissions").EnumerateArray());

        foreach (var response in responses)
        {
            response.Dispose();
        }
    }

    [Fact]
    public async Task An_emergency_admission_without_the_dispatch_reference_is_a_400()
    {
        using var client = await ClientAsync(ApiApplication.NurseEmail);
        var patientId = await NewPatientAsync(client, "No Dispatch Reference");

        var response = await CreateAsync(client, patientId, await NurseIdAsync(), source: "emergency");
        using var body = await ReadJsonAsync(response);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("cl_pat_007", body.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task An_emergency_admission_with_a_dispatch_reference_is_accepted_and_keeps_it()
    {
        using var client = await ClientAsync(ApiApplication.NurseEmail);
        var patientId = await NewPatientAsync(client, "Ambulance Arrival");

        var response = await CreateAsync(
            client, patientId, await NurseIdAsync(), source: "emergency", dispatchId: "DISP-2026-0042");
        using var body = await ReadJsonAsync(response);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal("DISP-2026-0042", body.RootElement.GetProperty("dispatch_id").GetString());
    }

    [Fact]
    public async Task Admitting_a_patient_who_does_not_exist_is_a_404()
    {
        using var client = await ClientAsync(ApiApplication.NurseEmail);

        var response = await CreateAsync(client, Guid.NewGuid().ToString(), await NurseIdAsync());

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Who_chose_the_care_level_is_whoever_is_signed_in_whatever_the_body_claims()
    {
        using var client = await ClientAsync(ApiApplication.NurseEmail);
        var patientId = await NewPatientAsync(client, "Claimed Clinician");

        var response = await CreateAsync(client, patientId, Guid.NewGuid().ToString());
        using var body = await ReadJsonAsync(response);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(
            await NurseIdAsync(),
            body.RootElement.GetProperty("category_set_by_staff_id").GetString());
    }

    [Fact]
    public async Task An_admission_detail_carries_an_empty_bed_history_and_omits_what_has_no_table()
    {
        using var client = await ClientAsync(ApiApplication.NurseEmail);
        var patientId = await NewPatientAsync(client, "Detail View");
        var id = await CreateIdAsync(client, patientId, await NurseIdAsync());

        using var body = await ReadJsonAsync(await client.GetAsync($"/api/admissions/{id}"));

        Assert.Equal(JsonValueKind.Array, body.RootElement.GetProperty("bed_assignments").ValueKind);
        Assert.Empty(body.RootElement.GetProperty("bed_assignments").EnumerateArray());

        Assert.Equal(JsonValueKind.Null, body.RootElement.GetProperty("discharge").ValueKind);
        Assert.Equal(JsonValueKind.Null, body.RootElement.GetProperty("bill").ValueKind);

        Assert.False(body.RootElement.TryGetProperty("workflows", out _));
    }

    [Fact]
    public async Task An_unknown_admission_id_is_a_404_carrying_problem_json()
    {
        using var client = await ClientAsync(ApiApplication.NurseEmail);

        var response = await client.GetAsync($"/api/admissions/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task The_worklist_filter_finds_the_admissions_whose_paperwork_is_outstanding()
    {
        using var client = await ClientAsync(ApiApplication.NurseEmail);
        var nurseId = await NurseIdAsync();

        var incompleteName = $"Incomplete {Guid.NewGuid():N}"[..20];
        var completeName = $"Complete {Guid.NewGuid():N}"[..20];

        await CreateAsync(client, await NewPatientAsync(client, incompleteName), nurseId);
        await CreateAsync(client, await NewCompletePatientAsync(client, completeName), nurseId);

        using var outstanding = await ReadJsonAsync(
            await client.GetAsync("/api/admissions?detailsComplete=false&pageSize=100"));
        var names = PatientNames(outstanding);

        Assert.Contains(incompleteName, names);
        Assert.DoesNotContain(completeName, names);
    }

    [Fact]
    public async Task Filtering_by_status_narrows_the_list_and_an_unmatched_status_returns_nothing()
    {
        using var client = await ClientAsync(ApiApplication.NurseEmail);
        var name = $"Status Filter {Guid.NewGuid():N}"[..24];
        await CreateAsync(client, await NewPatientAsync(client, name), await NurseIdAsync());

        var awaitingResponse = await client.GetAsync("/api/admissions?status=awaiting_bed&pageSize=100");
        Assert.Equal(HttpStatusCode.OK, awaitingResponse.StatusCode);

        using var awaiting = await ReadJsonAsync(awaitingResponse);
        using var discharged = await ReadJsonAsync(
            await client.GetAsync("/api/admissions?status=discharged&pageSize=100"));

        Assert.Contains(name, PatientNames(awaiting));
        Assert.DoesNotContain(name, PatientNames(discharged));
    }

    [Fact]
    public async Task Sorting_by_urgency_ranks_by_how_urgent_it_is_and_not_alphabetically()
    {
        using var client = await ClientAsync(ApiApplication.NurseEmail);
        var nurseId = await NurseIdAsync();
        var marker = $"Urg{Guid.NewGuid():N}"[..12];

        await CreateAsync(client, await NewPatientAsync(client, $"{marker} routine"), nurseId, urgency: "routine");
        await CreateAsync(client, await NewPatientAsync(client, $"{marker} emergency"), nurseId, urgency: "emergency");
        await CreateAsync(client, await NewPatientAsync(client, $"{marker} urgent"), nurseId, urgency: "urgent");

        var response = await client.GetAsync(
            $"/api/admissions?search={marker}&sortBy=urgency&sortDir=desc&pageSize=100");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var body = await ReadJsonAsync(response);
        var order = body.RootElement.GetProperty("items").EnumerateArray()
            .Select(item => item.GetProperty("urgency").GetString()!).ToArray();

        Assert.Equal(new[] { "emergency", "urgent", "routine" }, order);
    }

    [Fact]
    public async Task Searching_matches_the_patients_name_through_the_admission()
    {
        using var client = await ClientAsync(ApiApplication.NurseEmail);
        var nic = NewNic();
        var name = $"Searchable {Guid.NewGuid():N}"[..22];

        await CreateAsync(client, await NewPatientAsync(client, name, nic), await NurseIdAsync());

        using var byName = await ReadJsonAsync(await client.GetAsync($"/api/admissions?search={name}"));
        using var byNic = await ReadJsonAsync(await client.GetAsync($"/api/admissions?search={nic}"));

        Assert.Equal(1, byName.RootElement.GetProperty("total_items").GetInt32());
        Assert.Equal(1, byNic.RootElement.GetProperty("total_items").GetInt32());
    }

    [Fact]
    public async Task An_unknown_sort_field_is_a_400_and_not_a_silently_ignored_parameter()
    {
        using var client = await ClientAsync(ApiApplication.NurseEmail);

        var response = await client.GetAsync("/api/admissions?sortBy=patient_name");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Completing_details_clears_the_fields_it_supplies_from_the_missing_list()
    {
        using var client = await ClientAsync(ApiApplication.NurseEmail);
        var patientId = await NewPatientAsync(client, "Relative Arrived", nic: NewNic());
        var id = await CreateIdAsync(client, patientId, await NurseIdAsync());

        var response = await client.PatchAsJsonAsync($"/api/admissions/{id}/details", new
        {
            phone = NewPhone(),
            date_of_birth = "1988-04-12"
        });

        using var body = await ReadJsonAsync(response);
        var missing = body.RootElement.GetProperty("missing_fields")
            .EnumerateArray().Select(item => item.GetString()!).ToArray();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.DoesNotContain("phone", missing);
        Assert.DoesNotContain("date_of_birth", missing);

        Assert.Contains("address", missing);
        Assert.False(body.RootElement.GetProperty("details_complete").GetBoolean());
    }

    [Fact]
    public async Task A_field_left_out_of_the_patch_is_left_alone_rather_than_cleared()
    {
        using var client = await ClientAsync(ApiApplication.NurseEmail);
        var nic = NewNic();
        var patientId = await NewPatientAsync(client, "Keeps Their Nic", nic);
        var id = await CreateIdAsync(client, patientId, await NurseIdAsync());

        await client.PatchAsJsonAsync($"/api/admissions/{id}/details", new { address = "12 Temple Road" });

        using var body = await ReadJsonAsync(await client.GetAsync($"/api/admissions/{id}"));

        Assert.Equal(nic, body.RootElement.GetProperty("patient").GetProperty("nic").GetString());
    }

    [Fact]
    public async Task Completing_details_onto_another_patients_nic_is_a_409_rather_than_a_silent_merge()
    {
        using var client = await ClientAsync(ApiApplication.NurseEmail);
        var taken = NewNic();

        await NewPatientAsync(client, "Owns The Nic", taken);
        var patientId = await NewPatientAsync(client, "Unidentified Arrival");
        var id = await CreateIdAsync(client, patientId, await NurseIdAsync());

        var response = await client.PatchAsJsonAsync($"/api/admissions/{id}/details", new { nic = taken });
        using var body = await ReadJsonAsync(response);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("cl_pat_002", body.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Completing_details_on_an_unknown_admission_is_a_404()
    {
        using var client = await ClientAsync(ApiApplication.NurseEmail);

        var response = await client.PatchAsJsonAsync(
            $"/api/admissions/{Guid.NewGuid()}/details", new { phone = NewPhone() });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task An_admission_with_no_care_level_is_refused_rather_than_filed_as_icu()
    {
        using var nurse = await ClientAsync(ApiApplication.NurseEmail);
        var patientId = await NewPatientAsync(nurse, "No Category");

        var created = await nurse.PostAsJsonAsync("/api/admissions", new
        {
            patient_id = patientId,
            source = "walk_in",
            category_set_by_staff_id = await NurseIdAsync(),
            urgency = "routine",
            is_infectious = false
        });

        Assert.Equal(HttpStatusCode.BadRequest, created.StatusCode);

        using var body = await ReadJsonAsync(created);
        Assert.Equal("cl_err_400", body.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task An_admission_with_no_source_is_refused_rather_than_filed_as_an_emergency()
    {
        using var nurse = await ClientAsync(ApiApplication.NurseEmail);
        var patientId = await NewPatientAsync(nurse, "No Source");

        var created = await nurse.PostAsJsonAsync("/api/admissions", new
        {
            patient_id = patientId,
            admission_category = "general",
            category_set_by_staff_id = await NurseIdAsync(),
            urgency = "routine",
            is_infectious = false
        });

        Assert.Equal(HttpStatusCode.BadRequest, created.StatusCode);
    }

    [Fact]
    public async Task An_admission_with_no_urgency_is_refused_rather_than_filed_as_routine()
    {
        using var nurse = await ClientAsync(ApiApplication.NurseEmail);
        var patientId = await NewPatientAsync(nurse, "No Urgency");

        var created = await nurse.PostAsJsonAsync("/api/admissions", new
        {
            patient_id = patientId,
            source = "walk_in",
            admission_category = "general",
            category_set_by_staff_id = await NurseIdAsync(),
            is_infectious = false
        });

        Assert.Equal(HttpStatusCode.BadRequest, created.StatusCode);
    }

    [Fact]
    public async Task A_held_bed_becomes_an_occupied_one_when_the_patient_walks_in()
    {
        using var nurse = await ClientAsync(ApiApplication.NurseEmail);
        var patientId = await NewPatientAsync(nurse, "Arrives On Time");
        var id = await CreateIdAsync(nurse, patientId, await NurseIdAsync());
        await ReserveABedAsync(id);

        var arrived = await nurse.PostAsync($"/api/admissions/{id}/arrive", null);
        using var body = await ReadJsonAsync(arrived);

        Assert.Equal(HttpStatusCode.OK, arrived.StatusCode);
        Assert.Equal("admitted", body.RootElement.GetProperty("status").GetString());

        Assert.NotEqual(
            JsonValueKind.Null,
            body.RootElement.GetProperty("admitted_at").ValueKind);

        using var detail = await ReadJsonAsync(await nurse.GetAsync($"/api/admissions/{id}"));
        var assignment = detail.RootElement.GetProperty("bed_assignments").EnumerateArray().Single();

        Assert.Equal("occupied", assignment.GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.Null, assignment.GetProperty("reserved_until").ValueKind);
    }

    [Fact]
    public async Task A_patient_cannot_arrive_into_a_bed_that_was_never_approved()
    {
        using var nurse = await ClientAsync(ApiApplication.NurseEmail);
        var patientId = await NewPatientAsync(nurse, "No Bed Yet");
        var id = await CreateIdAsync(nurse, patientId, await NurseIdAsync());

        var arrived = await nurse.PostAsync($"/api/admissions/{id}/arrive", null);
        using var body = await ReadJsonAsync(arrived);

        Assert.Equal(HttpStatusCode.Conflict, arrived.StatusCode);
        Assert.Equal("cl_err_409_transition", body.RootElement.GetProperty("code").GetString());

        Assert.Contains("awaiting_bed", body.RootElement.GetProperty("detail").GetString()!);
        Assert.Contains("admitted", body.RootElement.GetProperty("detail").GetString()!);
    }

    [Fact]
    public async Task Arriving_twice_is_refused_the_second_time()
    {
        using var nurse = await ClientAsync(ApiApplication.NurseEmail);
        var patientId = await NewPatientAsync(nurse, "Arrives Twice");
        var id = await CreateIdAsync(nurse, patientId, await NurseIdAsync());
        await ReserveABedAsync(id);

        var first = await nurse.PostAsync($"/api/admissions/{id}/arrive", null);
        var second = await nurse.PostAsync($"/api/admissions/{id}/arrive", null);

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task Marking_an_admission_that_does_not_exist_as_arrived_is_a_404()
    {
        using var nurse = await ClientAsync(ApiApplication.NurseEmail);

        var arrived = await nurse.PostAsync($"/api/admissions/{Guid.NewGuid()}/arrive", null);

        Assert.Equal(HttpStatusCode.NotFound, arrived.StatusCode);
    }

    [Fact]
    public async Task A_visit_can_be_called_off_before_the_patient_gets_here()
    {
        using var nurse = await ClientAsync(ApiApplication.NurseEmail);
        var patientId = await NewPatientAsync(nurse, "Never Turned Up");
        var id = await CreateIdAsync(nurse, patientId, await NurseIdAsync());

        using var manager = await ClientAsync(ApiApplication.ManagerEmail);
        var cancelled = await manager.PostAsJsonAsync($"/api/admissions/{id}/cancel", new
        {
            reason = "diverted_to_other_hospital",
            note = "Ambulance rerouted to Kandy, family informed."
        });

        using var body = await ReadJsonAsync(cancelled);

        Assert.Equal(HttpStatusCode.OK, cancelled.StatusCode);
        Assert.Equal("cancelled", body.RootElement.GetProperty("status").GetString());
        Assert.Equal(
            "diverted_to_other_hospital",
            body.RootElement.GetProperty("cancel_reason").GetString());

        Assert.Equal(
            "Ambulance rerouted to Kandy, family informed.",
            body.RootElement.GetProperty("cancel_note").GetString());
    }

    [Fact]
    public async Task Calling_a_visit_off_puts_the_bed_it_was_holding_back_in_the_pool()
    {
        using var nurse = await ClientAsync(ApiApplication.NurseEmail);
        var patientId = await NewPatientAsync(nurse, "Held A Bed");
        var id = await CreateIdAsync(nurse, patientId, await NurseIdAsync());
        await ReserveABedAsync(id);

        using var manager = await ClientAsync(ApiApplication.ManagerEmail);
        var cancelled = await manager.PostAsJsonAsync(
            $"/api/admissions/{id}/cancel", new { reason = "false_alarm" });

        Assert.Equal(HttpStatusCode.OK, cancelled.StatusCode);

        using var detail = await ReadJsonAsync(await nurse.GetAsync($"/api/admissions/{id}"));
        var assignment = detail.RootElement.GetProperty("bed_assignments").EnumerateArray().Single();

        Assert.Equal("released", assignment.GetProperty("status").GetString());
        Assert.Equal("cancelled", assignment.GetProperty("release_reason").GetString());
        Assert.Equal(JsonValueKind.Null, assignment.GetProperty("reserved_until").ValueKind);
    }

    [Fact]
    public async Task A_patient_lying_in_a_ward_cannot_be_cancelled()
    {
        using var nurse = await ClientAsync(ApiApplication.NurseEmail);
        var patientId = await NewPatientAsync(nurse, "Already In Bed");
        var id = await CreateIdAsync(nurse, patientId, await NurseIdAsync());
        await ReserveABedAsync(id);
        await nurse.PostAsync($"/api/admissions/{id}/arrive", null);

        using var manager = await ClientAsync(ApiApplication.ManagerEmail);
        var cancelled = await manager.PostAsJsonAsync(
            $"/api/admissions/{id}/cancel", new { reason = "patient_refused" });

        using var body = await ReadJsonAsync(cancelled);

        Assert.Equal(HttpStatusCode.Conflict, cancelled.StatusCode);
        Assert.Equal("cl_err_409_transition", body.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Cancelling_an_already_cancelled_visit_is_refused()
    {
        using var nurse = await ClientAsync(ApiApplication.NurseEmail);
        var patientId = await NewPatientAsync(nurse, "Cancelled Twice");
        var id = await CreateIdAsync(nurse, patientId, await NurseIdAsync());

        using var manager = await ClientAsync(ApiApplication.ManagerEmail);
        var first = await manager.PostAsJsonAsync(
            $"/api/admissions/{id}/cancel", new { reason = "no_show" });
        var second = await manager.PostAsJsonAsync(
            $"/api/admissions/{id}/cancel", new { reason = "false_alarm" });

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task A_cancellation_with_no_reason_is_refused_rather_than_filed_under_the_first_one()
    {
        using var nurse = await ClientAsync(ApiApplication.NurseEmail);
        var patientId = await NewPatientAsync(nurse, "Reasonless");
        var id = await CreateIdAsync(nurse, patientId, await NurseIdAsync());

        using var manager = await ClientAsync(ApiApplication.ManagerEmail);
        var missing = await manager.PostAsJsonAsync(
            $"/api/admissions/{id}/cancel", new { note = "she changed her mind" });
        var nonsense = await manager.PostAsJsonAsync(
            $"/api/admissions/{id}/cancel", new { reason = "bored" });

        Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, nonsense.StatusCode);
    }

    [Fact]
    public async Task Cancelling_an_admission_that_does_not_exist_is_a_404()
    {
        using var manager = await ClientAsync(ApiApplication.ManagerEmail);

        var cancelled = await manager.PostAsJsonAsync(
            $"/api/admissions/{Guid.NewGuid()}/cancel", new { reason = "no_show" });

        Assert.Equal(HttpStatusCode.NotFound, cancelled.StatusCode);
    }

    [Fact]
    public async Task A_cancelled_visit_leaves_the_patient_free_to_be_admitted_again()
    {
        using var nurse = await ClientAsync(ApiApplication.NurseEmail);
        var nurseId = await NurseIdAsync();
        var patientId = await NewPatientAsync(nurse, "Came Back Later");
        var id = await CreateIdAsync(nurse, patientId, nurseId);

        using var manager = await ClientAsync(ApiApplication.ManagerEmail);
        await manager.PostAsJsonAsync($"/api/admissions/{id}/cancel", new { reason = "no_show" });

        var again = await CreateAsync(nurse, patientId, nurseId);

        Assert.Equal(HttpStatusCode.Created, again.StatusCode);
    }

    [Fact]
    public async Task A_cancellation_and_an_arrival_in_the_same_instant_do_not_both_win()
    {
        using var nurse = await ClientAsync(ApiApplication.NurseEmail);
        var patientId = await NewPatientAsync(nurse, "Two People At Once");
        var id = await CreateIdAsync(nurse, patientId, await NurseIdAsync());
        await ReserveABedAsync(id);

        using var manager = await ClientAsync(ApiApplication.ManagerEmail);

        var arrive = nurse.PostAsync($"/api/admissions/{id}/arrive", null);
        var cancel = manager.PostAsJsonAsync(
            $"/api/admissions/{id}/cancel", new { reason = "died_en_route" });

        var codes = (await Task.WhenAll(arrive, cancel)).Select(r => r.StatusCode).ToArray();

        Assert.Single(codes, HttpStatusCode.OK);
        Assert.Single(codes, HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Starting_an_admission_is_403_for_an_administrator_and_401_without_a_token()
    {
        using var nurse = await ClientAsync(ApiApplication.NurseEmail);
        var patientId = await NewPatientAsync(nurse, "Role Check");
        var nurseId = await NurseIdAsync();

        using var anonymous = _application.CreateClient();
        var withoutToken = await CreateAsync(anonymous, patientId, nurseId);

        using var administrator = await ClientAsync(ApiApplication.AdministratorEmail);
        var wrongRole = await CreateAsync(administrator, patientId, nurseId);

        Assert.Equal(HttpStatusCode.Unauthorized, withoutToken.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, wrongRole.StatusCode);
    }

    [Fact]
    public async Task An_administrator_reads_the_worklist_as_well_as_the_register_but_still_cannot_write()
    {
        using var nurse = await ClientAsync(ApiApplication.NurseEmail);
        var patientId = await NewPatientAsync(nurse, "Administrator Reads");
        var id = await CreateIdAsync(nurse, patientId, await NurseIdAsync());

        using var administrator = await ClientAsync(ApiApplication.AdministratorEmail);

        var admissions = await administrator.GetAsync("/api/admissions");
        var patients = await administrator.GetAsync("/api/patients");

        var completed = await administrator.PatchAsJsonAsync(
            $"/api/admissions/{id}/details", new { address = "1 Admin Way" });

        Assert.Equal(HttpStatusCode.OK, admissions.StatusCode);
        Assert.Equal(HttpStatusCode.OK, patients.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, completed.StatusCode);
    }

    [Fact]
    public async Task A_doctor_may_read_the_worklist_but_may_not_complete_the_paperwork()
    {
        using var nurse = await ClientAsync(ApiApplication.NurseEmail);
        var patientId = await NewPatientAsync(nurse, "Doctor Reads");
        var id = await CreateIdAsync(nurse, patientId, await NurseIdAsync());

        using var doctor = await ClientAsync(ApiApplication.DoctorEmail);
        var list = await doctor.GetAsync("/api/admissions");
        var read = await doctor.GetAsync($"/api/admissions/{id}");
        var completed = await doctor.PatchAsJsonAsync(
            $"/api/admissions/{id}/details", new { address = "82 Galle Road, Colombo 03" });

        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        Assert.Equal(HttpStatusCode.OK, read.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, completed.StatusCode);
    }

    [Theory]
    [InlineData("nic", "!!!not-a-nic!!!")]
    [InlineData("nic", "12345")]
    [InlineData("phone", "12")]
    [InlineData("emergency_contact_phone", "071123456")]
    public async Task Completing_details_refuses_a_malformed_identifier(string field, string value)
    {
        using var nurse = await ClientAsync(ApiApplication.NurseEmail);
        var patientId = await NewPatientAsync(nurse, "Details Format Check");
        var id = await CreateIdAsync(nurse, patientId, await NurseIdAsync());

        var response = await nurse.PatchAsJsonAsync(
            $"/api/admissions/{id}/details",
            new Dictionary<string, string> { [field] = value });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        using var body = await ReadJsonAsync(response);
        Assert.True(body.RootElement.GetProperty("errors").TryGetProperty(field, out _));
    }

    [Fact]
    public async Task Completing_details_accepts_a_blank_field_as_leave_it_alone()
    {
        using var nurse = await ClientAsync(ApiApplication.NurseEmail);
        var patientId = await NewPatientAsync(nurse, "Details Blank Check");
        var id = await CreateIdAsync(nurse, patientId, await NurseIdAsync());

        var response = await nurse.PatchAsJsonAsync(
            $"/api/admissions/{id}/details", new { nic = "", phone = "  " });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task A_duty_manager_may_read_and_complete_an_admission()
    {
        using var nurse = await ClientAsync(ApiApplication.NurseEmail);
        var patientId = await NewPatientAsync(nurse, "Manager Reads");
        var id = await CreateIdAsync(nurse, patientId, await NurseIdAsync());

        using var manager = await ClientAsync(ApiApplication.ManagerEmail);
        var read = await manager.GetAsync($"/api/admissions/{id}");
        var completed = await manager.PatchAsJsonAsync(
            $"/api/admissions/{id}/details", new { address = "8 Hospital Lane" });

        Assert.Equal(HttpStatusCode.OK, read.StatusCode);
        Assert.Equal(HttpStatusCode.OK, completed.StatusCode);
    }

    [Fact]
    public async Task Marking_arrival_belongs_to_the_desk_and_the_ward_and_nobody_else()
    {
        using var nurse = await ClientAsync(ApiApplication.NurseEmail);
        var patientId = await NewPatientAsync(nurse, "Arrival Role Check");
        var id = await CreateIdAsync(nurse, patientId, await NurseIdAsync());

        using var anonymous = _application.CreateClient();
        using var doctor = await ClientAsync(ApiApplication.DoctorEmail);
        using var reception = await ClientAsync(ApiApplication.ReceptionEmail);
        using var manager = await ClientAsync(ApiApplication.ManagerEmail);

        var withoutToken = await anonymous.PostAsync($"/api/admissions/{id}/arrive", null);
        var wrongRole = await doctor.PostAsync($"/api/admissions/{id}/arrive", null);

        Assert.Equal(HttpStatusCode.Unauthorized, withoutToken.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, wrongRole.StatusCode);

        // Reception and the duty manager are past the policy. They get 409 because this
        // admission has no bed yet, which is a state problem and not a permission one.
        foreach (var allowed in new[] { reception, manager, nurse })
        {
            var response = await allowed.PostAsync($"/api/admissions/{id}/arrive", null);
            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        }
    }

    [Fact]
    public async Task Calling_a_visit_off_belongs_to_the_duty_manager_and_nobody_else()
    {
        using var nurse = await ClientAsync(ApiApplication.NurseEmail);
        var patientId = await NewPatientAsync(nurse, "Cancel Role Check");
        var id = await CreateIdAsync(nurse, patientId, await NurseIdAsync());

        using var anonymous = _application.CreateClient();
        var body = new { reason = "no_show" };

        var withoutToken = await anonymous.PostAsJsonAsync($"/api/admissions/{id}/cancel", body);
        var wrongRole = await nurse.PostAsJsonAsync($"/api/admissions/{id}/cancel", body);

        Assert.Equal(HttpStatusCode.Unauthorized, withoutToken.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, wrongRole.StatusCode);
    }

    private static Task<HttpResponseMessage> CreateAsync(
        HttpClient client,
        string patientId,
        string staffId,
        string source = "walk_in",
        string? dispatchId = null,
        string urgency = "routine",
        string category = "general")
        => client.PostAsJsonAsync("/api/admissions", new
        {
            patient_id = patientId,
            source,
            dispatch_id = dispatchId,
            admission_category = category,
            category_set_by_staff_id = staffId,
            urgency,
            is_infectious = false
        });

    private static async Task<string> CreateIdAsync(
        HttpClient client, string patientId, string staffId)
    {
        var created = await CreateAsync(client, patientId, staffId);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        using var body = await ReadJsonAsync(created);
        return body.RootElement.GetProperty("id").GetString()!;
    }

    private static async Task<string> NewPatientAsync(
        HttpClient client, string fullName, string? nic = null)
    {
        var created = await client.PostAsJsonAsync("/api/patients", new
        {
            full_name = fullName,
            gender = "male",
            nic
        });

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        using var body = await ReadJsonAsync(created);
        return body.RootElement.GetProperty("id").GetString()!;
    }

    private static async Task<string> NewCompletePatientAsync(HttpClient client, string fullName)
    {
        var created = await client.PostAsJsonAsync("/api/patients", new
        {
            full_name = fullName,
            gender = "female",
            nic = NewNic(),
            date_of_birth = "1990-01-01",
            phone = NewPhone(),
            address = "1 Complete Road, Colombo",
            emergency_contact_name = "A Relative",
            emergency_contact_phone = NewPhone()
        });

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        using var body = await ReadJsonAsync(created);
        return body.RootElement.GetProperty("id").GetString()!;
    }

    private async Task ReserveABedAsync(string admissionId)
    {
        using var administrator = await ClientAsync(ApiApplication.AdministratorEmail);
        using var equipment = await ClientAsync(ApiApplication.EquipmentEmail);
        using var nurse = await ClientAsync(ApiApplication.NurseEmail);

        var ward = await administrator.PostAsJsonAsync("/api/wards", new
        {
            name = $"Adm-Ward-{Guid.NewGuid():N}"[..24],
            ward_type = "general",
            gender_policy = "mixed",
            is_active = true
        });

        Assert.Equal(HttpStatusCode.Created, ward.StatusCode);

        using var wardBody = await ReadJsonAsync(ward);

        var bed = await equipment.PostAsJsonAsync("/api/beds", new
        {
            ward_id = wardBody.RootElement.GetProperty("id").GetString(),
            bed_number = "B1",
            has_isolation = false
        });

        Assert.Equal(HttpStatusCode.Created, bed.StatusCode);

        using var bedBody = await ReadJsonAsync(bed);

        var assigned = await nurse.PostAsJsonAsync(
            $"/api/admissions/{admissionId}/assign-bed",
            new { bed_id = bedBody.RootElement.GetProperty("id").GetString() });

        Assert.Equal(HttpStatusCode.OK, assigned.StatusCode);
    }

    private static IReadOnlyList<string> PatientNames(JsonDocument page)
        => page.RootElement.GetProperty("items").EnumerateArray()
            .Select(item => item.GetProperty("patient").GetProperty("full_name").GetString()!)
            .ToList();

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
            var login = await client.PostAsJsonAsync(
                "/api/auth/login", new { email, password = ApiApplication.Password });

            Assert.Equal(HttpStatusCode.OK, login.StatusCode);

            using var body = await ReadJsonAsync(login);
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

    private static string NewNic() => $"A{Guid.NewGuid():N}"[..12];

    private static string NewPhone() => $"07{Random.Shared.NextInt64(10000000, 99999999)}";
}
