using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace CareLanka.Api.Tests;

// The picker behind two screens: the laboratory filing a result, and the equipment register
// assigning an item. Both need a patient to choose rather than an id to type.
[Collection(ApiCollection.Name)]
public sealed class WardPatientEndpointTests
{
    private readonly ApiApplication _application;

    public WardPatientEndpointTests(ApiApplication application) => _application = application;


    [Fact]
    public async Task The_list_is_how_a_screen_offers_a_patient_instead_of_an_id()
    {
        using var equipment = await ClientAsync(ApiApplication.EquipmentEmail);
        using var nurse = await ClientAsync(ApiApplication.NurseEmail);

        // An outpatient in for a blood test: no bed, admitted the moment the record opens. The
        // cheapest current visit to make, and exactly the case the ward filter must not hide.
        var patientId = await NewPatientAsync(nurse, "Lab Ward Listing");
        var admissionId = await NewOutpatientVisitAsync(nurse, patientId);

        using var everyone = await ReadJsonAsync(
            await equipment.GetAsync("/api/ward-patients?pageSize=100"));
        var rows = everyone.RootElement.GetProperty("items").EnumerateArray().ToList();
        var mine = rows.Single(row => row.GetProperty("patient_id").GetString() == patientId);

        // What either screen needs: who, where they are, and both ids.
        Assert.Equal("Lab Ward Listing", mine.GetProperty("full_name").GetString());
        Assert.Equal(admissionId, mine.GetProperty("admission_id").GetString());
        Assert.False(string.IsNullOrWhiteSpace(mine.GetProperty("patient_code").GetString()));
        Assert.Equal("admitted", mine.GetProperty("admission_status").GetString());

        // Null rather than absent. The column renders "No bed" instead of going blank.
        Assert.Equal(JsonValueKind.Null, mine.GetProperty("ward_name").ValueKind);
    }

    [Fact]
    public async Task Asking_for_one_ward_leaves_out_everybody_who_is_not_in_it()
    {
        using var equipment = await ClientAsync(ApiApplication.EquipmentEmail);
        using var nurse = await ClientAsync(ApiApplication.NurseEmail);

        var patientId = await NewPatientAsync(nurse, "Lab Ward Filter");
        await NewOutpatientVisitAsync(nurse, patientId);

        using var everyone = await ReadJsonAsync(
            await equipment.GetAsync("/api/ward-patients?pageSize=100"));
        using var inWard = await ReadJsonAsync(
            await equipment.GetAsync("/api/ward-patients?wardName=General-A&pageSize=100"));

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
        using var equipment = await ClientAsync(ApiApplication.EquipmentEmail);

        var response = await equipment.GetAsync("/api/ward-patients?wardName=No+Such+Ward");
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
    public async Task The_ward_list_is_clinical_staff_and_the_laboratory(
        string email, HttpStatusCode expected)
    {
        using var client = await ClientAsync(email);

        var response = await client.GetAsync("/api/ward-patients");

        Assert.Equal(expected, response.StatusCode);
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
