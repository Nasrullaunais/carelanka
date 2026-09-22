using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace CareLanka.Api.Tests;

[Collection(ApiCollection.Name)]
public sealed class PrescriptionEndpointTests
{
    private static readonly byte[] Jpeg = [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46];

    private readonly ApiApplication _application;

    public PrescriptionEndpointTests(ApiApplication application) => _application = application;

    [Fact]
    public async Task A_patient_uploads_a_prescription_and_the_pharmacy_sees_it_waiting_with_their_name()
    {
        using var patient = await LinkedPatientAsync("Rx Waiting Patient");
        using var pharmacy = await StaffAsync(ApiApplication.EquipmentEmail);

        var uploaded = await UploadAsync(patient, note: "After 4pm");
        using var sent = await ReadJsonAsync(uploaded);
        var id = sent.RootElement.GetProperty("id").GetGuid();

        using var waiting = await ReadJsonAsync(await pharmacy.GetAsync("/api/prescriptions?status=submitted"));
        var row = Assert.Single(waiting.RootElement.EnumerateArray(), p => p.GetProperty("id").GetGuid() == id);

        Assert.Equal(HttpStatusCode.Created, uploaded.StatusCode);
        Assert.Equal("submitted", sent.RootElement.GetProperty("status").GetString());
        Assert.Equal("After 4pm", sent.RootElement.GetProperty("note").GetString());
        Assert.Equal("Rx Waiting Patient", row.GetProperty("patient_name").GetString());
    }

    [Fact]
    public async Task Ready_issues_the_next_token_for_the_day_and_the_patient_sees_it()
    {
        using var first = await LinkedPatientAsync("Rx Token One");
        using var second = await LinkedPatientAsync("Rx Token Two");
        using var pharmacy = await StaffAsync(ApiApplication.EquipmentEmail);

        var firstId = await UploadedIdAsync(first);
        var secondId = await UploadedIdAsync(second);

        using var firstReady = await ReadJsonAsync(await pharmacy.PostAsync($"/api/prescriptions/{firstId}/ready", null));
        using var secondReady = await ReadJsonAsync(await pharmacy.PostAsync($"/api/prescriptions/{secondId}/ready", null));
        using var mine = await ReadJsonAsync(await second.GetAsync("/api/me/prescriptions"));

        var firstToken = firstReady.RootElement.GetProperty("token_number").GetInt32();
        var secondToken = secondReady.RootElement.GetProperty("token_number").GetInt32();
        var seen = Assert.Single(mine.RootElement.EnumerateArray());

        Assert.Equal(firstToken + 1, secondToken);
        Assert.Equal("ready", seen.GetProperty("status").GetString());
        Assert.Equal(secondToken, seen.GetProperty("token_number").GetInt32());
    }

    [Fact]
    public async Task Delivered_needs_ready_first_and_the_patient_then_sees_it_delivered()
    {
        using var patient = await LinkedPatientAsync("Rx Delivered");
        using var pharmacy = await StaffAsync(ApiApplication.EquipmentEmail);
        var id = await UploadedIdAsync(patient);

        var tooEarly = await pharmacy.PostAsync($"/api/prescriptions/{id}/deliver", null);
        using var tooEarlyBody = await ReadJsonAsync(tooEarly);

        await pharmacy.PostAsync($"/api/prescriptions/{id}/ready", null);
        var delivered = await pharmacy.PostAsync($"/api/prescriptions/{id}/deliver", null);
        using var mine = await ReadJsonAsync(await patient.GetAsync("/api/me/prescriptions"));
        var seen = Assert.Single(mine.RootElement.EnumerateArray());

        Assert.Equal(HttpStatusCode.Conflict, tooEarly.StatusCode);
        Assert.Equal("cl_equ_020", tooEarlyBody.RootElement.GetProperty("code").GetString());
        Assert.Equal(HttpStatusCode.OK, delivered.StatusCode);
        Assert.Equal("delivered", seen.GetProperty("status").GetString());
        Assert.NotEqual(JsonValueKind.Null, seen.GetProperty("delivered_at").ValueKind);
    }

    [Fact]
    public async Task Cant_fill_needs_a_reason_and_the_patient_sees_it()
    {
        using var patient = await LinkedPatientAsync("Rx Rejected");
        using var pharmacy = await StaffAsync(ApiApplication.EquipmentEmail);
        var id = await UploadedIdAsync(patient);

        var noReason = await pharmacy.PostAsJsonAsync($"/api/prescriptions/{id}/reject", new { reason = "" });
        var rejected = await pharmacy.PostAsJsonAsync(
            $"/api/prescriptions/{id}/reject", new { reason = "The photo is blurred." });
        using var mine = await ReadJsonAsync(await patient.GetAsync("/api/me/prescriptions"));
        var seen = Assert.Single(mine.RootElement.EnumerateArray());

        Assert.Equal(HttpStatusCode.BadRequest, noReason.StatusCode);
        Assert.Equal(HttpStatusCode.OK, rejected.StatusCode);
        Assert.Equal("rejected", seen.GetProperty("status").GetString());
        Assert.Equal("The photo is blurred.", seen.GetProperty("rejection_reason").GetString());
    }

