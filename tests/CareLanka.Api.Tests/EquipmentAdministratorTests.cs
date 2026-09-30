using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace CareLanka.Api.Tests;

// The equipment administrator: confirms newly-registered equipment and confirms maintenance
// done, without needing the shared confirmation code the hospital administrator still needs
// (see EquipmentItemService/MaintenanceService.EnsureConfirmationCode) - and nothing else, so
// booking maintenance work stays refused (Policies.MaintenanceDesk was not extended).
[Collection(ApiCollection.Name)]
public sealed class EquipmentAdministratorTests
{
    private readonly ApiApplication _application;

    public EquipmentAdministratorTests(ApiApplication application) => _application = application;

    [Fact]
    public async Task An_equipment_administrator_confirms_a_new_item_with_no_confirmation_code_header()
    {
        using var equipment = await ClientAsync(ApiApplication.EquipmentEmail);
        var id = await NewAwaitingItemIdAsync(equipment);

        using var admin = await ClientAsync(ApiApplication.EquipmentAdministratorEmail);

        // Deliberately no X-Confirmation-Code header at all.
        var response = await admin.PostAsync($"/api/equipment-items/{id}/confirm", content: null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task An_equipment_administrator_rejects_a_new_item_with_no_confirmation_code_header()
    {
        using var equipment = await ClientAsync(ApiApplication.EquipmentEmail);
        var id = await NewAwaitingItemIdAsync(equipment);

        using var admin = await ClientAsync(ApiApplication.EquipmentAdministratorEmail);

        var response = await admin.PostAsync($"/api/equipment-items/{id}/reject", content: null);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task An_equipment_administrator_confirms_maintenance_done_with_no_confirmation_code_header()
    {
        using var equipment = await ClientAsync(ApiApplication.EquipmentEmail);
        var itemId = await NewAwaitingItemIdAsync(equipment);

        // Confirming the item first, as the hospital administrator (with the code, unchanged),
        // so it is in service and schedulable.
        using var hospitalAdmin = await ClientAsync(ApiApplication.AdministratorEmail);
        var confirmRequest = new HttpRequestMessage(HttpMethod.Post, $"/api/equipment-items/{itemId}/confirm");
        confirmRequest.Headers.Add("X-Confirmation-Code", ApiApplication.EquipmentConfirmationCode);
        await hospitalAdmin.SendAsync(confirmRequest);

        // Booking maintenance work is still the hospital administrator's alone (unchanged).
        using var schedule = await ReadJsonAsync(await hospitalAdmin.PostAsJsonAsync("/api/maintenance-schedules", new
        {
            asset_type = "equipment_item",
            asset_id = itemId,
            schedule_type = "routine_service",
            scheduled_date = "2026-12-01",
        }));
        var scheduleId = schedule.RootElement.GetProperty("id").GetGuid();

        using var equipAdmin = await ClientAsync(ApiApplication.EquipmentAdministratorEmail);

        var response = await equipAdmin.PostAsync($"/api/maintenance-schedules/{scheduleId}/confirm", content: null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task An_equipment_administrator_cannot_book_maintenance_work()
    {
        // Policies.MaintenanceDesk was deliberately not extended - booking work, and the general
        // open-jobs list, stay the hospital administrator's alone.
        using var equipAdmin = await ClientAsync(ApiApplication.EquipmentAdministratorEmail);

        var response = await equipAdmin.PostAsJsonAsync("/api/maintenance-schedules", new { });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private async Task<Guid> NewAwaitingItemIdAsync(HttpClient equipmentClient)
    {
        using var category = await ReadJsonAsync(await equipmentClient.PostAsJsonAsync(
            "/api/equipment-categories", new { name = $"Category {Guid.NewGuid():N}"[..20] }));

        using var body = await ReadJsonAsync(await equipmentClient.PostAsJsonAsync("/api/equipment-items", new
        {
            name = "Ventilator",
            category_id = category.RootElement.GetProperty("id").GetGuid(),
            model = "V-100",
            manufacturer = "Acme Medical",
            purchase_date = "2026-01-05",
            asset_tag = $"EQ-{Guid.NewGuid():N}"[..14],
            ward_id = Guid.NewGuid(),
        }));

        return body.RootElement.GetProperty("id").GetGuid();
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

    private static async Task<JsonDocument> ReadJsonAsync(HttpResponseMessage response) =>
        JsonDocument.Parse(await response.Content.ReadAsStringAsync());
}
