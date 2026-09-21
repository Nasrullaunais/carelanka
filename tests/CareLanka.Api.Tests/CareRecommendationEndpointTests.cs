using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using CareLanka.Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CareLanka.Api.Tests;

/// <summary>
/// End to end through the care advisory agent: a patient with no open stay is refused before
/// anything runs, an admitted patient's report is drafted and held for review, a red-flag report
/// is escalated and forced to high urgency, and only once a Doctor or Ward Nurse approves does the
/// patient's own view show anything at all.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class CareRecommendationEndpointTests
{
    private readonly ApiApplication _application;

    public CareRecommendationEndpointTests(ApiApplication application) => _application = application;

    [Fact]
    public async Task A_patient_with_no_open_stay_is_refused_before_anything_runs()
    {
        var (patient, _) = await NewAdmittablePatientAsync();

        var response = await patient.PostAsJsonAsync(
            "/api/me/care-queries", new { reported_text = "I have had a mild headache since yesterday." });

        using var body = await ReadJsonAsync(response);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("cl_pat_038", body.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task An_admitted_patients_report_is_drafted_and_held_for_review()
    {
        var (patient, patientId) = await NewAdmittablePatientAsync();
        using var nurse = await StaffClientAsync(ApiApplication.NurseEmail);
        await AdmitAsOutpatientAsync(nurse, patientId);

        using var summary = await SubmitAndWaitAsync(
            patient, "My headache is worse today and it hurts more when I lie flat.");

        Assert.Equal("pending_review", summary.RootElement.GetProperty("status").GetString());
        Assert.False(summary.RootElement.GetProperty("red_flag").GetBoolean());
        Assert.True(summary.RootElement.GetProperty("validation").GetProperty("passed").GetBoolean());

        var recommendationId = summary.RootElement.GetProperty("recommendation_id").GetString()!;

        // The patient's own list never shows a draft still awaiting review.
        using var mine = await ReadJsonAsync(await patient.GetAsync("/api/me/care-recommendations"));
        var mineRow = mine.RootElement.GetProperty("items").EnumerateArray()
            .Single(row => row.GetProperty("id").GetString() == recommendationId);

        Assert.Equal("pending_review", mineRow.GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.Null, mineRow.GetProperty("doctor_message").ValueKind);
    }

    [Fact]
    public async Task A_red_flag_report_is_escalated_and_urgency_is_forced_high()
    {
        var (patient, patientId) = await NewAdmittablePatientAsync();
        using var nurse = await StaffClientAsync(ApiApplication.NurseEmail);
        await AdmitAsOutpatientAsync(nurse, patientId);

        using var summary = await SubmitAndWaitAsync(patient, "I have sudden chest pain and I feel dizzy.");

        Assert.True(summary.RootElement.GetProperty("red_flag").GetBoolean());
        Assert.Equal("escalated", summary.RootElement.GetProperty("outcome").GetString());

        var recommendationId = summary.RootElement.GetProperty("recommendation_id").GetString()!;

        using var doctor = await StaffClientAsync(ApiApplication.DoctorEmail);
        using var detail = await ReadJsonAsync(
            await doctor.GetAsync($"/api/care-recommendations/{recommendationId}"));

        Assert.Equal("high", detail.RootElement.GetProperty("urgency_flag").GetString());
    }

    [Fact]
    public async Task A_ward_nurse_can_approve_and_only_then_does_the_patient_see_a_message()
    {
        var (patient, patientId) = await NewAdmittablePatientAsync();
        using var nurse = await StaffClientAsync(ApiApplication.NurseEmail);
        await AdmitAsOutpatientAsync(nurse, patientId);

        using var summary = await SubmitAndWaitAsync(patient, "I feel a little more tired than usual today.");
        var recommendationId = summary.RootElement.GetProperty("recommendation_id").GetString()!;

        var approved = await nurse.PostAsJsonAsync(
            $"/api/care-recommendations/{recommendationId}/approve",
            new { doctor_message = "A nurse will check on you this afternoon." });

        Assert.Equal(HttpStatusCode.OK, approved.StatusCode);
        using var approvedBody = await ReadJsonAsync(approved);
        Assert.Equal("approved", approvedBody.RootElement.GetProperty("status").GetString());

        using var mine = await ReadJsonAsync(await patient.GetAsync("/api/me/care-recommendations"));
        var mineRow = mine.RootElement.GetProperty("items").EnumerateArray()
            .Single(row => row.GetProperty("id").GetString() == recommendationId);

        Assert.Equal("approved", mineRow.GetProperty("status").GetString());
        Assert.Equal(
            "A nurse will check on you this afternoon.",
            mineRow.GetProperty("doctor_message").GetString());
    }

    [Fact]
    public async Task A_doctor_can_reject_and_the_patient_sees_no_reason_and_no_message()
    {
        var (patient, patientId) = await NewAdmittablePatientAsync();
        using var nurse = await StaffClientAsync(ApiApplication.NurseEmail);
        await AdmitAsOutpatientAsync(nurse, patientId);

        using var summary = await SubmitAndWaitAsync(patient, "Just checking in, feeling okay.");
        var recommendationId = summary.RootElement.GetProperty("recommendation_id").GetString()!;

        using var doctor = await StaffClientAsync(ApiApplication.DoctorEmail);
        var rejected = await doctor.PostAsJsonAsync(
            $"/api/care-recommendations/{recommendationId}/reject",
            new { reason = "Nothing clinically new; already covered on the ward round." });

        Assert.Equal(HttpStatusCode.OK, rejected.StatusCode);

        using var mine = await ReadJsonAsync(await patient.GetAsync("/api/me/care-recommendations"));
        var mineRow = mine.RootElement.GetProperty("items").EnumerateArray()
            .Single(row => row.GetProperty("id").GetString() == recommendationId);

        Assert.Equal("rejected", mineRow.GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.Null, mineRow.GetProperty("doctor_message").ValueKind);
    }

    [Fact]
    public async Task A_duty_manager_can_see_the_queue_but_cannot_approve()
    {
        var (patient, patientId) = await NewAdmittablePatientAsync();
        using var nurse = await StaffClientAsync(ApiApplication.NurseEmail);
        await AdmitAsOutpatientAsync(nurse, patientId);

        using var summary = await SubmitAndWaitAsync(patient, "Feeling a bit unsettled today.");
        var recommendationId = summary.RootElement.GetProperty("recommendation_id").GetString()!;

        using var manager = await StaffClientAsync(ApiApplication.ManagerEmail);

        var read = await manager.GetAsync($"/api/care-recommendations/{recommendationId}");
        Assert.Equal(HttpStatusCode.OK, read.StatusCode);

        var approve = await manager.PostAsJsonAsync(
            $"/api/care-recommendations/{recommendationId}/approve", new { });
        Assert.Equal(HttpStatusCode.Forbidden, approve.StatusCode);
    }

    private async Task<(HttpClient Client, Guid PatientId)> NewAdmittablePatientAsync()
    {
        var client = _application.CreateClient();

        var registered = await client.PostAsJsonAsync("/api/auth/patient/register", new
        {
            username = $"care.patient.{Random.Shared.Next(1_000_000, 9_999_999)}",
            password = ApiApplication.Password
        });

        Assert.Equal(HttpStatusCode.Created, registered.StatusCode);

        using var registeredBody = await ReadJsonAsync(registered);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", registeredBody.RootElement.GetProperty("access_token").GetString());

        var saved = await client.PostAsJsonAsync("/api/me/pre-register", new
        {
            nic = $"N{Guid.NewGuid():N}"[..12],
            full_name = $"Care Patient {Guid.NewGuid():N}"[..24],
            gender = "female"
        });

        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);

        using var savedBody = await ReadJsonAsync(saved);
        var code = savedBody.RootElement.GetProperty("patient_code").GetString();

        using var scope = _application.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CareLankaDbContext>();
        var record = await db.Patients.FirstAsync(p => p.PatientCode == code);

        return (client, record.Id);
    }

    private async Task AdmitAsOutpatientAsync(HttpClient nurse, Guid patientId)
    {
        var created = await nurse.PostAsJsonAsync("/api/admissions", new
        {
            patient_id = patientId,
            source = "walk_in",
            admission_category = "outpatient",
            category_set_by_staff_id = await NurseIdAsync(),
            urgency = "routine",
            is_infectious = false
        });

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        using var body = await ReadJsonAsync(created);
        Assert.Equal("admitted", body.RootElement.GetProperty("status").GetString());
    }

    /// <summary>
    /// Starts a care query as the patient and polls its published workflow with a staff token -
    /// only staff may read /care-workflows/{id} - until the run leaves "running". Fails loudly
    /// rather than hanging if it never does.
    /// </summary>
    private async Task<JsonDocument> SubmitAndWaitAsync(HttpClient patient, string reportedText)
    {
        var started = await patient.PostAsJsonAsync(
            "/api/me/care-queries", new { reported_text = reportedText });

        Assert.Equal(HttpStatusCode.Accepted, started.StatusCode);

        using var accepted = await ReadJsonAsync(started);
        var workflowId = accepted.RootElement.GetProperty("workflow_id").GetString()!;

        using var poller = await StaffClientAsync(ApiApplication.DoctorEmail);
        var deadline = DateTime.UtcNow.AddSeconds(30);

        while (true)
        {
            var response = await poller.GetAsync($"/api/care-workflows/{workflowId}");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var summary = await ReadJsonAsync(response);

            if (summary.RootElement.GetProperty("status").GetString() != "running")
            {
                return summary;
            }

            summary.Dispose();

            Assert.True(
                DateTime.UtcNow < deadline, $"Care workflow {workflowId} was still running after 30 seconds.");

            await Task.Delay(50);
        }
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

        using var client = await StaffClientAsync(ApiApplication.NurseEmail);
        using var body = await ReadJsonAsync(await client.GetAsync("/api/auth/me"));

        return _nurseId = body.RootElement.GetProperty("id").GetString()!;
    }

    private async Task<HttpClient> StaffClientAsync(string email)
    {
        var client = _application.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", await StaffTokenAsync(email));

        return client;
    }

    private async Task<string> StaffTokenAsync(string email)
    {
        await TokenLock.WaitAsync();

        try
        {
            if (Tokens.TryGetValue(email, out var cached))
            {
                return cached;
            }

            using var client = _application.CreateClient();

            var response = await client.PostAsJsonAsync(
                "/api/auth/login", new { email, password = ApiApplication.Password });

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
