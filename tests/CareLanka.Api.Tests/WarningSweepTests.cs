using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace CareLanka.Api.Tests;

// The database is shared by the whole collection and the sweep looks at all of it, so every
// assertion here picks out the warnings about the one medicine or machine the test created.
[Collection(ApiCollection.Name)]
public sealed class WarningSweepTests
{
    private const string CodeHeader = "X-Confirmation-Code";

    private readonly ApiApplication _application;

    public WarningSweepTests(ApiApplication application) => _application = application;

    [Fact]
    public async Task Low_stock_is_raised_once_and_closes_when_a_delivery_arrives()
    {
        using var equipment = await ClientAsync(ApiApplication.EquipmentEmail);
        var itemId = await NewMedicineAsync(equipment, quantity: 4, threshold: 10);

        await SweepAsync(equipment);
        await SweepAsync(equipment);
        var raised = await WarningsAboutAsync(equipment, itemId, "low_stock");

        var warning = Assert.Single(raised);
        Assert.Equal("open", warning.GetProperty("status").GetString());
        Assert.Equal("high", warning.GetProperty("severity").GetString());
        Assert.Equal("system", warning.GetProperty("raised_by").GetString());
        Assert.Contains("down to 4 box", warning.GetProperty("recommended_action").GetString());

        var delivered = await equipment.PostAsJsonAsync(
            $"/api/pharmacy-items/{itemId}/batches", new { quantity = 50 });
        delivered.EnsureSuccessStatusCode();
        using var result = await SweepAsync(equipment);
        var after = Assert.Single(await WarningsAboutAsync(equipment, itemId, "low_stock"));

        Assert.True(result.RootElement.GetProperty("resolved").GetInt32() >= 1);
        Assert.Equal("action_taken", after.GetProperty("status").GetString());
        Assert.NotEqual(JsonValueKind.Null, after.GetProperty("resolved_at").ValueKind);
    }

    [Fact]
    public async Task An_empty_shelf_is_critical()
    {
        using var equipment = await ClientAsync(ApiApplication.EquipmentEmail);
        var itemId = await NewMedicineAsync(equipment, quantity: 0, threshold: 10);

        await SweepAsync(equipment);
        var warning = Assert.Single(await WarningsAboutAsync(equipment, itemId, "low_stock"));

        Assert.Equal("critical", warning.GetProperty("severity").GetString());
        Assert.Contains("out of stock", warning.GetProperty("recommended_action").GetString());
    }

    [Fact]
    public async Task A_batch_expiring_this_week_is_high_and_an_expired_one_is_critical()
    {
        using var equipment = await ClientAsync(ApiApplication.EquipmentEmail);
        var soon = await NewMedicineAsync(equipment, quantity: 40, threshold: 5, expiresInDays: 5);
        var expired = await NewMedicineAsync(equipment, quantity: 40, threshold: 5, expiresInDays: -2);
        var later = await NewMedicineAsync(equipment, quantity: 40, threshold: 5, expiresInDays: 200);

        await SweepAsync(equipment);
        var soonWarning = Assert.Single(await WarningsAboutAsync(equipment, soon, "medicine_expiring"));
        var expiredWarning = Assert.Single(await WarningsAboutAsync(equipment, expired, "medicine_expiring"));

        Assert.Equal("high", soonWarning.GetProperty("severity").GetString());
        Assert.Contains("batch 1, 40 box, expires in 5 days",
            soonWarning.GetProperty("recommended_action").GetString());
        Assert.Equal("critical", expiredWarning.GetProperty("severity").GetString());
        Assert.Contains("expired 2 days ago", expiredWarning.GetProperty("recommended_action").GetString());
        Assert.Empty(await WarningsAboutAsync(equipment, later, "medicine_expiring"));
    }

    [Fact]
    public async Task A_machine_past_its_service_date_is_overdue_until_a_service_is_booked()
    {
        using var equipment = await ClientAsync(ApiApplication.EquipmentEmail);
        var itemId = await NewConfirmedItemAsync(equipment);
        var edited = await equipment.PutAsJsonAsync($"/api/equipment-items/{itemId}", new
        {
            next_maintenance_due = DateTime.UtcNow.AddDays(-45).ToString("yyyy-MM-dd")
        });
        edited.EnsureSuccessStatusCode();

        await SweepAsync(equipment);
        var warning = Assert.Single(await WarningsAboutAsync(equipment, itemId, "maintenance_overdue"));

        Assert.Equal("high", warning.GetProperty("severity").GetString());
        Assert.Contains("nothing is booked", warning.GetProperty("recommended_action").GetString());

        using var administrator = await AdministratorAsync();
        var booked = await administrator.PostAsJsonAsync("/api/maintenance-schedules", new
        {
            asset_type = "equipment_item",
            asset_id = itemId,
            schedule_type = "routine_service",
            scheduled_date = DateTime.UtcNow.AddDays(2).ToString("yyyy-MM-dd")
        });
        booked.EnsureSuccessStatusCode();
        await SweepAsync(equipment);

        var after = Assert.Single(await WarningsAboutAsync(equipment, itemId, "maintenance_overdue"));
        Assert.Equal("action_taken", after.GetProperty("status").GetString());
    }

