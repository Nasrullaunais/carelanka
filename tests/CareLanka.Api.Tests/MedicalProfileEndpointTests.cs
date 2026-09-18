using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace CareLanka.Api.Tests;

[Collection(ApiCollection.Name)]
public sealed class MedicalProfileEndpointTests
{
    private readonly ApiApplication _application;

    public MedicalProfileEndpointTests(ApiApplication application) => _application = application;

    [Fact]
    public async Task A_patient_nobody_has_written_a_profile_for_reads_back_empty_not_missing()
    {
        using var client = await ClientAsync(ApiApplication.NurseEmail);
        var id = await CreatePatientAsync(client);

        var response = await client.GetAsync($"/api/patients/{id}/medical-profile");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var body = await ReadJsonAsync(response);

        Assert.Equal(id, body.RootElement.GetProperty("patient_id").GetString());
        Assert.Equal(JsonValueKind.Null, body.RootElement.GetProperty("allergies").ValueKind);
        Assert.Equal(JsonValueKind.Null, body.RootElement.GetProperty("known_conditions").ValueKind);
        Assert.Equal(JsonValueKind.Null, body.RootElement.GetProperty("updated_at").ValueKind);
    }

    [Fact]
    public async Task A_profile_for_a_patient_who_does_not_exist_is_a_404()
    {
        using var client = await ClientAsync(ApiApplication.NurseEmail);

        var response = await client.GetAsync($"/api/patients/{Guid.NewGuid()}/medical-profile");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task A_nurse_writes_a_profile_and_it_reads_back_with_who_wrote_it()
    {
        using var client = await ClientAsync(ApiApplication.NurseEmail);
        var id = await CreatePatientAsync(client);

        var saved = await client.PutAsJsonAsync($"/api/patients/{id}/medical-profile", new
        {
            known_conditions = "Type 2 diabetes, diagnosed 2019.",
            allergies = "Penicillin",
            current_symptoms = "Headache since admission.",
            recent_situation = "Two days of dizziness at home."
        });

        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);

        using var body = await ReadJsonAsync(await client.GetAsync($"/api/patients/{id}/medical-profile"));

        Assert.Equal("Penicillin", body.RootElement.GetProperty("allergies").GetString());
        Assert.Equal(
            "Type 2 diabetes, diagnosed 2019.",
            body.RootElement.GetProperty("known_conditions").GetString());
        Assert.NotEqual(
            JsonValueKind.Null, body.RootElement.GetProperty("updated_by_staff_name").ValueKind);
        Assert.NotEqual(JsonValueKind.Null, body.RootElement.GetProperty("updated_at").ValueKind);
    }

    [Fact]
    public async Task Writing_twice_replaces_the_one_row_rather_than_adding_a_second()
    {
        using var client = await ClientAsync(ApiApplication.NurseEmail);
        var id = await CreatePatientAsync(client);

        await client.PutAsJsonAsync($"/api/patients/{id}/medical-profile", new
        {
            allergies = "Penicillin"
        });

        await client.PutAsJsonAsync($"/api/patients/{id}/medical-profile", new
        {
            allergies = "Penicillin, sulfa drugs"
        });

        using var body = await ReadJsonAsync(await client.GetAsync($"/api/patients/{id}/medical-profile"));

        Assert.Equal("Penicillin, sulfa drugs", body.RootElement.GetProperty("allergies").GetString());
    }

    [Fact]
    public async Task Leaving_a_field_out_clears_it_because_this_is_a_replace_not_a_patch()
    {
        using var client = await ClientAsync(ApiApplication.NurseEmail);
        var id = await CreatePatientAsync(client);

        await client.PutAsJsonAsync($"/api/patients/{id}/medical-profile", new
        {
            known_conditions = "Asthma since childhood.",
            allergies = "Dust"
        });

        await client.PutAsJsonAsync($"/api/patients/{id}/medical-profile", new
        {
            known_conditions = "Asthma since childhood."
        });

        using var body = await ReadJsonAsync(await client.GetAsync($"/api/patients/{id}/medical-profile"));

        Assert.Equal(JsonValueKind.Null, body.RootElement.GetProperty("allergies").ValueKind);
    }

    [Fact]
    public async Task A_doctor_may_write_one_too()
    {
        using var nurse = await ClientAsync(ApiApplication.NurseEmail);
        using var doctor = await ClientAsync(ApiApplication.DoctorEmail);
        var id = await CreatePatientAsync(nurse);

        var saved = await doctor.PutAsJsonAsync($"/api/patients/{id}/medical-profile", new
        {
            known_conditions = "Hypertension, on medication."
        });

        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
    }

    [Fact]
    public async Task Reception_takes_names_and_money_and_cannot_write_a_clinical_history()
    {
        using var nurse = await ClientAsync(ApiApplication.NurseEmail);
        using var reception = await ClientAsync(ApiApplication.ReceptionEmail);
        var id = await CreatePatientAsync(nurse);

        var write = await reception.PutAsJsonAsync($"/api/patients/{id}/medical-profile", new
        {
            allergies = "Penicillin"
        });

        var read = await reception.GetAsync($"/api/patients/{id}/medical-profile");

        Assert.Equal(HttpStatusCode.Forbidden, write.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, read.StatusCode);
    }

    [Fact]
    public async Task The_duty_manager_reads_the_profile_but_does_not_write_it()
    {
        using var nurse = await ClientAsync(ApiApplication.NurseEmail);
        using var manager = await ClientAsync(ApiApplication.ManagerEmail);
        var id = await CreatePatientAsync(nurse);

        var read = await manager.GetAsync($"/api/patients/{id}/medical-profile");

        var write = await manager.PutAsJsonAsync($"/api/patients/{id}/medical-profile", new
        {
            allergies = "Penicillin"
        });

        Assert.Equal(HttpStatusCode.OK, read.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, write.StatusCode);
    }

    [Fact]
    public async Task An_over_long_field_is_a_400_before_the_service_ever_runs()
    {
        using var client = await ClientAsync(ApiApplication.NurseEmail);
        var id = await CreatePatientAsync(client);

        var response = await client.PutAsJsonAsync($"/api/patients/{id}/medical-profile", new
        {
            allergies = new string('x', 1001)
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        using var body = await ReadJsonAsync(response);

        Assert.Equal("cl_err_400", body.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task An_empty_body_is_a_legitimate_save_because_an_empty_profile_is_ordinary()
    {
        using var client = await ClientAsync(ApiApplication.NurseEmail);
        var id = await CreatePatientAsync(client);

        var response = await client.PutAsJsonAsync($"/api/patients/{id}/medical-profile", new { });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private async Task<string> CreatePatientAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync("/api/patients", new
        {
            full_name = "Profile Subject",
            nic = NewNic(),
            gender = "female"
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        using var body = await ReadJsonAsync(response);
        return body.RootElement.GetProperty("id").GetString()!;
    }

    private static readonly SemaphoreSlim TokenLock = new(1, 1);
    private static readonly Dictionary<string, string> Tokens = new();

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

    private static string NewNic() => $"T{Guid.NewGuid():N}"[..12];
}
