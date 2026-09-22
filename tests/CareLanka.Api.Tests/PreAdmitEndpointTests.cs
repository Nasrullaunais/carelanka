using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using CareLanka.Api.Data;
using CareLanka.Api.Data.Entities.Common;
using CareLanka.Api.Data.Entities.Patient;
using CareLanka.Api.Data.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CareLanka.Api.Tests;

[Collection(ApiCollection.Name)]
public sealed class PreAdmitEndpointTests
{
    private readonly ApiApplication _application;

    public PreAdmitEndpointTests(ApiApplication application) => _application = application;

    [Fact]
    public async Task A_pre_admission_for_the_caller_starts_unclassified_and_awaiting_a_bed()
    {
        using var nurse = await ClientAsync(ApiApplication.NurseEmail);
        var patientId = await NewPatientAsync(nurse, "Pre-Admit Caller");

        using var body = await ReadJsonAsync(await PreAdmitAsync(nurse, new
        {
            dispatch_id = $"DSP-{Guid.NewGuid():N}",
            patient_is_caller = true,
            patient_id = patientId,
            expected_arrival = DateTimeOffset.UtcNow.AddMinutes(20),
            urgency = "emergency"
        }));

        Assert.Equal("awaiting_bed", body.RootElement.GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.Null, body.RootElement.GetProperty("admission_category").ValueKind);
        Assert.Equal(patientId, body.RootElement.GetProperty("patient").GetProperty("id").GetString());
    }

    [Fact]
    public async Task A_pre_admission_for_someone_else_creates_a_new_patient_with_the_callers_contact_details()
    {
        using var nurse = await ClientAsync(ApiApplication.NurseEmail);
        var (callerUserId, callerName, callerPhone) = await SeedCallerAsync();

        using var body = await ReadJsonAsync(await PreAdmitAsync(nurse, new
        {
            dispatch_id = $"DSP-{Guid.NewGuid():N}",
            patient_is_caller = false,
            caller_user_id = callerUserId,
            provisional_name = "Father of Caller",
            provisional_gender = "male",
            expected_arrival = DateTimeOffset.UtcNow.AddMinutes(20),
            urgency = "urgent"
        }));

        Assert.Equal("Father of Caller", body.RootElement.GetProperty("patient").GetProperty("full_name").GetString());

        using var admin = await ClientAsync(ApiApplication.AdministratorEmail);
        var patientId = body.RootElement.GetProperty("patient").GetProperty("id").GetString();
        using var patientDetail = await ReadJsonAsync(await admin.GetAsync($"/api/patients/{patientId}"));

        Assert.Equal(callerName, patientDetail.RootElement.GetProperty("emergency_contact_name").GetString());
        Assert.Equal(callerPhone, patientDetail.RootElement.GetProperty("emergency_contact_phone").GetString());
    }

    private async Task<(string UserAccountId, string FullName, string Phone)> SeedCallerAsync()
    {
        using var scope = _application.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CareLankaDbContext>();

        var account = new PatientAccount
        {
            Id = Guid.NewGuid(),
            Username = $"caller{Guid.NewGuid():N}"[..20],
            PasswordHash = "not-used-in-this-test"
        };

        var phone = $"07{Random.Shared.NextInt64(10000000, 99999999)}";
        var patient = new Patient
        {
            Id = Guid.NewGuid(),
            PatientCode = $"P{Guid.NewGuid():N}"[..8].ToUpperInvariant(),
            FullName = "The Caller Themselves",
            Gender = Gender.Female,
            Phone = phone,
            UserAccountId = account.Id
        };

        db.PatientAccounts.Add(account);
        db.Patients.Add(patient);
        await db.SaveChangesAsync();

        return (account.Id.ToString(), patient.FullName, phone);
    }

