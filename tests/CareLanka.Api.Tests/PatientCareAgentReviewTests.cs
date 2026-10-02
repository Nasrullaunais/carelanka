using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using CareLanka.Api.Agents;
using CareLanka.Api.Agents.Patient;
using CareLanka.Api.Data;
using CareLanka.Api.Data.Entities.Common;
using CareLanka.Api.Data.Enums;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;
using CareRecommendationRow = CareLanka.Api.Data.Entities.Patient.CareRecommendation;

namespace CareLanka.Api.Tests;

internal sealed class RecordingCareModel : ILanguageModel
{
    public const string Draft = "Thank you for telling us. A nurse will come and see you shortly.";

    private readonly ConcurrentQueue<string> _sent = new();

    public bool IsConfigured => true;

    public IReadOnlyCollection<string> Sent => _sent;

    public Task<LanguageModelResult> CompleteJsonAsync(
        string instruction, string dataJson, CancellationToken cancellationToken = default)
    {
        _sent.Enqueue(dataJson);

        return Task.FromResult(LanguageModelResult.Success(
            $$"""{"patient_asked":"how they feel","kind":"health","urgency_flag":"medium","message":"{{Draft}}"}"""));
    }
}

internal static class CareReviewHost
{
    private static readonly object Gate = new();
    private static WebApplicationFactory<Program>? _factory;

    public static RecordingCareModel Model { get; } = new();

    public static WebApplicationFactory<Program> For(ApiApplication application)
    {
        lock (Gate)
        {
            return _factory ??= application.WithWebHostBuilder(builder => builder.ConfigureServices(
                services => services.Replace(ServiceDescriptor.Singleton<ILanguageModel>(Model))));
        }
    }
}

