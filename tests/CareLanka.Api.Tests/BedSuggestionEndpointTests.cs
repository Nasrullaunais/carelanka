using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using CareLanka.Api.Data;
using CareLanka.Api.Data.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CareLanka.Api.Tests;

/// <summary>
/// The bed agent searches the whole hospital, so a test that wants a known answer has to be the
/// whole hospital: <see cref="OnlyTheseWardsAsync"/> takes every bed left over from another test
/// out of service first. Without it these tests assert on whatever the previous class happened to
/// leave free, which is the sort of test that passes until the day it matters.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class BedSuggestionEndpointTests
{
    private readonly ApiApplication _application;

    public BedSuggestionEndpointTests(ApiApplication application) => _application = application;

    [Fact]
    public async Task The_free_matching_bed_is_suggested_and_the_answer_says_who_it_is_for()
    {
        using var nurse = await ClientAsync(ApiApplication.NurseEmail);
        var ward = await NewWardAsync();
        var beds = await AddBedsAsync(ward, 1);
        await OnlyTheseWardsAsync(ward.Id);
        var patient = await NewPatientAsync(nurse);
        var admissionId = await NewAdmissionAsync(nurse, patient);

        using var run = await SuggestAsync(nurse, new { admission_id = admissionId });
        var answer = run.RootElement;

        Assert.Equal("proposed", answer.GetProperty("outcome").GetString());
        Assert.Equal(beds[0].ToString(), answer.GetProperty("best").GetProperty("bed_id").GetString());
        Assert.Equal(patient.Code, answer.GetProperty("patient").GetProperty("patient_code").GetString());
        Assert.Equal(patient.FullName, answer.GetProperty("patient").GetProperty("full_name").GetString());
        Assert.Equal("ward_nurse", answer.GetProperty("requires_approval_by").GetString());
        Assert.Equal(JsonValueKind.Null, answer.GetProperty("blocker").ValueKind);
        Assert.True(answer.GetProperty("validation").GetProperty("passed").GetBoolean());
    }

    [Fact]
    public async Task Every_other_bed_that_passed_comes_back_as_a_selectable_alternative()
    {
        using var nurse = await ClientAsync(ApiApplication.NurseEmail);
        var ward = await NewWardAsync();
        var beds = await AddBedsAsync(ward, 3);
        await OnlyTheseWardsAsync(ward.Id);
        var admissionId = await NewAdmissionAsync(nurse, await NewPatientAsync(nurse));

        using var run = await SuggestAsync(nurse, new { admission_id = admissionId });

        var offered = new[] { run.RootElement.GetProperty("best") }
            .Concat(run.RootElement.GetProperty("alternatives").EnumerateArray())
            .Select(bed => Guid.Parse(bed.GetProperty("bed_id").GetString()!))
            .ToList();

        Assert.Equal(3, offered.Count);
        Assert.Equal(beds.OrderBy(id => id), offered.OrderBy(id => id));
        Assert.All(
            run.RootElement.GetProperty("alternatives").EnumerateArray(),
            bed => Assert.False(bed.GetProperty("requires_duty_manager").GetBoolean()));
    }

    [Fact]
    public async Task A_run_holds_no_bed_and_leaves_the_visit_where_it_found_it()
    {
        using var nurse = await ClientAsync(ApiApplication.NurseEmail);
        var ward = await NewWardAsync();
        var beds = await AddBedsAsync(ward, 1);
        await OnlyTheseWardsAsync(ward.Id);
        var admissionId = await NewAdmissionAsync(nurse, await NewPatientAsync(nurse));

        using var _ = await SuggestAsync(nurse, new { admission_id = admissionId });

        using var admission = await ReadJsonAsync(await nurse.GetAsync($"/api/admissions/{admissionId}"));
        var bed = await BedRowAsync(nurse, ward, beds[0]);

        Assert.Equal("awaiting_bed", admission.RootElement.GetProperty("status").GetString());
        Assert.Equal("free", bed.GetProperty("availability").GetString());
    }

    [Fact]
    public async Task A_visit_that_needs_no_bed_is_told_so_rather_than_shown_an_empty_list()
    {
        using var nurse = await ClientAsync(ApiApplication.NurseEmail);
        var ward = await NewWardAsync();
        await AddBedsAsync(ward, 1);
        await OnlyTheseWardsAsync(ward.Id);
        var admissionId = await NewAdmissionAsync(
            nurse, await NewPatientAsync(nurse), category: "outpatient");

        using var run = await SuggestAsync(nurse, new { admission_id = admissionId });

        Assert.Equal("visit_needs_no_bed", run.RootElement.GetProperty("outcome").GetString());
        Assert.Equal(
            "no_bed_required",
            run.RootElement.GetProperty("blocker").GetProperty("code").GetString());
        Assert.Equal(JsonValueKind.Null, run.RootElement.GetProperty("best").ValueKind);
    }

    [Fact]
    public async Task A_maternity_bed_is_never_offered_to_a_patient_who_is_not_on_maternity()
    {
        using var nurse = await ClientAsync(ApiApplication.NurseEmail);
        var general = await NewWardAsync();
        var generalBeds = await AddBedsAsync(general, 1, hasIsolation: true);
        var maternity = await NewWardAsync(wardType: "maternity", genderPolicy: "female");
        await AddBedsAsync(maternity, 5, hasIsolation: true);
        await OnlyTheseWardsAsync(general.Id, maternity.Id);

        var patient = await NewPatientAsync(nurse, gender: "female");
        var admissionId = await NewAdmissionAsync(nurse, patient, isInfectious: true);

        using var run = await SuggestAsync(nurse, new { admission_id = admissionId });

        Assert.Equal("proposed", run.RootElement.GetProperty("outcome").GetString());
        Assert.Equal(
            generalBeds[0].ToString(),
            run.RootElement.GetProperty("best").GetProperty("bed_id").GetString());
    }

    [Fact]
    public async Task A_patient_code_off_the_slip_finds_the_patient_and_their_open_visit()
    {
        using var nurse = await ClientAsync(ApiApplication.NurseEmail);
        var ward = await NewWardAsync();
        await AddBedsAsync(ward, 1);
        await OnlyTheseWardsAsync(ward.Id);
        var patient = await NewPatientAsync(nurse);
        var admissionId = await NewAdmissionAsync(nurse, patient);

        using var run = await SuggestAsync(nurse, new { patient_identifier = patient.Code });

        Assert.Equal("proposed", run.RootElement.GetProperty("outcome").GetString());
        Assert.Equal(admissionId, run.RootElement.GetProperty("admission_id").GetString());
    }

    [Fact]
    public async Task An_nic_off_the_slip_works_the_same_way_and_nobody_says_which_it_was()
    {
        using var nurse = await ClientAsync(ApiApplication.NurseEmail);
        var ward = await NewWardAsync();
        await AddBedsAsync(ward, 1);
        await OnlyTheseWardsAsync(ward.Id);
        var patient = await NewPatientAsync(nurse);
        await NewAdmissionAsync(nurse, patient);

        using var run = await SuggestAsync(nurse, new { patient_identifier = patient.Nic });

        Assert.Equal("proposed", run.RootElement.GetProperty("outcome").GetString());
    }

    [Fact]
    public async Task An_identifier_matching_nobody_is_an_answer_and_not_an_error()
    {
        using var nurse = await ClientAsync(ApiApplication.NurseEmail);

        using var run = await SuggestAsync(nurse, new { patient_identifier = "PZZZZZZZ" });

        Assert.Equal("patient_not_found", run.RootElement.GetProperty("outcome").GetString());
        Assert.Equal(
            "no_such_patient",
            run.RootElement.GetProperty("blocker").GetProperty("code").GetString());
        Assert.Equal(JsonValueKind.Null, run.RootElement.GetProperty("patient").ValueKind);
    }

    [Fact]
    public async Task A_patient_with_no_open_visit_is_told_to_register_one()
    {
        using var nurse = await ClientAsync(ApiApplication.NurseEmail);
        var patient = await NewPatientAsync(nurse);

        using var run = await SuggestAsync(nurse, new { patient_identifier = patient.Nic });

        Assert.Equal(
            "no_open_admission",
            run.RootElement.GetProperty("blocker").GetProperty("code").GetString());
    }

    [Fact]
    public async Task With_nothing_free_at_the_right_level_it_offers_one_step_down_and_names_the_approver()
    {
        using var nurse = await ClientAsync(ApiApplication.NurseEmail);
        var hdu = await NewWardAsync(wardType: "hdu");
        var beds = await AddBedsAsync(hdu, 1);
        await OnlyTheseWardsAsync(hdu.Id);
        var admissionId = await NewAdmissionAsync(
            nurse, await NewPatientAsync(nurse), category: "icu");

        using var run = await SuggestAsync(nurse, new { admission_id = admissionId });

        Assert.Equal("proposed_with_downgrade", run.RootElement.GetProperty("outcome").GetString());
        Assert.Equal(
            "downgrade_needed",
            run.RootElement.GetProperty("blocker").GetProperty("code").GetString());
        Assert.Equal("duty_manager", run.RootElement.GetProperty("requires_approval_by").GetString());

        var best = run.RootElement.GetProperty("best");

        Assert.Equal(beds[0].ToString(), best.GetProperty("bed_id").GetString());
        Assert.True(best.GetProperty("is_downgrade").GetBoolean());
        Assert.True(best.GetProperty("requires_duty_manager").GetBoolean());
    }

    [Fact]
    public async Task It_never_ranks_a_more_acute_bed_as_its_best_pick()
    {
        using var nurse = await ClientAsync(ApiApplication.NurseEmail);
        var icu = await NewWardAsync(wardType: "icu");
        await AddBedsAsync(icu, 2);
        await OnlyTheseWardsAsync(icu.Id);
        var admissionId = await NewAdmissionAsync(
            nurse, await NewPatientAsync(nurse), category: "inpatient");

        using var run = await SuggestAsync(nurse, new { admission_id = admissionId });

        Assert.Equal("needs_duty_manager", run.RootElement.GetProperty("outcome").GetString());
        Assert.Equal(
            "upgrade_only",
            run.RootElement.GetProperty("blocker").GetProperty("code").GetString());
        Assert.Equal(JsonValueKind.Null, run.RootElement.GetProperty("best").ValueKind);
        Assert.Empty(run.RootElement.GetProperty("alternatives").EnumerateArray());
    }

    [Fact]
    public async Task A_ward_that_will_not_take_this_patient_is_named_as_the_reason()
    {
        using var nurse = await ClientAsync(ApiApplication.NurseEmail);
        var ward = await NewWardAsync(genderPolicy: "female");
        await AddBedsAsync(ward, 2);
        await OnlyTheseWardsAsync(ward.Id);
        var patient = await NewPatientAsync(nurse, gender: "male");
        var admissionId = await NewAdmissionAsync(nurse, patient);

        using var run = await SuggestAsync(nurse, new { admission_id = admissionId });
        var blocker = run.RootElement.GetProperty("blocker");

        Assert.Equal("no_bed_available", run.RootElement.GetProperty("outcome").GetString());
        Assert.Equal("gender_policy", blocker.GetProperty("code").GetString());
        Assert.Contains("male", blocker.GetProperty("message").GetString());
    }

    [Fact]
    public async Task An_infectious_patient_with_no_isolation_bed_free_is_told_exactly_that()
    {
        using var nurse = await ClientAsync(ApiApplication.NurseEmail);
        var ward = await NewWardAsync();
        await AddBedsAsync(ward, 2);
        await OnlyTheseWardsAsync(ward.Id);
        var admissionId = await NewAdmissionAsync(
            nurse, await NewPatientAsync(nurse), isInfectious: true);

        using var run = await SuggestAsync(nurse, new { admission_id = admissionId });

        Assert.Equal(
            "needs_isolation",
            run.RootElement.GetProperty("blocker").GetProperty("code").GetString());
    }

    [Fact]
    public async Task Only_childrens_beds_free_is_its_own_sentence_and_not_hospital_full()
    {
        using var nurse = await ClientAsync(ApiApplication.NurseEmail);
        var ward = await NewWardAsync(wardType: "pediatric");
        await AddBedsAsync(ward, 1);
        await OnlyTheseWardsAsync(ward.Id);
        var patient = await NewPatientAsync(
            nurse, dateOfBirth: DateTime.UtcNow.AddYears(-40).ToString("yyyy-MM-dd"));
        var admissionId = await NewAdmissionAsync(nurse, patient);

        using var run = await SuggestAsync(nurse, new { admission_id = admissionId });
        var blocker = run.RootElement.GetProperty("blocker");

        Assert.Equal("pediatric_only", blocker.GetProperty("code").GetString());
        Assert.Contains("40", blocker.GetProperty("message").GetString());
    }

    [Fact]
    public async Task With_no_bed_free_anywhere_the_patient_stays_on_the_waiting_list()
    {
        using var nurse = await ClientAsync(ApiApplication.NurseEmail);
        var ward = await NewWardAsync();
        await AddBedsAsync(ward, 1);
        await OnlyTheseWardsAsync();
        var admissionId = await NewAdmissionAsync(nurse, await NewPatientAsync(nurse));

        using var run = await SuggestAsync(nurse, new { admission_id = admissionId });

        Assert.Equal("no_bed_available", run.RootElement.GetProperty("outcome").GetString());
        Assert.Equal(
            "ward_full",
            run.RootElement.GetProperty("blocker").GetProperty("code").GetString());
    }

    [Fact]
    public async Task The_emptier_ward_wins_between_two_that_both_fit()
    {
        using var nurse = await ClientAsync(ApiApplication.NurseEmail);
        var busy = await NewWardAsync();
        var quiet = await NewWardAsync();
        var busyBeds = await AddBedsAsync(busy, 2);
        var quietBeds = await AddBedsAsync(quiet, 2);
        await OnlyTheseWardsAsync(busy.Id, quiet.Id);
        await AssignAsync(busyBeds[0]);
        var admissionId = await NewAdmissionAsync(nurse, await NewPatientAsync(nurse));

        using var run = await SuggestAsync(nurse, new { admission_id = admissionId });

        Assert.Contains(
            run.RootElement.GetProperty("best").GetProperty("bed_id").GetString(),
            quietBeds.Select(id => id.ToString()));
    }

    [Fact]
    public async Task The_run_is_on_disk_with_its_plan_its_steps_and_the_tools_it_called()
    {
        using var nurse = await ClientAsync(ApiApplication.NurseEmail);
        var ward = await NewWardAsync();
        await AddBedsAsync(ward, 1);
        await OnlyTheseWardsAsync(ward.Id);
        var admissionId = await NewAdmissionAsync(nurse, await NewPatientAsync(nurse));

        var workflowId = Guid.Parse(
            await SettledRunAsync(nurse, new { admission_id = admissionId }));

        using var scope = _application.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CareLankaDbContext>();
        var workflow = await db.AgentWorkflows
            .Include(row => row.ProposedChanges)
            .AsNoTracking()
            .SingleAsync(row => row.Id == workflowId);

        Assert.Equal(AgentType.PatientAdmissionBed, workflow.AgentType);
        Assert.Equal(AgentWorkflowStatus.PendingApproval, workflow.Status);
        Assert.Equal("suggest_bed", workflow.Objective);
        Assert.Equal("proposed", workflow.FinalOutcome);
        Assert.Equal(StaffRole.WardNurse, workflow.RequiredApproverRole);
        Assert.Contains("apply_hard_rules", workflow.Plan);
        Assert.Contains("list_available_beds", workflow.ToolResults);
        Assert.Contains("rank_on_soft_rules", workflow.CompletedSteps);
        Assert.Single(workflow.ProposedChanges);
        Assert.Null(workflow.Errors);
    }

    [Fact]
    public async Task Pressing_use_this_bed_stamps_the_assignment_with_the_run_that_suggested_it()
    {
        using var nurse = await ClientAsync(ApiApplication.NurseEmail);
        var ward = await NewWardAsync();
        var beds = await AddBedsAsync(ward, 1);
        await OnlyTheseWardsAsync(ward.Id);
        var admissionId = await NewAdmissionAsync(nurse, await NewPatientAsync(nurse));

        var workflowId = await SettledRunAsync(nurse, new { admission_id = admissionId });

        using var assigned = await ReadJsonAsync(await nurse.PostAsJsonAsync(
            $"/api/admissions/{admissionId}/assign-bed",
            new { bed_id = beds[0], workflow_id = workflowId }));

        Assert.Equal("agent", assigned.RootElement.GetProperty("assigned_by").GetString());
        Assert.Equal(workflowId, assigned.RootElement.GetProperty("workflow_id").GetString());

        using var scope = _application.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CareLankaDbContext>();
        var workflow = await db.AgentWorkflows
            .Include(row => row.ProposedChanges)
            .AsNoTracking()
            .SingleAsync(row => row.Id == Guid.Parse(workflowId));

        Assert.Equal(AgentWorkflowStatus.Executed, workflow.Status);
        Assert.NotNull(workflow.ReviewedAt);
        Assert.NotNull(workflow.ProposedChanges.Single().AppliedAt);
    }

    [Fact]
    public async Task A_workflow_id_naming_a_bed_the_run_never_suggested_is_recorded_as_a_manual_pick()
    {
        using var nurse = await ClientAsync(ApiApplication.NurseEmail);
        var ward = await NewWardAsync();
        var beds = await AddBedsAsync(ward, 2);
        await OnlyTheseWardsAsync(ward.Id);
        var admissionId = await NewAdmissionAsync(nurse, await NewPatientAsync(nurse));

        // The run only ever saw the first bed: the second is out of service while it looks.
        await SetConditionAsync(beds[1], BedCondition.OutOfService);
        var workflowId = await SettledRunAsync(nurse, new { admission_id = admissionId });
        await SetConditionAsync(beds[1], BedCondition.Usable);

        using var assigned = await ReadJsonAsync(await nurse.PostAsJsonAsync(
            $"/api/admissions/{admissionId}/assign-bed",
            new { bed_id = beds[1], workflow_id = workflowId }));

        Assert.Equal("user", assigned.RootElement.GetProperty("assigned_by").GetString());
        Assert.Equal(JsonValueKind.Null, assigned.RootElement.GetProperty("workflow_id").ValueKind);
    }

    [Fact]
    public async Task A_workflow_id_nobody_has_heard_of_is_recorded_as_a_manual_pick()
    {
        using var nurse = await ClientAsync(ApiApplication.NurseEmail);
        var ward = await NewWardAsync();
        var beds = await AddBedsAsync(ward, 1);
        var admissionId = await NewAdmissionAsync(nurse, await NewPatientAsync(nurse));

        using var assigned = await ReadJsonAsync(await nurse.PostAsJsonAsync(
            $"/api/admissions/{admissionId}/assign-bed",
            new { bed_id = beds[0], workflow_id = Guid.NewGuid() }));

        Assert.Equal("user", assigned.RootElement.GetProperty("assigned_by").GetString());
    }

    [Fact]
    public async Task A_visit_that_already_has_a_bed_has_nothing_to_suggest()
    {
        using var nurse = await ClientAsync(ApiApplication.NurseEmail);
        var ward = await NewWardAsync();
        var beds = await AddBedsAsync(ward, 1);
        var admissionId = await NewAdmissionAsync(nurse, await NewPatientAsync(nurse));

        await nurse.PostAsJsonAsync(
            $"/api/admissions/{admissionId}/assign-bed", new { bed_id = beds[0] });

        var refused = await nurse.PostAsJsonAsync(
            "/api/bed-suggestions", new { admission_id = admissionId });
        using var problem = await ReadJsonAsync(refused);

        Assert.Equal(HttpStatusCode.Conflict, refused.StatusCode);
        Assert.Equal("cl_pat_039", problem.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task An_admission_nobody_has_heard_of_is_a_404()
    {
        using var nurse = await ClientAsync(ApiApplication.NurseEmail);

        var response = await nurse.PostAsJsonAsync(
            "/api/bed-suggestions", new { admission_id = Guid.NewGuid() });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public async Task Both_ways_in_at_once_or_neither_is_a_400(bool withAdmission, bool withIdentifier)
    {
        using var nurse = await ClientAsync(ApiApplication.NurseEmail);

        var response = await nurse.PostAsJsonAsync("/api/bed-suggestions", new
        {
            admission_id = withAdmission ? Guid.NewGuid() : (Guid?)null,
            patient_identifier = withIdentifier ? "PABCDEFG" : null
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task A_run_nobody_has_heard_of_is_a_404_rather_than_an_empty_summary()
    {
        using var nurse = await ClientAsync(ApiApplication.NurseEmail);

        var response = await nurse.GetAsync($"/api/bed-workflows/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task A_doctor_may_not_ask_for_a_bed_and_nobody_may_ask_without_a_token()
    {
        using var doctor = await ClientAsync(ApiApplication.DoctorEmail);
        using var anonymous = _application.CreateClient();

        var refused = await doctor.PostAsJsonAsync(
            "/api/bed-suggestions", new { patient_identifier = "PABCDEFG" });
        var unauthenticated = await anonymous.GetAsync($"/api/bed-workflows/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.Forbidden, refused.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, unauthenticated.StatusCode);
    }

    [Fact]
    public async Task Reception_and_the_duty_manager_may_both_ask()
    {
        using var reception = await ClientAsync(ApiApplication.ReceptionEmail);
        using var manager = await ClientAsync(ApiApplication.ManagerEmail);

        var one = await reception.PostAsJsonAsync(
            "/api/bed-suggestions", new { patient_identifier = "PZZZZZZZ" });
        var two = await manager.PostAsJsonAsync(
            "/api/bed-suggestions", new { patient_identifier = "PZZZZZZZ" });

        Assert.Equal(HttpStatusCode.Accepted, one.StatusCode);
        Assert.Equal(HttpStatusCode.Accepted, two.StatusCode);
    }

    private sealed record TestWard(Guid Id, string Name);

    private sealed record TestPatient(string Id, string Code, string Nic, string FullName);

    /// <summary>
    /// Makes this test's wards the only ones with a free bed in them, by taking every bed left
    /// behind by another test out of service. The agent looks at the whole hospital, so without
    /// this the answer depends on which test ran first.
    /// </summary>
    private async Task OnlyTheseWardsAsync(params Guid[] wardIds)
    {
        using var scope = _application.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CareLankaDbContext>();

        var keep = wardIds.ToHashSet();

        var others = await db.Beds
            .Where(bed => !keep.Contains(bed.WardId))
            .Where(bed => bed.Condition == BedCondition.Usable)
            .ToListAsync();

        foreach (var bed in others)
        {
            bed.Condition = BedCondition.OutOfService;
        }

        await db.SaveChangesAsync();
    }

    private static async Task<JsonDocument> SuggestAsync(HttpClient client, object body)
        => await SettledAsync(client, await StartAsync(client, body));

    /// <summary>
    /// The endpoint answers 202 and the run happens on the background worker, so a test that reads
    /// the summary straight away reads "running" and no answer. This polls the published poll URL
    /// exactly as a screen does, and fails loudly rather than hanging if a run never finishes.
    /// </summary>
    private static async Task<JsonDocument> SettledAsync(HttpClient client, string workflowId)
    {
        var deadline = DateTime.UtcNow.AddSeconds(30);

        while (true)
        {
            var response = await client.GetAsync($"/api/bed-workflows/{workflowId}");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var summary = await ReadJsonAsync(response);

            if (summary.RootElement.GetProperty("status").GetString() != "running")
            {
                return summary;
            }

            summary.Dispose();

            Assert.True(
                DateTime.UtcNow < deadline,
                $"Bed workflow {workflowId} was still running after 30 seconds.");

            await Task.Delay(50);
        }
    }

    private static async Task<string> StartAsync(HttpClient client, object body)
    {
        var accepted = await client.PostAsJsonAsync("/api/bed-suggestions", body);
        Assert.Equal(HttpStatusCode.Accepted, accepted.StatusCode);

        using var document = await ReadJsonAsync(accepted);

        return document.RootElement.GetProperty("workflow_id").GetString()!;
    }

    /// <summary>Starts a run and waits for it, returning the id for tests that read the row.</summary>
    private static async Task<string> SettledRunAsync(HttpClient client, object body)
    {
        var workflowId = await StartAsync(client, body);
        using var _ = await SettledAsync(client, workflowId);

        return workflowId;
    }

    private async Task<TestWard> NewWardAsync(
        string wardType = "general", string genderPolicy = "mixed")
    {
        using var administrator = await ClientAsync(ApiApplication.AdministratorEmail);
        var name = $"Agent-Ward-{Guid.NewGuid():N}"[..24];

        var created = await administrator.PostAsJsonAsync("/api/wards", new
        {
            name,
            ward_type = wardType,
            gender_policy = genderPolicy,
            is_active = true
        });

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        using var body = await ReadJsonAsync(created);

        return new TestWard(Guid.Parse(body.RootElement.GetProperty("id").GetString()!), name);
    }

    private async Task<IReadOnlyList<Guid>> AddBedsAsync(
        TestWard ward, int count, bool hasIsolation = false)
    {
        using var equipment = await ClientAsync(ApiApplication.EquipmentEmail);
        var ids = new List<Guid>();

        for (var number = 1; number <= count; number++)
        {
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

    private async Task SetConditionAsync(Guid bedId, BedCondition condition)
    {
        using var scope = _application.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CareLankaDbContext>();

        var bed = await db.Beds.FirstAsync(row => row.Id == bedId);
        bed.Condition = condition;

        await db.SaveChangesAsync();
    }

    private async Task<string> AssignAsync(Guid bedId)
    {
        using var nurse = await ClientAsync(ApiApplication.NurseEmail);
        var admissionId = await NewAdmissionAsync(nurse, await NewPatientAsync(nurse));

        var assigned = await nurse.PostAsJsonAsync(
            $"/api/admissions/{admissionId}/assign-bed", new { bed_id = bedId });

        Assert.Equal(HttpStatusCode.OK, assigned.StatusCode);

        return admissionId;
    }

    private static async Task<TestPatient> NewPatientAsync(
        HttpClient nurse, string gender = "male", string? dateOfBirth = null)
    {
        var fullName = $"Agent Patient {Guid.NewGuid():N}"[..28];
        var nic = $"A{Guid.NewGuid():N}"[..12];

        var created = await nurse.PostAsJsonAsync("/api/patients", new
        {
            full_name = fullName,
            gender,
            nic,
            date_of_birth = dateOfBirth
        });

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        using var body = await ReadJsonAsync(created);

        return new TestPatient(
            body.RootElement.GetProperty("id").GetString()!,
            body.RootElement.GetProperty("patient_code").GetString()!,
            nic,
            fullName);
    }

    private async Task<string> NewAdmissionAsync(
        HttpClient nurse,
        TestPatient patient,
        string category = "inpatient",
        bool isInfectious = false)
    {
        var created = await nurse.PostAsJsonAsync("/api/admissions", new
        {
            patient_id = patient.Id,
            source = "walk_in",
            admission_category = category,
            category_set_by_staff_id = await NurseIdAsync(),
            urgency = "routine",
            is_infectious = isInfectious
        });

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        using var body = await ReadJsonAsync(created);

        return body.RootElement.GetProperty("id").GetString()!;
    }

    private static async Task<JsonElement> BedRowAsync(
        HttpClient client, TestWard ward, Guid bedId)
    {
        var response = await client.GetAsync($"/api/bed-availability?wardId={ward.Id}&pageSize=100");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var body = await ReadJsonAsync(response);

        return body.RootElement.GetProperty("items").EnumerateArray()
            .Single(row => row.GetProperty("id").GetString() == bedId.ToString())
            .Clone();
    }

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
