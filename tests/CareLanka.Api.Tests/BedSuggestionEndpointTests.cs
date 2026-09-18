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
/// The two agent endpoints, end to end through HTTP and the background worker.
/// </summary>
/// <remarks>
/// These deliberately do not assert WHICH bed comes back. The agent reads every free bed in the
/// hospital, and another test's ward is a bed it would rightly offer - so pinning the answer here
/// would be pinning the order the suite happens to run in. Which bed wins, and every blocked
/// outcome, is <see cref="BedAgentTests"/> with the tools faked.
/// </remarks>
[Collection(ApiCollection.Name)]
public sealed class BedSuggestionEndpointTests
{
    private readonly ApiApplication _application;

    public BedSuggestionEndpointTests(ApiApplication application) => _application = application;

    [Fact]
    public async Task A_run_started_from_the_board_comes_back_with_a_bed_and_the_patients_name()
    {
        using var nurse = await ClientAsync(ApiApplication.NurseEmail);
        var ward = await NewWardAsync();
        await AddBedsAsync(ward, 1);
        var admissionId = await NewAdmissionAsync(nurse);

        var workflow = await RunAsync(nurse, new { admission_id = admissionId });

        Assert.Equal("proposed", workflow.GetProperty("outcome").GetString());
        Assert.Equal("awaiting_approval", workflow.GetProperty("status").GetString());
        Assert.Equal("assign_bed", workflow.GetProperty("objective").GetString());

        var patient = workflow.GetProperty("patient");
        Assert.False(string.IsNullOrWhiteSpace(patient.GetProperty("full_name").GetString()));
        Assert.Equal(admissionId, patient.GetProperty("admission_id").GetString());

        var best = workflow.GetProperty("best");
        Assert.NotEqual(JsonValueKind.Null, best.ValueKind);
        Assert.False(string.IsNullOrWhiteSpace(best.GetProperty("bed_number").GetString()));
        Assert.False(best.GetProperty("is_downgrade").GetBoolean());
        Assert.Equal("ward_nurse", workflow.GetProperty("requires_approval_by").GetString());
        Assert.Equal(JsonValueKind.Null, workflow.GetProperty("blocker").ValueKind);
    }

    [Fact]
    public async Task Every_alternative_carries_the_same_fields_as_the_best_pick()
    {
        using var nurse = await ClientAsync(ApiApplication.NurseEmail);
        var ward = await NewWardAsync();
        await AddBedsAsync(ward, 3);
        var admissionId = await NewAdmissionAsync(nurse);

        var workflow = await RunAsync(nurse, new { admission_id = admissionId });

        foreach (var alternative in workflow.GetProperty("alternatives").EnumerateArray())
        {
            // The one thing that makes an alternative selectable rather than a footnote.
            Assert.False(string.IsNullOrWhiteSpace(alternative.GetProperty("bed_id").GetString()));
            Assert.False(string.IsNullOrWhiteSpace(alternative.GetProperty("bed_number").GetString()));
            Assert.False(string.IsNullOrWhiteSpace(alternative.GetProperty("ward_name").GetString()));
            alternative.GetProperty("requires_duty_manager").GetBoolean();
        }
    }

    [Fact]
    public async Task The_run_persists_its_plan_its_steps_and_its_tool_calls()
    {
        using var nurse = await ClientAsync(ApiApplication.NurseEmail);
        var ward = await NewWardAsync();
        await AddBedsAsync(ward, 1);

        var workflow = await RunAsync(
            nurse, new { admission_id = await NewAdmissionAsync(nurse) });

        Assert.NotEmpty(workflow.GetProperty("plan").EnumerateArray());
        Assert.NotEmpty(workflow.GetProperty("steps").EnumerateArray());
        Assert.Equal(1, workflow.GetProperty("attempt_count").GetInt32());

        var tools = workflow.GetProperty("tool_calls").EnumerateArray().ToList();
        Assert.Contains(tools, call => call.GetProperty("tool").GetString() == "list_available_beds");
        Assert.All(tools, call => Assert.True(call.GetProperty("duration_ms").GetInt32() >= 0));

        Assert.True(workflow.GetProperty("validation").GetProperty("passed").GetBoolean());
    }