/// <summary>
/// PT-AI review, privacy and rate-limit tests. They go through the real API and database with a
/// recording fake model in place of Gemini, so no key and no network are used.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class PatientCareAgentReviewTests
{
    private const string Quiet = "I feel a little tired today.";

    private static readonly ConcurrentDictionary<string, string> Tokens = new();
    private static string? _nurseId;

    private static readonly string[] Plan =
    [
        "screen_red_flags", "get_medical_profile", "get_patient_history", "get_current_admission",
        "draft_recommendation", "validate_deterministically", "pause_for_approval"
    ];

    private readonly WebApplicationFactory<Program> _host;

    public PatientCareAgentReviewTests(ApiApplication application) => _host = CareReviewHost.For(application);

    [Theory]
    [Trait("id", "PT-AI-07a")]
    [InlineData("nurse", "approve")]
    [InlineData("nurse", "reject")]
    [InlineData("nurse", "redraft")]
    [InlineData("doctor", "approve")]
    [InlineData("doctor", "reject")]
    [InlineData("doctor", "redraft")]
    public async Task A_ward_nurse_and_a_doctor_may_approve_reject_and_redraft(string role, string action)
    {
        var draft = await PendingDraftAsync();
        using var reviewer = await StaffAsync(
            role == "nurse" ? ApiApplication.NurseEmail : ApiApplication.DoctorEmail);

        var response = await Act(reviewer, action, draft.RecommendationId);

        Assert.Equal(action == "redraft" ? HttpStatusCode.Accepted : HttpStatusCode.OK, response.StatusCode);

        if (action == "redraft")
        {
            using var body = await Json(response);
            await WaitForRunAsync(Guid.Parse(body.RootElement.GetProperty("workflow_id").GetString()!));
        }
    }

    [Fact]
    [Trait("id", "PT-AI-07b")]
    public async Task Every_other_role_is_refused_and_the_draft_is_left_alone()
    {
        var draft = await PendingDraftAsync();
        var refusedStaff = new[]
        {
            ApiApplication.ManagerEmail, ApiApplication.AdministratorEmail, ApiApplication.ReceptionEmail,
            ApiApplication.EquipmentEmail, ApiApplication.EquipmentAdministratorEmail, ApiApplication.AmbulanceEmail
        };

        foreach (var email in refusedStaff)
        {
            using var staff = await StaffAsync(email);

            foreach (var action in new[] { "approve", "reject", "redraft" })
            {
                var response = await Act(staff, action, draft.RecommendationId);
                Assert.True(HttpStatusCode.Forbidden == response.StatusCode, $"{email} {action}: {response.StatusCode}");
            }
        }

        foreach (var action in new[] { "approve", "reject", "redraft" })
        {
            var asPatient = await Act(draft.Patient.Client, action, draft.RecommendationId);
            Assert.True(HttpStatusCode.Forbidden == asPatient.StatusCode, $"patient {action}: {asPatient.StatusCode}");

            using var anonymous = _host.CreateClient();
            var asAnonymous = await Act(anonymous, action, draft.RecommendationId);
            Assert.True(HttpStatusCode.Unauthorized == asAnonymous.StatusCode, $"anonymous {action}: {asAnonymous.StatusCode}");
        }

        using var doctor = await StaffAsync(ApiApplication.DoctorEmail);
        using var detail = await Json(await doctor.GetAsync($"/api/care-recommendations/{draft.RecommendationId}"));

        Assert.Equal("pending_review", detail.RootElement.GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.Null, detail.RootElement.GetProperty("doctor_message").ValueKind);
        Assert.Equal(JsonValueKind.Null, detail.RootElement.GetProperty("rejection_reason").ValueKind);
        Assert.Equal(JsonValueKind.Null, detail.RootElement.GetProperty("reviewed_by_staff_id").ValueKind);
        Assert.Equal(1, await WorkflowCountAsync(draft.RecommendationId));
    }

    [Fact]
    [Trait("id", "PT-AI-08a")]
    public async Task An_approved_or_rejected_draft_cannot_be_reviewed_again()
    {
        var approved = await PendingDraftAsync();
        var rejected = await PendingDraftAsync();
        using var nurse = await StaffAsync(ApiApplication.NurseEmail);
        using var doctor = await StaffAsync(ApiApplication.DoctorEmail);

        var first = await nurse.PostAsJsonAsync(
            $"/api/care-recommendations/{approved.RecommendationId}/approve",
            new { doctor_message = "A nurse will see you this afternoon." });
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        var second = await doctor.PostAsJsonAsync(
            $"/api/care-recommendations/{rejected.RecommendationId}/reject", new { reason = "Already covered." });
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);

        foreach (var draft in new[] { approved, rejected })
        {
            var again = await nurse.PostAsJsonAsync(
                $"/api/care-recommendations/{draft.RecommendationId}/approve", new { doctor_message = "Changed my mind." });
            await AssertProblemAsync(again, HttpStatusCode.Conflict, "cl_pat_040");

            foreach (var action in new[] { "reject", "redraft" })
            {
                await AssertProblemAsync(
                    await Act(nurse, action, draft.RecommendationId), HttpStatusCode.Conflict, "cl_pat_040");
            }
        }

        using var mine = await Json(await approved.Patient.Client.GetAsync("/api/me/care-recommendations"));

        Assert.Equal(
            "A nurse will see you this afternoon.",
            Row(mine, approved.RecommendationId).GetProperty("doctor_message").GetString());
    }

    [Fact]
    [Trait("id", "PT-AI-08b")]
    public async Task A_report_that_does_not_exist_gives_not_found_for_every_action()
    {
        using var nurse = await StaffAsync(ApiApplication.NurseEmail);

        foreach (var action in new[] { "approve", "reject", "redraft" })
        {
            var response = await Act(nurse, action, Guid.NewGuid());
            Assert.True(HttpStatusCode.NotFound == response.StatusCode, $"{action}: {response.StatusCode}");
        }
    }

    [Fact]
    [Trait("id", "PT-AI-08c")]
    public async Task A_redraft_starts_a_new_run_and_keeps_the_first_one_on_record()
    {
        var draft = await PendingDraftAsync();
        using var nurse = await StaffAsync(ApiApplication.NurseEmail);

        var response = await nurse.PostAsync($"/api/care-recommendations/{draft.RecommendationId}/redraft", null);
        using var body = await Json(response);
        var newRun = Guid.Parse(body.RootElement.GetProperty("workflow_id").GetString()!);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        Assert.NotEqual(draft.WorkflowId, newRun);
        Assert.Equal($"/api/care-workflows/{newRun}", body.RootElement.GetProperty("poll_url").GetString());

        await WaitForRunAsync(newRun);

        using var scope = _host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CareLankaDbContext>();
        var runs = await db.AgentWorkflows.AsNoTracking()
            .Where(row => row.EntityId == draft.RecommendationId).ToListAsync();

        Assert.Equal(2, runs.Count);
        Assert.All(runs, run => Assert.NotNull(run.CompletedAt));

        using var detail = await Json(await nurse.GetAsync($"/api/care-recommendations/{draft.RecommendationId}"));

        Assert.Equal("pending_review", detail.RootElement.GetProperty("status").GetString());
        Assert.Equal(RecordingCareModel.Draft, detail.RootElement.GetProperty("agent_message").GetString());
        Assert.Equal(newRun.ToString(), detail.RootElement.GetProperty("workflow_id").GetString());
    }

    [Fact]
    [Trait("id", "PT-AI-08d")]
    public async Task A_redraft_is_refused_while_the_agent_is_still_drafting()
    {
        var draft = await PendingDraftAsync();
        using var nurse = await StaffAsync(ApiApplication.NurseEmail);

        using (var scope = _host.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CareLankaDbContext>();
            db.AgentWorkflows.Add(new AgentWorkflow
            {
                Id = Guid.NewGuid(),
                AgentType = AgentType.PatientCareAdvisory,
                EntityType = CareAgentExecutor.WorkflowEntityType,
                EntityId = draft.RecommendationId,
                CorrelationId = Guid.NewGuid(),
                Objective = Services.Patient.CareRecommendationService.Objective,
                Plan = "[]",
                Status = AgentWorkflowStatus.Pending,
                StartedAt = DateTimeOffset.UtcNow.AddMinutes(-1),
                AttemptCount = 0
            });
            await db.SaveChangesAsync();
        }

        var response = await nurse.PostAsync($"/api/care-recommendations/{draft.RecommendationId}/redraft", null);

        await AssertProblemAsync(response, HttpStatusCode.Conflict, "cl_pat_049");
    }

    [Theory]
    [Trait("id", "PT-AI-08e")]
    [InlineData("", HttpStatusCode.BadRequest, "pending_review")]
    [InlineData("   ", HttpStatusCode.BadRequest, "pending_review")]
    [InlineData("ab", HttpStatusCode.BadRequest, "pending_review")]
    [InlineData("abc", HttpStatusCode.OK, "rejected")]
    public async Task A_rejection_reason_needs_at_least_three_characters(
        string reason, HttpStatusCode expected, string statusAfter)
    {
        var draft = await PendingDraftAsync();
        using var doctor = await StaffAsync(ApiApplication.DoctorEmail);

        var response = await doctor.PostAsJsonAsync(
            $"/api/care-recommendations/{draft.RecommendationId}/reject", new { reason });

        Assert.Equal(expected, response.StatusCode);

        using var detail = await Json(await doctor.GetAsync($"/api/care-recommendations/{draft.RecommendationId}"));

        Assert.Equal(statusAfter, detail.RootElement.GetProperty("status").GetString());
    }

    [Fact]
    [Trait("id", "PT-AI-04a")]
    public async Task A_draft_waiting_for_review_shows_the_patient_nothing_but_their_own_report()
    {
        var draft = await PendingDraftAsync();

        using var mine = await Json(await draft.Patient.Client.GetAsync("/api/me/care-recommendations"));
        var row = Row(mine, draft.RecommendationId);
        var keys = row.EnumerateObject().Select(property => property.Name).Order().ToArray();

        Assert.Equal(
            new[]
            {
                "doctor_message", "id", "reported_at", "reported_text", "reviewed_at", "reviewed_by_name",
                "reviewed_by_role", "status"
            },
            keys);
        Assert.Equal("pending_review", row.GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.Null, row.GetProperty("doctor_message").ValueKind);
        Assert.DoesNotContain(RecordingCareModel.Draft, row.GetRawText());
    }

    [Fact]
    [Trait("id", "PT-AI-04b")]
    public async Task A_rejection_reason_never_reaches_the_patient()
    {
        var draft = await PendingDraftAsync();
        using var doctor = await StaffAsync(ApiApplication.DoctorEmail);

        var rejected = await doctor.PostAsJsonAsync(
            $"/api/care-recommendations/{draft.RecommendationId}/reject",
            new { reason = "Zebra-quartz clinical note 7731" });
        Assert.Equal(HttpStatusCode.OK, rejected.StatusCode);

        var raw = await (await draft.Patient.Client.GetAsync("/api/me/care-recommendations")).Content.ReadAsStringAsync();
        using var mine = JsonDocument.Parse(raw);
        var row = Row(mine, draft.RecommendationId);

        Assert.Equal("rejected", row.GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.Null, row.GetProperty("doctor_message").ValueKind);
        Assert.DoesNotContain("Zebra-quartz", raw);
        Assert.DoesNotContain(RecordingCareModel.Draft, raw);
    }

    [Fact]
    [Trait("id", "PT-AI-04c")]
    public async Task An_edited_reply_replaces_the_draft_in_what_the_patient_sees()
    {
        var draft = await PendingDraftAsync();
        using var nurse = await StaffAsync(ApiApplication.NurseEmail);

        var approved = await nurse.PostAsJsonAsync(
            $"/api/care-recommendations/{draft.RecommendationId}/approve",
            new { doctor_message = "Your nurse has read this and will visit you after lunch." });
        Assert.Equal(HttpStatusCode.OK, approved.StatusCode);

        var raw = await (await draft.Patient.Client.GetAsync("/api/me/care-recommendations")).Content.ReadAsStringAsync();
        using var mine = JsonDocument.Parse(raw);

        Assert.Equal(
            "Your nurse has read this and will visit you after lunch.",
            Row(mine, draft.RecommendationId).GetProperty("doctor_message").GetString());
        Assert.DoesNotContain(RecordingCareModel.Draft, raw);
    }

    [Fact]
    [Trait("id", "PT-AI-09")]
    public async Task One_patient_cannot_reach_another_patients_report()
    {
        var a = await PendingDraftAsync();
        var b = await PendingDraftAsync();

        var raw = await (await a.Patient.Client.GetAsync("/api/me/care-recommendations")).Content.ReadAsStringAsync();
        using var mine = JsonDocument.Parse(raw);
        var ids = mine.RootElement.GetProperty("items").EnumerateArray()
            .Select(row => row.GetProperty("id").GetString()).ToArray();

        Assert.Contains(a.RecommendationId.ToString(), ids);
        Assert.DoesNotContain(b.RecommendationId.ToString(), ids);
        Assert.DoesNotContain(b.RecommendationId.ToString(), raw);

        var client = a.Patient.Client;
        var attempts = new[]
        {
            await client.GetAsync($"/api/care-recommendations/{b.RecommendationId}"),
            await client.GetAsync($"/api/care-workflows/{b.WorkflowId}"),
            await Act(client, "approve", b.RecommendationId),
            await Act(client, "reject", b.RecommendationId),
            await Act(client, "redraft", b.RecommendationId)
        };

        Assert.All(attempts, response => Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode));

        using var anonymous = _host.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/me/care-recommendations")).StatusCode);
    }

    [Fact]
    [Trait("id", "PT-AI-12a")]
    public async Task The_model_is_only_sent_the_facts_of_the_patient_who_asked()
    {
        var a = await NewPatientAsync("Latex");
        var b = await NewPatientAsync("Shellfish");
        using var nurse = await StaffAsync(ApiApplication.NurseEmail);

        var aFirst = await SubmitAsync(a, "A earlier question, feeling tired.");
        await nurse.PostAsJsonAsync(
            $"/api/care-recommendations/{aFirst.RecommendationId}/approve",
            new { doctor_message = "Reply for A only: rest well." });

        var bFirst = await SubmitAsync(b, "B earlier question, feeling hungry.");
        await nurse.PostAsJsonAsync(
            $"/api/care-recommendations/{bFirst.RecommendationId}/approve",
            new { doctor_message = "Reply for B only: drink water." });

        await SubmitAsync(a, "A second question about my sleep.");

        var sent = CareReviewHost.Model.Sent.Single(data => data.Contains("A second question about my sleep."));
        using var facts = JsonDocument.Parse(sent);
        var past = facts.RootElement.GetProperty("past_messages");

        Assert.Equal("Latex", facts.RootElement.GetProperty("medical_profile").GetProperty("allergies").GetString());
        Assert.Equal(1, past.GetArrayLength());
        Assert.Equal("Reply for A only: rest well.", past[0].GetProperty("reply_sent").GetString());
        Assert.DoesNotContain("Shellfish", sent);
        Assert.DoesNotContain("Reply for B only", sent);
        Assert.DoesNotContain("B earlier question", sent);
        Assert.DoesNotContain(b.Id.ToString(), sent);
    }

    [Fact]
    [Trait("id", "PT-AI-12b")]
    public async Task A_run_changes_nothing_in_the_patient_admission_bed_or_bill_rows()
    {
        var patient = await NewPatientAsync();
        var before = await SnapshotAsync(patient.Id);

        var run = await SubmitAsync(patient, Quiet);

        Assert.Equal(before, await SnapshotAsync(patient.Id));

        using var scope = _host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CareLankaDbContext>();
        Assert.Equal(1, await db.CareRecommendations.CountAsync(row => row.Id == run.RecommendationId));
    }

    [Fact]
    [Trait("id", "PT-AI-14")]
    public async Task A_finished_run_leaves_its_steps_validation_outcome_and_proposed_change_on_record()
    {
        var draft = await PendingDraftAsync();
        using var doctor = await StaffAsync(ApiApplication.DoctorEmail);

        using var summary = await Json(await doctor.GetAsync($"/api/care-workflows/{draft.WorkflowId}"));
        var root = summary.RootElement;

        Assert.Equal(Plan, root.GetProperty("plan").EnumerateArray().Select(step => step.GetString()).ToArray());
        Assert.Equal(
            Plan,
            root.GetProperty("steps").EnumerateArray().Select(step => step.GetProperty("step").GetString()).ToArray());
        Assert.All(root.GetProperty("steps").EnumerateArray(), step => Assert.True(step.GetProperty("ok").GetBoolean()));
        Assert.Equal("pending_review", root.GetProperty("status").GetString());
        Assert.Equal("drafted", root.GetProperty("outcome").GetString());
        Assert.True(root.GetProperty("validation").GetProperty("passed").GetBoolean());
        Assert.Equal(0, root.GetProperty("validation").GetProperty("failed_rules").GetArrayLength());
        Assert.Equal("model", root.GetProperty("draft_source").GetString());
        Assert.Equal(0, root.GetProperty("retries").GetInt32());

        using var scope = _host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CareLankaDbContext>();
        var workflow = await db.AgentWorkflows.AsNoTracking().SingleAsync(row => row.Id == draft.WorkflowId);
        var changes = await db.AgentProposedChanges.AsNoTracking()
            .Where(row => row.AgentWorkflowId == draft.WorkflowId).ToListAsync();

        Assert.Equal(AgentWorkflowStatus.PendingApproval, workflow.Status);
        Assert.Equal("drafted", workflow.FinalOutcome);
        Assert.Equal(1, workflow.AttemptCount);
        Assert.NotNull(workflow.StartedAt);
        Assert.NotNull(workflow.CompletedAt);

        var change = Assert.Single(changes);
        Assert.Equal(ProposedChangeType.CreateCareRecommendation, change.ChangeType);
        Assert.Equal(draft.RecommendationId, change.TargetEntityId);
        Assert.Equal(ProposedChangeValidationStatus.Passed, change.ValidationStatus);
        Assert.Contains(RecordingCareModel.Draft, change.Payload);
    }

    [Fact]
    [Trait("id", "PT-AI-16a")]
    public async Task A_patient_at_the_limit_does_not_block_a_different_patient()
    {
        var a = await NewPatientAsync();
        var b = await NewPatientAsync();
        await AddReportsAsync(a.Id, 3, TimeSpan.FromSeconds(5));

        var refused = await a.Client.PostAsJsonAsync("/api/me/care-queries", new { reported_text = "One more report from A." });
        await AssertProblemAsync(refused, HttpStatusCode.TooManyRequests, "cl_pat_050");

        var allowed = await b.Client.PostAsJsonAsync("/api/me/care-queries", new { reported_text = "A report from B." });
        using var body = await Json(allowed);

        Assert.Equal(HttpStatusCode.Accepted, allowed.StatusCode);
        await WaitForRunAsync(Guid.Parse(body.RootElement.GetProperty("workflow_id").GetString()!));
    }

    [Fact]
    [Trait("id", "PT-AI-16b")]
    public async Task A_patient_can_report_again_once_the_minute_has_passed()
    {
        var patient = await NewPatientAsync();
        await AddReportsAsync(patient.Id, 3, TimeSpan.FromSeconds(5));

        await MoveReportsBackAsync(patient.Id, TimeSpan.FromSeconds(55));
        var stillInside = await patient.Client.PostAsJsonAsync(
            "/api/me/care-queries", new { reported_text = "Report 55 seconds later." });
        await AssertProblemAsync(stillInside, HttpStatusCode.TooManyRequests, "cl_pat_050");

        await MoveReportsBackAsync(patient.Id, TimeSpan.FromSeconds(61));
        var afterTheMinute = await patient.Client.PostAsJsonAsync(
            "/api/me/care-queries", new { reported_text = "Report 61 seconds later." });
        using var body = await Json(afterTheMinute);

        Assert.Equal(HttpStatusCode.Accepted, afterTheMinute.StatusCode);
        await WaitForRunAsync(Guid.Parse(body.RootElement.GetProperty("workflow_id").GetString()!));
    }

    [Fact]
    [Trait("id", "PT-AI-D19")]
    public async Task Finding_D19_an_edited_reply_with_a_dose_is_approved_without_the_safety_check()
    {
        // Known open finding, not a fix: approving an edited reply never runs CR1 to CR5 again,
        // so a reviewer's edit with a dose reaches the patient unchecked.
        var draft = await PendingDraftAsync();
        using var nurse = await StaffAsync(ApiApplication.NurseEmail);
        const string edited = "Take 500mg of ibuprofen every 4 hours.";

        var approved = await nurse.PostAsJsonAsync(
            $"/api/care-recommendations/{draft.RecommendationId}/approve", new { doctor_message = edited });

        Assert.Equal(HttpStatusCode.OK, approved.StatusCode);

        using var mine = await Json(await draft.Patient.Client.GetAsync("/api/me/care-recommendations"));

        Assert.Equal(edited, Row(mine, draft.RecommendationId).GetProperty("doctor_message").GetString());
    }

    private static Task<HttpResponseMessage> Act(HttpClient client, string action, Guid id)
        => action switch
        {
            "approve" => client.PostAsJsonAsync($"/api/care-recommendations/{id}/approve", new { }),
            "reject" => client.PostAsJsonAsync($"/api/care-recommendations/{id}/reject", new { reason = "Not needed right now." }),
            _ => client.PostAsync($"/api/care-recommendations/{id}/redraft", null)
        };

    private static async Task<JsonDocument> Json(HttpResponseMessage response)
        => JsonDocument.Parse(await response.Content.ReadAsStringAsync());

    private static JsonElement Row(JsonDocument list, Guid recommendationId)
        => list.RootElement.GetProperty("items").EnumerateArray()
            .Single(row => row.GetProperty("id").GetString() == recommendationId.ToString());

    private static async Task AssertProblemAsync(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        var text = await response.Content.ReadAsStringAsync();

        Assert.True(status == response.StatusCode, $"Expected {status} but got {response.StatusCode}: {text}");

        using var body = JsonDocument.Parse(text);

        Assert.Equal(code, body.RootElement.GetProperty("code").GetString());
    }

    private async Task<HttpClient> StaffAsync(string email)
    {
        var client = _host.CreateClient();

        if (!Tokens.TryGetValue(email, out var token))
        {
            var response = await client.PostAsJsonAsync(
                "/api/auth/login", new { email, password = ApiApplication.Password });
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            using var body = await Json(response);
            token = Tokens[email] = body.RootElement.GetProperty("access_token").GetString()!;
        }

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        return client;
    }

    private async Task<string> NurseIdAsync()
    {
        if (_nurseId is not null)
        {
            return _nurseId;
        }

        using var nurse = await StaffAsync(ApiApplication.NurseEmail);
        using var body = await Json(await nurse.GetAsync("/api/auth/me"));

        return _nurseId = body.RootElement.GetProperty("id").GetString()!;
    }

    private async Task<CarePatient> NewPatientAsync(string? allergies = null)
    {
        var client = _host.CreateClient();

        var registered = await client.PostAsJsonAsync("/api/auth/patient/register", new
        {
            username = $"ai.patient.{Random.Shared.Next(1_000_000, 9_999_999)}",
            password = ApiApplication.Password
        });
        Assert.Equal(HttpStatusCode.Created, registered.StatusCode);

        using var registeredBody = await Json(registered);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", registeredBody.RootElement.GetProperty("access_token").GetString());

        var saved = await client.PostAsJsonAsync("/api/me/pre-register", new
        {
            nic = $"N{Guid.NewGuid():N}"[..12],
            full_name = $"Care Patient {Guid.NewGuid():N}"[..24],
            gender = "female"
        });
        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);

        using var savedBody = await Json(saved);
        var code = savedBody.RootElement.GetProperty("patient_code").GetString();

        Guid patientId;
        using (var scope = _host.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CareLankaDbContext>();
            patientId = (await db.Patients.FirstAsync(p => p.PatientCode == code)).Id;
        }

        using var nurse = await StaffAsync(ApiApplication.NurseEmail);

        if (allergies is not null)
        {
            var profile = await nurse.PutAsJsonAsync(
                $"/api/patients/{patientId}/medical-profile", new { allergies });
            Assert.Equal(HttpStatusCode.OK, profile.StatusCode);
        }

        var created = await nurse.PostAsJsonAsync("/api/admissions", new
        {
            patient_id = patientId,
            source = "walk_in",
            admission_category = "general",
            category_set_by_staff_id = await NurseIdAsync(),
            urgency = "routine",
            is_infectious = false
        });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        using var createdBody = await Json(created);
        var admissionId = Guid.Parse(createdBody.RootElement.GetProperty("id").GetString()!);

        using (var scope = _host.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CareLankaDbContext>();
            var admission = await db.Admissions.FirstAsync(a => a.Id == admissionId);
            admission.Status = AdmissionStatus.Admitted;
            await db.SaveChangesAsync();
        }

        return new CarePatient(client, patientId);
    }

    private async Task<CareDraft> SubmitAsync(CarePatient patient, string reportedText)
    {
        var started = await patient.Client.PostAsJsonAsync("/api/me/care-queries", new { reported_text = reportedText });
        Assert.Equal(HttpStatusCode.Accepted, started.StatusCode);

        using var body = await Json(started);
        var workflowId = Guid.Parse(body.RootElement.GetProperty("workflow_id").GetString()!);
        var recommendationId = Guid.Parse(body.RootElement.GetProperty("recommendation_id").GetString()!);

        await WaitForRunAsync(workflowId);

        return new CareDraft(patient, recommendationId, workflowId);
    }

    private async Task<CareDraft> PendingDraftAsync(string? allergies = null)
        => await SubmitAsync(await NewPatientAsync(allergies), Quiet);

    private async Task WaitForRunAsync(Guid workflowId)
    {
        using var doctor = await StaffAsync(ApiApplication.DoctorEmail);
        var deadline = DateTime.UtcNow.AddSeconds(30);

        while (true)
        {
            using var summary = await Json(await doctor.GetAsync($"/api/care-workflows/{workflowId}"));

            if (summary.RootElement.GetProperty("status").GetString() != "running")
            {
                return;
            }

            Assert.True(DateTime.UtcNow < deadline, $"Care workflow {workflowId} was still running after 30 seconds.");

            await Task.Delay(50);
        }
    }

    private async Task<int> WorkflowCountAsync(Guid recommendationId)
    {
        using var scope = _host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CareLankaDbContext>();

        return await db.AgentWorkflows.CountAsync(row => row.EntityId == recommendationId);
    }

    private async Task AddReportsAsync(Guid patientId, int count, TimeSpan ago)
    {
        using var scope = _host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CareLankaDbContext>();
        var admissionId = await db.Admissions.Where(a => a.PatientId == patientId).Select(a => a.Id).FirstAsync();

        for (var i = 1; i <= count; i++)
        {
            db.CareRecommendations.Add(new CareRecommendationRow
            {
                Id = Guid.NewGuid(),
                PatientId = patientId,
                AdmissionId = admissionId,
                ReportedText = $"Setup report {i}.",
                ReportedAt = DateTimeOffset.UtcNow - ago,
                RedFlag = false,
                Status = CareRecommendationStatus.PendingReview
            });
        }

        await db.SaveChangesAsync();
    }

    private async Task MoveReportsBackAsync(Guid patientId, TimeSpan ago)
    {
        using var scope = _host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CareLankaDbContext>();
        var reportedAt = DateTimeOffset.UtcNow - ago;

        await db.CareRecommendations
            .Where(row => row.PatientId == patientId)
            .ExecuteUpdateAsync(set => set.SetProperty(row => row.ReportedAt, reportedAt));
    }

    private async Task<string> SnapshotAsync(Guid patientId)
    {
        using var scope = _host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CareLankaDbContext>();

        var patientUpdatedAt = await db.Patients.AsNoTracking()
            .Where(p => p.Id == patientId).Select(p => p.UpdatedAt).SingleAsync();
        var admissions = await db.Admissions.AsNoTracking()
            .Where(a => a.PatientId == patientId).OrderBy(a => a.Id).ToListAsync();
        var ids = admissions.Select(a => a.Id).ToList();
        var beds = await db.BedAssignments.CountAsync(b => ids.Contains(b.AdmissionId));
        var bills = await db.Bills.CountAsync(b => b.AdmissionId.HasValue && ids.Contains(b.AdmissionId.Value));

        return $"{patientUpdatedAt:O}|"
            + string.Join(",", admissions.Select(a => $"{a.Id}:{a.Status}:{a.UpdatedAt:O}"))
            + $"|beds={beds}|bills={bills}";
    }

    private sealed record CarePatient(HttpClient Client, Guid Id);

    private sealed record CareDraft(CarePatient Patient, Guid RecommendationId, Guid WorkflowId);
}