    [Fact]
    public async Task Pre_admitting_the_same_dispatch_twice_is_a_409()
    {
        using var nurse = await ClientAsync(ApiApplication.NurseEmail);
        var patientId = await NewPatientAsync(nurse, "Duplicate Dispatch");
        var dispatchId = $"DSP-{Guid.NewGuid():N}";

        var body = new
        {
            dispatch_id = dispatchId,
            patient_is_caller = true,
            patient_id = patientId,
            expected_arrival = DateTimeOffset.UtcNow.AddMinutes(20),
            urgency = "routine"
        };

        Assert.Equal(HttpStatusCode.Created, (await PreAdmitAsync(nurse, body)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await PreAdmitAsync(nurse, body)).StatusCode);
    }

    [Fact]
    public async Task Classifying_a_pre_admission_stamps_the_acting_staff_member_and_sets_the_category()
    {
        using var nurse = await ClientAsync(ApiApplication.NurseEmail);
        var nurseId = await NurseIdAsync();
        var id = await NewPreAdmissionIdAsync(nurse, "Pre-Admit Classify");

        var classified = await nurse.PostAsJsonAsync(
            $"/api/admissions/{id}/classify", new { admission_category = "inpatient" });

        Assert.Equal(HttpStatusCode.OK, classified.StatusCode);
        using var body = await ReadJsonAsync(classified);

        Assert.Equal("inpatient", body.RootElement.GetProperty("admission_category").GetString());
        Assert.Equal(nurseId, body.RootElement.GetProperty("category_set_by_staff_id").GetString());
        Assert.Equal("awaiting_bed", body.RootElement.GetProperty("status").GetString());
    }

    [Fact]
    public async Task Classifying_an_already_classified_admission_is_a_409()
    {
        using var nurse = await ClientAsync(ApiApplication.NurseEmail);
        var id = await NewPreAdmissionIdAsync(nurse, "Pre-Admit Double Classify");

        Assert.Equal(
            HttpStatusCode.OK,
            (await nurse.PostAsJsonAsync($"/api/admissions/{id}/classify", new { admission_category = "hdu" }))
                .StatusCode);

        Assert.Equal(
            HttpStatusCode.Conflict,
            (await nurse.PostAsJsonAsync($"/api/admissions/{id}/classify", new { admission_category = "icu" }))
                .StatusCode);
    }

    [Fact]
    public async Task Classifying_as_outpatient_admits_immediately_without_a_bed()
    {
        using var nurse = await ClientAsync(ApiApplication.NurseEmail);
        var id = await NewPreAdmissionIdAsync(nurse, "Pre-Admit Outpatient");

        using var body = await ReadJsonAsync(await nurse.PostAsJsonAsync(
            $"/api/admissions/{id}/classify", new { admission_category = "outpatient" }));

        Assert.Equal("admitted", body.RootElement.GetProperty("status").GetString());
        Assert.NotEqual(JsonValueKind.Null, body.RootElement.GetProperty("admitted_at").ValueKind);
    }

    private static Task<HttpResponseMessage> PreAdmitAsync(HttpClient client, object body)
        => client.PostAsJsonAsync("/api/admissions/pre-admit", body);

    private async Task<string> NewPreAdmissionIdAsync(HttpClient nurse, string fullName)
    {
        var patientId = await NewPatientAsync(nurse, fullName);

        using var body = await ReadJsonAsync(await PreAdmitAsync(nurse, new
        {
            dispatch_id = $"DSP-{Guid.NewGuid():N}",
            patient_is_caller = true,
            patient_id = patientId,
            expected_arrival = DateTimeOffset.UtcNow.AddMinutes(20),
            urgency = "routine"
        }));

        return body.RootElement.GetProperty("id").GetString()!;
    }

    private static async Task<string> NewPatientAsync(HttpClient client, string fullName)
    {
        var created = await client.PostAsJsonAsync("/api/patients", new
        {
            full_name = fullName,
            gender = "male"
        });

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        using var body = await ReadJsonAsync(created);
        return body.RootElement.GetProperty("id").GetString()!;
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
}