    [Fact]
    public async Task The_agent_holds_no_bed_and_moves_no_admission()
    {
        using var nurse = await ClientAsync(ApiApplication.NurseEmail);
        var ward = await NewWardAsync();
        var beds = await AddBedsAsync(ward, 1);
        var admissionId = await NewAdmissionAsync(nurse);

        var workflow = await RunAsync(nurse, new { admission_id = admissionId });
        Assert.Equal("proposed", workflow.GetProperty("outcome").GetString());

        // The whole point of the 2026-09-16 redesign: a run nobody acts on leaves the database
        // exactly as it found it and takes no bed out of circulation.
        Assert.Equal("awaiting_bed", await StatusAsync(nurse, admissionId));

        using var scope = _application.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CareLankaDbContext>();

        Assert.False(await db.BedAssignments.AnyAsync(a => beds.Contains(a.BedId)));
    }

    [Fact]
    public async Task An_outpatient_is_told_they_need_no_bed_rather_than_refused_at_the_door()
    {
        using var nurse = await ClientAsync(ApiApplication.NurseEmail);

        var workflow = await RunAsync(nurse, new
        {
            admission_id = await NewAdmissionAsync(nurse, category: "outpatient")
        });

        Assert.Equal("visit_needs_no_bed", workflow.GetProperty("outcome").GetString());
        Assert.Equal("completed", workflow.GetProperty("status").GetString());
        Assert.Equal(
            "no_bed_required", workflow.GetProperty("blocker").GetProperty("code").GetString());
        Assert.Equal(JsonValueKind.Null, workflow.GetProperty("requires_approval_by").ValueKind);
    }

    [Fact]
    public async Task An_identifier_matching_nobody_is_a_completed_run_and_not_a_404()
    {
        using var nurse = await ClientAsync(ApiApplication.NurseEmail);

        var workflow = await RunAsync(nurse, new { patient_identifier = "PZZZZZZZ" });

        Assert.Equal("patient_not_found", workflow.GetProperty("outcome").GetString());
        Assert.Equal(JsonValueKind.Null, workflow.GetProperty("patient").ValueKind);
        Assert.Equal(
            "no_such_patient", workflow.GetProperty("blocker").GetProperty("code").GetString());
    }

    [Fact]
    public async Task A_patient_code_off_the_slip_finds_the_open_visit_without_being_given_it()
    {
        using var nurse = await ClientAsync(ApiApplication.NurseEmail);
        var ward = await NewWardAsync();
        await AddBedsAsync(ward, 1);
        var (admissionId, patientCode) = await NewAdmissionWithCodeAsync(nurse);

        var workflow = await RunAsync(nurse, new { patient_identifier = patientCode });

        Assert.Equal("proposed", workflow.GetProperty("outcome").GetString());
        Assert.Equal(admissionId, workflow.GetProperty("admission_id").GetString());
    }

