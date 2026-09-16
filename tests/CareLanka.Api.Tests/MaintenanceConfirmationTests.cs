using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace CareLanka.Api.Tests;

[Collection(ApiCollection.Name)]
public sealed class MaintenanceConfirmationTests
{
    private const string CodeHeader = "X-Confirmation-Code";

    private readonly ApiApplication _application;

    public MaintenanceConfirmationTests(ApiApplication application) => _application = application;

    [Fact]
    public async Task A_reported_fault_is_on_the_administrators_list_straight_away()
    {
        using var equipment = await ClientAsync(ApiApplication.EquipmentEmail);
        var itemId = await NewConfirmedItemAsync(equipment);
        await equipment.PostAsJsonAsync(
            $"/api/equipment-items/{itemId}/report-fault", new { description = "No power." });

        using var administrator = await AdministratorAsync();
        using var body = await ReadJsonAsync(
            await administrator.GetAsync("/api/maintenance-schedules/pending-confirmation"));

        var job = Assert.Single(
            body.RootElement.EnumerateArray(),
            job => job.GetProperty("asset_id").GetGuid() == itemId);
        Assert.Equal("repair", job.GetProperty("schedule_type").GetString());
        Assert.Equal("No power.", job.GetProperty("notes").GetString());
    }

    [Fact]
    public async Task Confirming_done_returns_the_item_to_service_and_closes_the_fault()
    {
        using var equipment = await ClientAsync(ApiApplication.EquipmentEmail);
        var itemId = await NewConfirmedItemAsync(equipment);
        await equipment.PostAsJsonAsync(
            $"/api/equipment-items/{itemId}/report-fault", new { description = "No power." });
        var jobId = await OpenJobIdAsync(equipment, itemId);

        using var administrator = await AdministratorAsync();
        var confirmed = await administrator.PostAsync($"/api/maintenance-schedules/{jobId}/confirm", null);
        using var job = await ReadJsonAsync(confirmed);
        using var item = await ReadJsonAsync(await equipment.GetAsync($"/api/equipment-items/{itemId}"));

        Assert.Equal(HttpStatusCode.OK, confirmed.StatusCode);
        Assert.Equal("completed", job.RootElement.GetProperty("status").GetString());
        Assert.NotEqual(JsonValueKind.Null, job.RootElement.GetProperty("completed_at").ValueKind);
        Assert.Equal("available", item.RootElement.GetProperty("status").GetString());
        Assert.Empty(item.RootElement.GetProperty("open_warnings").EnumerateArray());
    }

    [Fact]
    public async Task The_code_is_needed_and_the_equipment_manager_cannot_confirm()
    {
        using var equipment = await ClientAsync(ApiApplication.EquipmentEmail);
        var jobId = await ScheduleAsync(equipment, await NewConfirmedItemAsync(equipment));

        using var administrator = await ClientAsync(ApiApplication.AdministratorEmail);
        var withoutCode = await administrator.GetAsync("/api/maintenance-schedules/pending-confirmation");

        administrator.DefaultRequestHeaders.Add(CodeHeader, "not-the-code");
        var wrongCode = await administrator.PostAsync($"/api/maintenance-schedules/{jobId}/confirm", null);
        using var wrongBody = await ReadJsonAsync(wrongCode);

        equipment.DefaultRequestHeaders.Add(CodeHeader, ApiApplication.EquipmentConfirmationCode);
        var equipmentManager = await equipment.PostAsync($"/api/maintenance-schedules/{jobId}/confirm", null);

        Assert.Equal(HttpStatusCode.Forbidden, withoutCode.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, wrongCode.StatusCode);
        Assert.Equal("cl_equ_017", wrongBody.RootElement.GetProperty("code").GetString());
        Assert.Equal(HttpStatusCode.Forbidden, equipmentManager.StatusCode);
    }