    [Fact]
    public async Task Acknowledging_records_who_and_a_closed_warning_cannot_be_acknowledged()
    {
        using var equipment = await ClientAsync(ApiApplication.EquipmentEmail);
        var itemId = await NewMedicineAsync(equipment, quantity: 2, threshold: 10);
        await SweepAsync(equipment);
        var warningId = Assert.Single(await WarningsAboutAsync(equipment, itemId, "low_stock"))
            .GetProperty("id").GetGuid();

        var acknowledged = await equipment.PostAsync($"/api/warnings/{warningId}/acknowledge", null);
        using var body = await ReadJsonAsync(acknowledged);
        var again = await equipment.PostAsync($"/api/warnings/{warningId}/acknowledge", null);

        Assert.Equal(HttpStatusCode.OK, acknowledged.StatusCode);
        Assert.Equal("acknowledged", body.RootElement.GetProperty("status").GetString());
        Assert.NotEqual(JsonValueKind.Null, body.RootElement.GetProperty("acknowledged_by_staff_id").ValueKind);
        Assert.Equal(HttpStatusCode.OK, again.StatusCode);

        await equipment.PostAsJsonAsync($"/api/pharmacy-items/{itemId}/batches", new { quantity = 50 });
        await SweepAsync(equipment);
        var closed = await equipment.PostAsync($"/api/warnings/{warningId}/acknowledge", null);
        using var closedBody = await ReadJsonAsync(closed);

        Assert.Equal(HttpStatusCode.Conflict, closed.StatusCode);
        Assert.Equal("cl_equ_028", closedBody.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Done_takes_a_resolved_warning_off_the_list()
    {
        using var equipment = await ClientAsync(ApiApplication.EquipmentEmail);
        var itemId = await NewMedicineAsync(equipment, quantity: 2, threshold: 10);
        await SweepAsync(equipment);
        var warningId = Assert.Single(await WarningsAboutAsync(equipment, itemId, "low_stock"))
            .GetProperty("id").GetGuid();

        var whileOpen = await equipment.PostAsync($"/api/warnings/{warningId}/clear", null);
        using var whileOpenBody = await ReadJsonAsync(whileOpen);

        await equipment.PostAsJsonAsync($"/api/pharmacy-items/{itemId}/batches", new { quantity = 50 });
        await SweepAsync(equipment);

        var cleared = await equipment.PostAsync($"/api/warnings/{warningId}/clear", null);
        var again = await equipment.PostAsync($"/api/warnings/{warningId}/clear", null);

        Assert.Equal(HttpStatusCode.Conflict, whileOpen.StatusCode);
        Assert.Equal("cl_equ_029", whileOpenBody.RootElement.GetProperty("code").GetString());
        Assert.Equal(HttpStatusCode.NoContent, cleared.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, again.StatusCode);
        Assert.Empty(await WarningsAboutAsync(equipment, itemId, "low_stock"));
    }

    [Fact]
    public async Task Done_is_open_to_the_administrator_too_and_closed_to_everyone_else()
    {
        using var equipment = await ClientAsync(ApiApplication.EquipmentEmail);
        var itemId = await NewMedicineAsync(equipment, quantity: 2, threshold: 10);
        await SweepAsync(equipment);
        var warningId = Assert.Single(await WarningsAboutAsync(equipment, itemId, "low_stock"))
            .GetProperty("id").GetGuid();
        await equipment.PostAsJsonAsync($"/api/pharmacy-items/{itemId}/batches", new { quantity = 50 });
        await SweepAsync(equipment);

        using var nurse = await ClientAsync(ApiApplication.NurseEmail);
        var byNurse = await nurse.PostAsync($"/api/warnings/{warningId}/clear", null);
        using var administrator = await AdministratorAsync();
        var byAdministrator = await administrator.PostAsync($"/api/warnings/{warningId}/clear", null);

        Assert.Equal(HttpStatusCode.Forbidden, byNurse.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, byAdministrator.StatusCode);
    }

    [Fact]
    public async Task Only_the_equipment_manager_and_the_administrator_see_warnings()
    {
        using var nurse = await ClientAsync(ApiApplication.NurseEmail);
        using var administrator = await ClientAsync(ApiApplication.AdministratorEmail);

        var nurseLists = await nurse.GetAsync("/api/warnings");
        var nurseSweeps = await nurse.PostAsync("/api/warnings/sweep", null);
        var administratorSweeps = await administrator.PostAsync("/api/warnings/sweep", null);

        Assert.Equal(HttpStatusCode.Forbidden, nurseLists.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, nurseSweeps.StatusCode);
        Assert.Equal(HttpStatusCode.OK, administratorSweeps.StatusCode);
    }

    [Fact]
    public async Task A_reported_fault_is_left_alone_by_the_sweep()
    {
        using var equipment = await ClientAsync(ApiApplication.EquipmentEmail);
        var itemId = await NewConfirmedItemAsync(equipment);
        await equipment.PostAsJsonAsync(
            $"/api/equipment-items/{itemId}/report-fault", new { description = "No power." });

        await SweepAsync(equipment);
        var warnings = await WarningsAboutAsync(equipment, itemId, type: null);

        var fault = Assert.Single(warnings);
        Assert.Equal("equipment_faulty", fault.GetProperty("type").GetString());
        Assert.Equal("open", fault.GetProperty("status").GetString());
    }

    private static async Task<JsonDocument> SweepAsync(HttpClient client)
    {
        var response = await client.PostAsync("/api/warnings/sweep", null);
        response.EnsureSuccessStatusCode();
        return await ReadJsonAsync(response);
    }

    private static async Task<IReadOnlyList<JsonElement>> WarningsAboutAsync(
        HttpClient client, Guid relatedId, string? type)
    {
        var found = new List<JsonElement>();

        for (var page = 1; ; page++)
        {
            var query = type is null ? string.Empty : $"&type={type}";
            using var body = await ReadJsonAsync(
                await client.GetAsync($"/api/warnings?page={page}&pageSize=100{query}"));

            found.AddRange(body.RootElement.GetProperty("items").EnumerateArray()
                .Where(w => w.GetProperty("related_entity_id").GetGuid() == relatedId)
                .Select(w => w.Clone()));

            if (page >= body.RootElement.GetProperty("total_pages").GetInt32())
            {
                return found;
            }
        }
    }

    private static async Task<Guid> NewMedicineAsync(
        HttpClient client, int quantity, int threshold, int? expiresInDays = null)
    {
        using var category = await ReadJsonAsync(await client.PostAsJsonAsync(
            "/api/pharmacy-categories",
            new { name = $"Category {Guid.NewGuid():N}"[..20], requires_prescription = false }));

        using var item = await ReadJsonAsync(await client.PostAsJsonAsync("/api/pharmacy-items", new
        {
            name = $"Sweep {Guid.NewGuid():N}"[..20],
            category_id = category.RootElement.GetProperty("id").GetGuid(),
            unit = "box",
            quantity_on_hand = quantity,
            reorder_threshold = threshold,
            expiry_date = expiresInDays is { } days
                ? DateTime.UtcNow.AddHours(5.5).AddDays(days).ToString("yyyy-MM-dd")
                : null
        }));

        return item.RootElement.GetProperty("id").GetGuid();
    }

    private async Task<Guid> NewConfirmedItemAsync(HttpClient equipment)
    {
        using var category = await ReadJsonAsync(await equipment.PostAsJsonAsync(
            "/api/equipment-categories", new { name = $"Category {Guid.NewGuid():N}"[..20] }));

        using var item = await ReadJsonAsync(await equipment.PostAsJsonAsync("/api/equipment-items", new
        {
            name = "Infusion pump",
            category_id = category.RootElement.GetProperty("id").GetGuid(),
            model = "P-7",
            manufacturer = "Acme Medical",
            purchase_date = "2026-01-01",
            asset_tag = $"WS-{Guid.NewGuid():N}"[..14]
        }));
        var id = item.RootElement.GetProperty("id").GetGuid();

        using var administrator = await AdministratorAsync();
        var confirmed = await administrator.PostAsync($"/api/equipment-items/{id}/confirm", null);
        confirmed.EnsureSuccessStatusCode();

        return id;
    }

    private async Task<HttpClient> AdministratorAsync()
    {
        var client = await ClientAsync(ApiApplication.AdministratorEmail);
        client.DefaultRequestHeaders.Add(CodeHeader, ApiApplication.EquipmentConfirmationCode);
        return client;
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