    [Fact]
    public async Task Sending_both_identifiers_or_neither_is_a_400()
    {
        using var nurse = await ClientAsync(ApiApplication.NurseEmail);

        var both = await nurse.PostAsJsonAsync("/api/bed-suggestions", new
        {
            admission_id = Guid.NewGuid(),
            patient_identifier = "PABCDEFG"
        });

        var neither = await nurse.PostAsJsonAsync("/api/bed-suggestions", new { });

        Assert.Equal(HttpStatusCode.BadRequest, both.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, neither.StatusCode);
        Assert.Equal("application/problem+json", both.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task An_unknown_admission_is_a_404_and_costs_no_workflow_row()
    {
        using var nurse = await ClientAsync(ApiApplication.NurseEmail);

        using var scope = _application.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CareLankaDbContext>();
        var before = await db.AgentWorkflows.CountAsync();

        var response = await nurse.PostAsJsonAsync(
            "/api/bed-suggestions", new { admission_id = Guid.NewGuid() });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(before, await db.AgentWorkflows.CountAsync());
    }

    [Fact]
    public async Task A_visit_already_holding_a_bed_is_refused_with_cl_pat_039()
    {
        using var nurse = await ClientAsync(ApiApplication.NurseEmail);
        var ward = await NewWardAsync();
        var beds = await AddBedsAsync(ward, 1);
        var admissionId = await NewAdmissionAsync(nurse);

        var assigned = await nurse.PostAsJsonAsync(
            $"/api/admissions/{admissionId}/assign-bed", new { bed_id = beds[0] });
        Assert.Equal(HttpStatusCode.OK, assigned.StatusCode);

        var response = await nurse.PostAsJsonAsync(
            "/api/bed-suggestions", new { admission_id = admissionId });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        using var problem = await ReadJsonAsync(response);
        Assert.Equal("cl_pat_039", problem.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Ambulance_crew_cannot_ask_for_a_bed_suggestion()
    {
        using var crew = await ClientAsync(ApiApplication.AmbulanceEmail);

        var response = await crew.PostAsJsonAsync(
            "/api/bed-suggestions", new { admission_id = Guid.NewGuid() });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task An_unknown_workflow_is_a_404()
    {
        using var nurse = await ClientAsync(ApiApplication.NurseEmail);

        var response = await nurse.GetAsync($"/api/bed-workflows/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Confirming_a_suggestion_runs_the_manual_path_and_records_the_agent()
    {
        using var nurse = await ClientAsync(ApiApplication.NurseEmail);
        var ward = await NewWardAsync();
        await AddBedsAsync(ward, 1);
        var admissionId = await NewAdmissionAsync(nurse);

        var workflow = await RunAsync(nurse, new { admission_id = admissionId });
        var workflowId = workflow.GetProperty("workflow_id").GetString();
        var bedId = workflow.GetProperty("best").GetProperty("bed_id").GetString();

        var assigned = await nurse.PostAsJsonAsync(
            $"/api/admissions/{admissionId}/assign-bed",
            new { bed_id = bedId, workflow_id = workflowId });

        Assert.Equal(HttpStatusCode.OK, assigned.StatusCode);

        using var body = await ReadJsonAsync(assigned);

        Assert.Equal("agent", body.RootElement.GetProperty("assigned_by").GetString());
        Assert.Equal(workflowId, body.RootElement.GetProperty("workflow_id").GetString());

        // The person who pressed the button is still the approver. assigned_by records only where
        // the idea came from.
        Assert.Equal(
            JsonValueKind.String, body.RootElement.GetProperty("approved_by_staff_id").ValueKind);
        Assert.Equal("bed_reserved", await StatusAsync(nurse, admissionId));
    }

    [Fact]
    public async Task A_workflow_that_never_suggested_this_bed_is_recorded_as_a_manual_pick()
    {
        using var nurse = await ClientAsync(ApiApplication.NurseEmail);
        var ward = await NewWardAsync();
        var beds = await AddBedsAsync(ward, 1);

        var otherRun = await RunAsync(
            nurse, new { admission_id = await NewAdmissionAsync(nurse) });

        var response = await nurse.PostAsJsonAsync(
            $"/api/admissions/{await NewAdmissionAsync(nurse)}/assign-bed",
            new
            {
                bed_id = beds[0],
                workflow_id = otherRun.GetProperty("workflow_id").GetString()
            });

        // Not refused. A stale id from a screen somebody left open must not stop a nurse bedding a
        // patient, and "no run suggested this bed" is exactly what assigned_by = user says.
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var body = await ReadJsonAsync(response);

        Assert.Equal("user", body.RootElement.GetProperty("assigned_by").GetString());
        Assert.Equal(JsonValueKind.Null, body.RootElement.GetProperty("workflow_id").ValueKind);
    }

    [Fact]
    public async Task A_bed_picked_by_hand_is_still_recorded_as_a_persons_choice()
    {
        using var nurse = await ClientAsync(ApiApplication.NurseEmail);
        var ward = await NewWardAsync();
        var beds = await AddBedsAsync(ward, 1);
        var admissionId = await NewAdmissionAsync(nurse);

        var assigned = await nurse.PostAsJsonAsync(
            $"/api/admissions/{admissionId}/assign-bed", new { bed_id = beds[0] });

        using var body = await ReadJsonAsync(assigned);

        Assert.Equal("user", body.RootElement.GetProperty("assigned_by").GetString());
        Assert.Equal(JsonValueKind.Null, body.RootElement.GetProperty("workflow_id").ValueKind);
    }

    // ---- helpers ----

    /// <summary>
    /// Starts a run and polls until it stops being <c>running</c>, which is what the screen does.
    /// </summary>
    private async Task<JsonElement> RunAsync(HttpClient client, object request)
    {
        var started = await client.PostAsJsonAsync("/api/bed-suggestions", request);

        Assert.Equal(HttpStatusCode.Accepted, started.StatusCode);

        using var accepted = await ReadJsonAsync(started);
        var workflowId = accepted.RootElement.GetProperty("workflow_id").GetString();

        Assert.Equal(
            $"/api/bed-workflows/{workflowId}",
            accepted.RootElement.GetProperty("poll_url").GetString());

        var deadline = DateTime.UtcNow.AddSeconds(30);

        while (DateTime.UtcNow < deadline)
        {
            var response = await client.GetAsync($"/api/bed-workflows/{workflowId}");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

            if (body.RootElement.GetProperty("status").GetString() != "running")
            {
                return body.RootElement.Clone();
            }

            await Task.Delay(100);
        }

        throw new TimeoutException($"Workflow {workflowId} was still running after 30 seconds.");
    }

    private sealed record TestWard(Guid Id, string Name);

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

    private async Task<IReadOnlyList<Guid>> AddBedsAsync(TestWard ward, int count)
    {
        using var equipment = await ClientAsync(ApiApplication.EquipmentEmail);
        var ids = new List<Guid>();

        for (var number = 1; number <= count; number++)
        {
            var created = await equipment.PostAsJsonAsync("/api/beds", new
            {
                ward_id = ward.Id,
                bed_number = $"A{number}",
                has_isolation = false
            });

            Assert.Equal(HttpStatusCode.Created, created.StatusCode);

            using var body = await ReadJsonAsync(created);
            ids.Add(Guid.Parse(body.RootElement.GetProperty("id").GetString()!));
        }

        return ids;
    }

    private async Task<string> StatusAsync(HttpClient client, string admissionId)
    {
        using var body = await ReadJsonAsync(await client.GetAsync($"/api/admissions/{admissionId}"));

        return body.RootElement.GetProperty("status").GetString()!;
    }

    private async Task<string> NewAdmissionAsync(
        HttpClient nurse, string category = "inpatient")
        => (await NewAdmissionWithCodeAsync(nurse, category)).AdmissionId;

    private async Task<(string AdmissionId, string PatientCode)> NewAdmissionWithCodeAsync(
        HttpClient nurse, string category = "inpatient")
    {
        var patient = await nurse.PostAsJsonAsync("/api/patients", new
        {
            full_name = $"Agent Patient {Guid.NewGuid():N}"[..28],
            gender = "male",
            nic = $"A{Guid.NewGuid():N}"[..12]
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
            is_infectious = false
        });

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        using var body = await ReadJsonAsync(created);

        return (
            body.RootElement.GetProperty("id").GetString()!,
            patientBody.RootElement.GetProperty("patient_code").GetString()!);
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

        using var scope = _application.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CareLankaDbContext>();

        var nurse = await db.StaffMembers.FirstAsync(s => s.Email == ApiApplication.NurseEmail);

        return _nurseId = nurse.Id.ToString();
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