    [Fact]
    public async Task There_is_no_longer_a_way_to_complete_a_job_from_the_equipment_side()
    {
        using var equipment = await ClientAsync(ApiApplication.EquipmentEmail);
        var jobId = await ScheduleAsync(equipment, await NewConfirmedItemAsync(equipment));

        var response = await equipment.PostAsJsonAsync(
            $"/api/maintenance-schedules/{jobId}/complete", new { notes = "Done." });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task A_finished_job_leaves_the_list_and_cannot_be_confirmed_again()
    {
        using var equipment = await ClientAsync(ApiApplication.EquipmentEmail);
        var jobId = await ScheduleAsync(equipment, await NewConfirmedItemAsync(equipment));

        using var administrator = await AdministratorAsync();
        await administrator.PostAsync($"/api/maintenance-schedules/{jobId}/confirm", null);
        var again = await administrator.PostAsync($"/api/maintenance-schedules/{jobId}/confirm", null);
        using var againBody = await ReadJsonAsync(again);
        using var list = await ReadJsonAsync(
            await administrator.GetAsync("/api/maintenance-schedules/pending-confirmation"));

        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
        Assert.Equal("cl_equ_012", againBody.RootElement.GetProperty("code").GetString());
        Assert.DoesNotContain(list.RootElement.EnumerateArray(), job => job.GetProperty("id").GetGuid() == jobId);
    }

    [Fact]
    public async Task The_open_count_follows_scheduling_and_confirming()
    {
        using var equipment = await ClientAsync(ApiApplication.EquipmentEmail);
        using var nurse = await ClientAsync(ApiApplication.NurseEmail);

        var before = await CountAsync(equipment);
        var jobId = await ScheduleAsync(equipment, await NewConfirmedItemAsync(equipment));
        var scheduled = await CountAsync(equipment);

        using var administrator = await AdministratorAsync();
        await administrator.PostAsync($"/api/maintenance-schedules/{jobId}/confirm", null);
        var confirmed = await CountAsync(administrator);

        var nurseAsks = await nurse.GetAsync("/api/maintenance-schedules/pending-confirmation/count");

        Assert.Equal(before + 1, scheduled);
        Assert.Equal(before, confirmed);
        Assert.Equal(HttpStatusCode.Forbidden, nurseAsks.StatusCode);
    }

    private static async Task<int> CountAsync(HttpClient client)
    {
        using var body = await ReadJsonAsync(
            await client.GetAsync("/api/maintenance-schedules/pending-confirmation/count"));

        return body.RootElement.GetProperty("count").GetInt32();
    }

    private static async Task<Guid> ScheduleAsync(HttpClient client, Guid itemId)
    {
        using var body = await ReadJsonAsync(await client.PostAsJsonAsync("/api/maintenance-schedules", new
        {
            asset_type = "equipment_item",
            asset_id = itemId,
            schedule_type = "routine_service",
            scheduled_date = DateTime.UtcNow.ToString("yyyy-MM-dd")
        }));

        return body.RootElement.GetProperty("id").GetGuid();
    }

    private static async Task<Guid> OpenJobIdAsync(HttpClient client, Guid itemId)
    {
        using var body = await ReadJsonAsync(await client.GetAsync($"/api/equipment-items/{itemId}"));

        return body.RootElement.GetProperty("maintenance_history").EnumerateArray()
            .First(job => job.GetProperty("status").GetString() is "scheduled" or "overdue")
            .GetProperty("id").GetGuid();
    }

    private async Task<Guid> NewConfirmedItemAsync(HttpClient equipment)
    {
        using var category = await ReadJsonAsync(await equipment.PostAsJsonAsync(
            "/api/equipment-categories", new { name = $"Category {Guid.NewGuid():N}"[..20] }));

        using var item = await ReadJsonAsync(await equipment.PostAsJsonAsync("/api/equipment-items", new
        {
            name = "Suction unit",
            category_id = category.RootElement.GetProperty("id").GetGuid(),
            model = "S-3",
            manufacturer = "Acme Medical",
            purchase_date = "2026-07-01",
            asset_tag = $"MC-{Guid.NewGuid():N}"[..14]
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