    [Fact]
    public async Task A_patient_only_sees_their_own_prescriptions()
    {
        using var mine = await LinkedPatientAsync("Rx Mine");
        using var theirs = await LinkedPatientAsync("Rx Theirs");

        await UploadedIdAsync(theirs);

        using var body = await ReadJsonAsync(await mine.GetAsync("/api/me/prescriptions"));

        Assert.Empty(body.RootElement.EnumerateArray());
    }

    [Fact]
    public async Task An_account_with_no_hospital_record_is_told_to_add_its_details()
    {
        using var patient = await NewPatientAccountAsync();

        var response = await UploadAsync(patient);
        using var body = await ReadJsonAsync(response);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("cl_pat_033", body.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Only_a_photo_or_a_pdf_is_accepted()
    {
        using var patient = await LinkedPatientAsync("Rx File Type");

        var response = await UploadAsync(patient, contentType: "text/plain", fileName: "notes.txt");
        using var body = await ReadJsonAsync(response);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("cl_equ_023", body.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Staff_cannot_use_the_patient_route_and_a_nurse_cannot_work_the_pharmacy()
    {
        using var nurse = await StaffAsync(ApiApplication.NurseEmail);

        var patientRoute = await nurse.GetAsync("/api/me/prescriptions");
        var pharmacyRoute = await nurse.GetAsync("/api/prescriptions?status=submitted");

        Assert.Equal(HttpStatusCode.Forbidden, patientRoute.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, pharmacyRoute.StatusCode);
    }

    [Fact]
    public async Task The_pharmacy_can_open_the_uploaded_file()
    {
        using var patient = await LinkedPatientAsync("Rx File");
        using var pharmacy = await StaffAsync(ApiApplication.EquipmentEmail);
        var id = await UploadedIdAsync(patient);

        var file = await pharmacy.GetAsync($"/api/prescriptions/{id}/file");

        Assert.Equal(HttpStatusCode.OK, file.StatusCode);
        Assert.Equal("image/jpeg", file.Content.Headers.ContentType?.MediaType);
        Assert.Equal(Jpeg, await file.Content.ReadAsByteArrayAsync());
    }

    private static async Task<Guid> UploadedIdAsync(HttpClient patient)
    {
        using var body = await ReadJsonAsync(await UploadAsync(patient));

        return body.RootElement.GetProperty("id").GetGuid();
    }

    private static Task<HttpResponseMessage> UploadAsync(
        HttpClient patient, string? note = null, string contentType = "image/jpeg", string fileName = "rx.jpg")
    {
        var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(Jpeg);
        file.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        form.Add(file, "File", fileName);

        if (note is not null)
        {
            form.Add(new StringContent(note), "Note");
        }

        return patient.PostAsync("/api/me/prescriptions", form);
    }

    private async Task<HttpClient> LinkedPatientAsync(string fullName)
    {
        var client = await NewPatientAccountAsync();

        var saved = await client.PostAsJsonAsync("/api/me/pre-register", new
        {
            nic = $"N{Guid.NewGuid():N}"[..12],
            full_name = fullName,
            gender = "male"
        });
        saved.EnsureSuccessStatusCode();

        return client;
    }

    private async Task<HttpClient> NewPatientAccountAsync()
    {
        var client = _application.CreateClient();

        using var body = await ReadJsonAsync(await client.PostAsJsonAsync("/api/auth/patient/register", new
        {
            username = $"rx.patient.{Random.Shared.Next(1_000_000, 9_999_999)}",
            password = ApiApplication.Password
        }));

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", body.RootElement.GetProperty("access_token").GetString());

        return client;
    }

    private async Task<HttpClient> StaffAsync(string email)
    {
        var client = _application.CreateClient();

        using var body = await ReadJsonAsync(await client.PostAsJsonAsync(
            "/api/auth/login", new { email, password = ApiApplication.Password }));

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", body.RootElement.GetProperty("access_token").GetString());

        return client;
    }

    private static async Task<JsonDocument> ReadJsonAsync(HttpResponseMessage response)
        => JsonDocument.Parse(await response.Content.ReadAsStringAsync());
}
