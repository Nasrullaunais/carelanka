using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace CareLanka.Api.Tests;

// A beginner-friendly starter test for the Equipment component.
[Collection(ApiCollection.Name)]
public sealed class EquipmentBeginnerTests
{
    private readonly ApiApplication _application;

    public EquipmentBeginnerTests(ApiApplication application) => _application = application;

    [Fact]
    public async Task Creating_an_equipment_item_without_a_name_is_rejected()
    {
        // 1. Log in as an equipment staff member (this repo's test helper does this for us).
        using var client = await EquipmentClientAsync();

        // 2. Try to create an equipment item, but "forget" the required name field.
        var response = await client.PostAsJsonAsync("/api/equipment-items", new
        {
            category_id = Guid.NewGuid(),
            model = "V-100",
            manufacturer = "Acme Medical",
            purchase_date = "2026-01-05",
            asset_tag = $"EQ-{Guid.NewGuid():N}"[..14],
            ward_id = Guid.NewGuid()
        });

        // 3. Check: the API should refuse this with "400 Bad Request".
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Creating_an_equipment_item_with_a_valid_name_succeeds()
    {
        // This is the "normal / happy path" case: same request as above,
        // but this time we DO include a name, so it should be accepted.
        using var client = await EquipmentClientAsync();

        using var category = await ReadJsonAsync(await client.PostAsJsonAsync(
            "/api/equipment-categories", new { name = $"Category {Guid.NewGuid():N}"[..20] }));

        var response = await client.PostAsJsonAsync("/api/equipment-items", new
        {
            name = "Ventilator",
            category_id = category.RootElement.GetProperty("id").GetGuid(),
            model = "V-100",
            manufacturer = "Acme Medical",
            purchase_date = "2026-01-05",
            asset_tag = $"EQ-{Guid.NewGuid():N}"[..14],
            ward_id = Guid.NewGuid()
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task An_equipment_manager_cannot_confirm_items_because_that_role_is_administrator_only()
    {
        // DEFECT FOUND IN OUR OWN TEST DESIGN (not the app): this test used to be named
        // "...without_the_confirmation_code_is_forbidden" and asserted 403 using the
        // Equipment Manager client, assuming the confirmation-code check was what
        // rejected it. But api/Program.cs:379-380 shows EquipmentConfirmer requires the
        // HospitalAdministrator role only. So an Equipment Manager is rejected by the
        // ROLE check, before the confirmation-code check ever runs - the original test
        // passed, but not for the reason its name claimed. Renamed to test what it
        // actually tests; the real code-check case is covered by the next test below.
        using var client = await EquipmentClientAsync();

        var response = await client.PostAsync(
            $"/api/equipment-items/{Guid.NewGuid()}/confirm", content: null);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task An_administrator_without_the_confirmation_code_header_is_forbidden()
    {
        // This is the real "missing confirmation code" case: an account that DOES hold
        // the required HospitalAdministrator role, but omits the X-Confirmation-Code
        // header, should still be rejected - proving the code check itself works.
        using var client = await ClientAsync(ApiApplication.AdministratorEmail);

        var response = await client.PostAsync(
            $"/api/equipment-items/{Guid.NewGuid()}/confirm", content: null);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task A_name_one_character_over_the_150_character_limit_is_rejected()
    {
        // This is the "boundary/edge" case: CreateEquipmentItemRequest.Name has
        // [MaxLength(150)]. 151 characters should cross the line and fail.
        using var client = await EquipmentClientAsync();

        using var category = await ReadJsonAsync(await client.PostAsJsonAsync(
            "/api/equipment-categories", new { name = $"Category {Guid.NewGuid():N}"[..20] }));

        var response = await client.PostAsJsonAsync("/api/equipment-items", new
        {
            name = new string('A', 151),
            category_id = category.RootElement.GetProperty("id").GetGuid(),
            model = "V-100",
            manufacturer = "Acme Medical",
            purchase_date = "2026-01-05",
            asset_tag = $"EQ-{Guid.NewGuid():N}"[..14],
            ward_id = Guid.NewGuid()
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task An_equipment_manager_cannot_book_maintenance_work_that_belongs_to_the_maintenance_desk()
    {
        // Per Policies.cs: "The maintenance unit is run by the hospital administrator ...
        // The equipment manager reports faults and nothing more here." So an Equipment
        // Manager should be refused, even though they ARE logged in and ARE equipment staff.
        using var client = await EquipmentClientAsync();

        var response = await client.PostAsJsonAsync("/api/maintenance-schedules", new { });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Acknowledging_a_warning_that_does_not_exist_returns_not_found()
    {
        // This is a "failure case": WarningService.AcknowledgeAsync throws NotFoundException
        // when the id doesn't match any warning, which the API should turn into a 404.
        using var client = await EquipmentClientAsync();

        var response = await client.PostAsync($"/api/warnings/{Guid.NewGuid()}/acknowledge", content: null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private Task<HttpClient> EquipmentClientAsync() => ClientAsync(ApiApplication.EquipmentEmail);

    private async Task<HttpClient> ClientAsync(string email)
    {
        var client = _application.CreateClient();

        using var body = await ReadJsonAsync(await client.PostAsJsonAsync(
            "/api/auth/login", new { email, password = ApiApplication.Password }));

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", body.RootElement.GetProperty("access_token").GetString());

        return client;
    }

    private static async Task<JsonDocument> ReadJsonAsync(HttpResponseMessage response) =>
        JsonDocument.Parse(await response.Content.ReadAsStringAsync());
}
