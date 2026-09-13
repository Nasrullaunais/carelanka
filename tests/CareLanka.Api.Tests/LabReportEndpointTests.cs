using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Xunit;

namespace CareLanka.Api.Tests;

/// <summary>
/// Filing and reading laboratory results. The point of the feature is that a ward reads a result
/// the moment the lab issues it, so these cover who may file, who may read, and that what comes
/// back out is byte for byte what went in.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class LabReportEndpointTests
{
    // Enough of a PDF to be a real file with real bytes. The API checks the declared content
    // type rather than sniffing, so this does not need to be a valid document.
    private static readonly byte[] Pdf = Encoding.ASCII.GetBytes(
        "%PDF-1.4\n1 0 obj<</Type/Catalog>>endobj\ntrailer<</Root 1 0 R>>\n%%EOF\n");

    private readonly ApiApplication _application;

    public LabReportEndpointTests(ApiApplication application) => _application = application;

    [Fact]
    public async Task A_filed_report_comes_back_byte_for_byte()
    {
        using var lab = await ClientAsync(ApiApplication.EquipmentEmail);
        using var nurse = await ClientAsync(ApiApplication.NurseEmail);
        var patientId = await NewPatientAsync(nurse, "Lab Roundtrip");

        var filed = await UploadAsync(lab, patientId, "Full blood count", Pdf, "application/pdf");
        using var body = await ReadJsonAsync(filed);
        var reportId = body.RootElement.GetProperty("id").GetGuid();

        var download = await nurse.GetAsync($"/api/lab-reports/{reportId}/file");
        var returned = await download.Content.ReadAsByteArrayAsync();

        Assert.Equal(HttpStatusCode.Created, filed.StatusCode);
        Assert.Equal(Pdf, returned);
        Assert.Equal("application/pdf", download.Content.Headers.ContentType?.MediaType);

        // Inline, so a ward reads it on screen instead of it landing in the Downloads folder of
        // a shared nursing-station machine.
        Assert.Equal("inline", download.Content.Headers.ContentDisposition?.DispositionType);
    }

    [Fact]
    public async Task The_list_is_one_patient_newest_first_and_carries_no_file_bytes()
    {
        using var lab = await ClientAsync(ApiApplication.EquipmentEmail);
        using var nurse = await ClientAsync(ApiApplication.NurseEmail);
        var mine = await NewPatientAsync(nurse, "Lab Listing Mine");
        var theirs = await NewPatientAsync(nurse, "Lab Listing Theirs");

        await UploadAsync(lab, mine, "Serum creatinine", Pdf, "application/pdf");
        await UploadAsync(lab, mine, "Full blood count", Pdf, "application/pdf");
        await UploadAsync(lab, theirs, "Urine culture", Pdf, "application/pdf");

        using var body = await ReadJsonAsync(
            await lab.GetAsync($"/api/lab-reports?patientId={mine}"));
        var items = body.RootElement.GetProperty("items").EnumerateArray().ToList();

        // Only this patient's results. A lab screen opened on one person must not leak another.
        Assert.Equal(2, items.Count);

        // Newest first: a ward wants the latest result at the top.
        Assert.Equal("Full blood count", items[0].GetProperty("test_name").GetString());

        // The bytes are a separate request. A list carrying them would read every stored PDF
        // out of the database to render a table of filenames.
        Assert.False(items[0].TryGetProperty("content", out _));
    }

    [Fact]
    public async Task Only_the_laboratory_may_file_a_result()
    {
        using var lab = await ClientAsync(ApiApplication.EquipmentEmail);
        using var nurse = await ClientAsync(ApiApplication.NurseEmail);
        var patientId = await NewPatientAsync(nurse, "Lab Author Rules");

        var byTheLab = await UploadAsync(lab, patientId, "Full blood count", Pdf, "application/pdf");
        var byTheNurse = await UploadAsync(nurse, patientId, "Full blood count", Pdf, "application/pdf");

        // A nurse reads a result and acts on it. Issuing one is the laboratory's work, and a
        // nurse who could file a result could file one nobody ran.
        Assert.Equal(HttpStatusCode.Created, byTheLab.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, byTheNurse.StatusCode);
    }

    [Theory]
    [InlineData(ApiApplication.DoctorEmail, HttpStatusCode.OK)]
    [InlineData(ApiApplication.NurseEmail, HttpStatusCode.OK)]
    [InlineData(ApiApplication.ManagerEmail, HttpStatusCode.OK)]
    [InlineData(ApiApplication.EquipmentEmail, HttpStatusCode.OK)]
    // A result is clinical information about a named person. An administrator is off it for the
    // same reason they cannot read admissions.
    [InlineData(ApiApplication.AdministratorEmail, HttpStatusCode.Forbidden)]
    public async Task Reading_results_is_clinical_staff_and_the_laboratory(
        string email, HttpStatusCode expected)
    {
        using var nurse = await ClientAsync(ApiApplication.NurseEmail);
        var patientId = await NewPatientAsync(nurse, $"Lab Reader {Guid.NewGuid():N}"[..20]);

        using var client = await ClientAsync(email);
        var response = await client.GetAsync($"/api/lab-reports?patientId={patientId}");

        Assert.Equal(expected, response.StatusCode);
    }

    [Fact]
    public async Task A_result_cannot_be_filed_against_a_patient_who_does_not_exist()
    {
        using var lab = await ClientAsync(ApiApplication.EquipmentEmail);

        var response = await UploadAsync(
            lab, Guid.NewGuid().ToString(), "Full blood count", Pdf, "application/pdf");

        // Worse than a rejected upload: nothing errors, and the ward waits for a result sitting
        // under a mistyped id.
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [InlineData("text/plain")]
    [InlineData("application/zip")]
    public async Task A_report_has_to_be_something_a_ward_can_open(string contentType)
    {
        using var lab = await ClientAsync(ApiApplication.EquipmentEmail);
        using var nurse = await ClientAsync(ApiApplication.NurseEmail);
        var patientId = await NewPatientAsync(nurse, "Lab File Type");

        var response = await UploadAsync(lab, patientId, "Full blood count", Pdf, contentType);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task An_empty_file_is_refused()
    {
        using var lab = await ClientAsync(ApiApplication.EquipmentEmail);
        using var nurse = await ClientAsync(ApiApplication.NurseEmail);
        var patientId = await NewPatientAsync(nurse, "Lab Empty File");

        var response = await UploadAsync(
            lab, patientId, "Full blood count", Array.Empty<byte>(), "application/pdf");

        // A row that looks like a result and opens as nothing. Almost always a scanner that
        // did not finish.
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task The_uploader_is_taken_from_the_token_not_the_form()
    {
        using var lab = await ClientAsync(ApiApplication.EquipmentEmail);
        using var nurse = await ClientAsync(ApiApplication.NurseEmail);
        var patientId = await NewPatientAsync(nurse, "Lab Uploader");

        using var me = await ReadJsonAsync(await lab.GetAsync("/api/auth/me"));
        var labStaffId = me.RootElement.GetProperty("id").GetGuid();

        using var filed = await ReadJsonAsync(
            await UploadAsync(lab, patientId, "Full blood count", Pdf, "application/pdf"));

        Assert.Equal(labStaffId, filed.RootElement.GetProperty("uploaded_by_staff_id").GetGuid());
    }

    [Fact]
    public async Task The_list_is_how_the_lab_finds_a_patient_without_typing_an_identifier()
    {
        using var lab = await ClientAsync(ApiApplication.EquipmentEmail);
        using var nurse = await ClientAsync(ApiApplication.NurseEmail);

        // An outpatient in for a blood test: no bed, admitted the moment the record opens. The
        // cheapest current visit to make, and exactly the case the ward filter must not hide.
        var patientId = await NewPatientAsync(nurse, "Lab Ward Listing");
        await NewOutpatientVisitAsync(nurse, patientId);

        using var everyone = await ReadJsonAsync(
            await lab.GetAsync("/api/lab-reports/patients?pageSize=100"));
        var rows = everyone.RootElement.GetProperty("items").EnumerateArray().ToList();
        var mine = rows.Single(row => row.GetProperty("patient_id").GetString() == patientId);

        // What a lab needs to work down a rack of specimens: who, and where they are.
        Assert.Equal("Lab Ward Listing", mine.GetProperty("full_name").GetString());
        Assert.False(string.IsNullOrWhiteSpace(mine.GetProperty("patient_code").GetString()));
        Assert.Equal("admitted", mine.GetProperty("admission_status").GetString());

        // Null rather than absent. The column renders "No bed" instead of going blank.
        Assert.Equal(JsonValueKind.Null, mine.GetProperty("ward_name").ValueKind);
    }

    [Fact]
    public async Task Asking_for_one_ward_leaves_out_everybody_who_is_not_in_it()
    {
        using var lab = await ClientAsync(ApiApplication.EquipmentEmail);
        using var nurse = await ClientAsync(ApiApplication.NurseEmail);

        var patientId = await NewPatientAsync(nurse, "Lab Ward Filter");
        await NewOutpatientVisitAsync(nurse, patientId);

        using var everyone = await ReadJsonAsync(
            await lab.GetAsync("/api/lab-reports/patients?pageSize=100"));
        using var inWard = await ReadJsonAsync(
            await lab.GetAsync("/api/lab-reports/patients?wardName=General-A&pageSize=100"));

        // Present when nobody asked for a ward, gone when they did. Somebody holding no bed at
        // all must not leak into a ward's list.
        Assert.Contains(
            patientId,
            everyone.RootElement.GetProperty("items").EnumerateArray()
                .Select(row => row.GetProperty("patient_id").GetString()));

        var wardRows = inWard.RootElement.GetProperty("items").EnumerateArray().ToList();
        Assert.DoesNotContain(
            patientId, wardRows.Select(row => row.GetProperty("patient_id").GetString()));
        Assert.All(wardRows, row => Assert.Equal("General-A", row.GetProperty("ward_name").GetString()));
    }

    [Fact]
    public async Task A_ward_nobody_is_in_is_an_empty_list_not_an_error()
    {
        using var lab = await ClientAsync(ApiApplication.EquipmentEmail);

        var response = await lab.GetAsync("/api/lab-reports/patients?wardName=No+Such+Ward");
        using var body = await ReadJsonAsync(response);

        // An empty ward is an ordinary answer. A 404 here would put a red box over a screen
        // that is working correctly.
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Empty(body.RootElement.GetProperty("items").EnumerateArray());
        Assert.Equal(1, body.RootElement.GetProperty("total_pages").GetInt32());
    }

    [Theory]
    [InlineData(ApiApplication.NurseEmail, HttpStatusCode.OK)]
    [InlineData(ApiApplication.EquipmentEmail, HttpStatusCode.OK)]
    [InlineData(ApiApplication.AdministratorEmail, HttpStatusCode.Forbidden)]
    public async Task The_ward_list_is_gated_the_same_way_as_the_results(
        string email, HttpStatusCode expected)
    {
        using var client = await ClientAsync(email);

        var response = await client.GetAsync("/api/lab-reports/patients");

        Assert.Equal(expected, response.StatusCode);
    }

    private static async Task<HttpResponseMessage> UploadAsync(
        HttpClient client, string patientId, string testName, byte[] bytes, string contentType)
    {
        using var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new MediaTypeHeaderValue(contentType);

        form.Add(new StringContent(patientId), "PatientId");
        form.Add(new StringContent(testName), "TestName");
        form.Add(file, "File", "report.pdf");

        return await client.PostAsync("/api/lab-reports", form);
    }

    /// <summary>
    /// A visit that needs no bed, so it opens straight at `admitted` with no ward. The cheapest
    /// current visit there is: no ward, no bed, no assignment.
    /// </summary>
    private static async Task<string> NewOutpatientVisitAsync(HttpClient nurse, string patientId)
    {
        using var me = await ReadJsonAsync(await nurse.GetAsync("/api/auth/me"));

        var created = await nurse.PostAsJsonAsync("/api/admissions", new
        {
            patient_id = patientId,
            source = "walk_in",
            admission_category = "outpatient",
            category_set_by_staff_id = me.RootElement.GetProperty("id").GetString(),
            urgency = "routine",
            is_infectious = false
        });

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        using var body = await ReadJsonAsync(created);
        return body.RootElement.GetProperty("id").GetString()!;
    }

    private static async Task<string> NewPatientAsync(HttpClient client, string fullName)
    {
        var created = await client.PostAsJsonAsync("/api/patients", new
        {
            full_name = fullName,
            gender = "female"
        });

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        using var body = await ReadJsonAsync(created);
        return body.RootElement.GetProperty("id").GetString()!;
    }

    private async Task<HttpClient> ClientAsync(string email)
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
