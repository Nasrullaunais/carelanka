using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace CareLanka.Api.Tests;

[Collection(ApiCollection.Name)]
public sealed class EquipmentItemConfirmationTests
{
    private const string CodeHeader = "X-Confirmation-Code";

    private readonly ApiApplication _application;

    public EquipmentItemConfirmationTests(ApiApplication application) => _application = application;

    [Fact]
    public async Task A_newly_registered_item_waits_off_the_register_until_it_is_confirmed()
    {
        using var equipment = await ClientAsync(ApiApplication.EquipmentEmail);
        var (id, tag) = await RegisterAsync(equipment);

        Assert.Equal(0, await RegisterMatchesAsync(equipment, tag));

        using var administrator = await AdministratorAsync();
        var confirmed = await administrator.PostAsync($"/api/equipment-items/{id}/confirm", null);
        using var body = await ReadJsonAsync(confirmed);

        Assert.Equal(HttpStatusCode.OK, confirmed.StatusCode);
        Assert.False(body.RootElement.GetProperty("awaiting_confirmation").GetBoolean());
        Assert.NotEqual(JsonValueKind.Null, body.RootElement.GetProperty("confirmed_at").ValueKind);
        Assert.Equal(1, await RegisterMatchesAsync(equipment, tag));
    }

    [Fact]
    public async Task Registering_answers_with_the_item_marked_as_awaiting_confirmation()
    {
        using var equipment = await ClientAsync(ApiApplication.EquipmentEmail);

        using var body = await ReadJsonAsync(await PostItemAsync(equipment, NewTag()));

        Assert.True(body.RootElement.GetProperty("awaiting_confirmation").GetBoolean());
        Assert.Equal("available", body.RootElement.GetProperty("status").GetString());
    }

    [Fact]
    public async Task The_administrator_sees_the_waiting_item_only_with_the_right_code()
    {
        using var equipment = await ClientAsync(ApiApplication.EquipmentEmail);
        var (id, _) = await RegisterAsync(equipment);

        using var administrator = await ClientAsync(ApiApplication.AdministratorEmail);

        var withoutCode = await administrator.GetAsync("/api/equipment-items/pending-confirmation");

        administrator.DefaultRequestHeaders.Add(CodeHeader, "not-the-code");
        var wrongCode = await administrator.GetAsync("/api/equipment-items/pending-confirmation");
        using var wrongBody = await ReadJsonAsync(wrongCode);

        administrator.DefaultRequestHeaders.Remove(CodeHeader);
        administrator.DefaultRequestHeaders.Add(CodeHeader, ApiApplication.EquipmentConfirmationCode);
        var rightCode = await administrator.GetAsync("/api/equipment-items/pending-confirmation");
        using var rightBody = await ReadJsonAsync(rightCode);

        Assert.Equal(HttpStatusCode.Forbidden, withoutCode.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, wrongCode.StatusCode);
        Assert.Equal("cl_equ_017", wrongBody.RootElement.GetProperty("code").GetString());
        Assert.Equal(HttpStatusCode.OK, rightCode.StatusCode);
        Assert.Contains(
            rightBody.RootElement.EnumerateArray(),
            item => item.GetProperty("id").GetGuid() == id);
    }

    [Fact]
    public async Task The_equipment_manager_cannot_confirm_their_own_entry_even_with_the_code()
    {
        using var equipment = await ClientAsync(ApiApplication.EquipmentEmail);
        var (id, _) = await RegisterAsync(equipment);

        equipment.DefaultRequestHeaders.Add(CodeHeader, ApiApplication.EquipmentConfirmationCode);
        var response = await equipment.PostAsync($"/api/equipment-items/{id}/confirm", null);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task A_wrong_code_does_not_confirm_the_item()
    {
        using var equipment = await ClientAsync(ApiApplication.EquipmentEmail);
        var (id, tag) = await RegisterAsync(equipment);

        using var administrator = await ClientAsync(ApiApplication.AdministratorEmail);
        administrator.DefaultRequestHeaders.Add(CodeHeader, "equipment");
        var response = await administrator.PostAsync($"/api/equipment-items/{id}/confirm", null);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(0, await RegisterMatchesAsync(equipment, tag));
    }

    [Fact]
    public async Task Confirming_the_same_item_twice_is_refused()
    {
        using var equipment = await ClientAsync(ApiApplication.EquipmentEmail);
        var (id, _) = await RegisterAsync(equipment);

        using var administrator = await AdministratorAsync();
        await administrator.PostAsync($"/api/equipment-items/{id}/confirm", null);
        var again = await administrator.PostAsync($"/api/equipment-items/{id}/confirm", null);
        using var body = await ReadJsonAsync(again);

        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
        Assert.Equal("cl_equ_019", body.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task A_rejected_item_is_gone_and_its_asset_tag_can_be_registered_again()
    {
        using var equipment = await ClientAsync(ApiApplication.EquipmentEmail);
        var (id, tag) = await RegisterAsync(equipment);

        using var administrator = await AdministratorAsync();
        var rejected = await administrator.PostAsync($"/api/equipment-items/{id}/reject", null);
        var afterwards = await equipment.GetAsync($"/api/equipment-items/{id}");
        var registeredAgain = await PostItemAsync(equipment, tag);

        Assert.Equal(HttpStatusCode.NoContent, rejected.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, afterwards.StatusCode);
        Assert.Equal(HttpStatusCode.Created, registeredAgain.StatusCode);
    }

    [Fact]
    public async Task An_item_waiting_for_confirmation_cannot_be_put_to_use()
    {
        using var equipment = await ClientAsync(ApiApplication.EquipmentEmail);
        var (id, _) = await RegisterAsync(equipment);

        var edited = await equipment.PutAsJsonAsync(
            $"/api/equipment-items/{id}", new { name = "Renamed before anyone confirmed it" });
        var assigned = await equipment.PostAsJsonAsync(
            $"/api/equipment-items/{id}/assign", new { admission_id = Guid.NewGuid() });
        var faulted = await equipment.PostAsJsonAsync(
            $"/api/equipment-items/{id}/report-fault", new { description = "Will not start." });
        using var administrator = await ClientAsync(ApiApplication.AdministratorEmail);
        var serviced = await administrator.PostAsJsonAsync("/api/maintenance-schedules", new
        {
            asset_type = "equipment_item",
            asset_id = id,
            schedule_type = "routine_service",
            scheduled_date = DateTime.UtcNow.ToString("yyyy-MM-dd")
        });
        using var body = await ReadJsonAsync(assigned);

        Assert.Equal(HttpStatusCode.Conflict, edited.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, assigned.StatusCode);
        Assert.Equal("cl_equ_018", body.RootElement.GetProperty("code").GetString());
        Assert.Equal(HttpStatusCode.Conflict, faulted.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, serviced.StatusCode);
    }

    [Fact]
    public async Task The_waiting_count_moves_as_items_are_registered_and_confirmed()
    {
        using var equipment = await ClientAsync(ApiApplication.EquipmentEmail);
        using var nurse = await ClientAsync(ApiApplication.NurseEmail);

        var before = await CountAsync(equipment);
        var (id, _) = await RegisterAsync(equipment);
        var registered = await CountAsync(equipment);

        using var administrator = await AdministratorAsync();
        await administrator.PostAsync($"/api/equipment-items/{id}/confirm", null);
        var confirmed = await CountAsync(administrator);

        var nurseAsks = await nurse.GetAsync("/api/equipment-items/pending-confirmation/count");

        Assert.Equal(before + 1, registered);
        Assert.Equal(before, confirmed);
        Assert.Equal(HttpStatusCode.Forbidden, nurseAsks.StatusCode);
    }

    private static async Task<int> CountAsync(HttpClient client)
    {
        using var body = await ReadJsonAsync(
            await client.GetAsync("/api/equipment-items/pending-confirmation/count"));

        return body.RootElement.GetProperty("count").GetInt32();
    }

    private static async Task<int> RegisterMatchesAsync(HttpClient client, string tag)
    {
        using var body = await ReadJsonAsync(await client.GetAsync($"/api/equipment-items?search={tag}"));

        return body.RootElement.GetProperty("total_items").GetInt32();
    }

    private static async Task<(Guid Id, string Tag)> RegisterAsync(HttpClient client)
    {
        var tag = NewTag();
        using var body = await ReadJsonAsync(await PostItemAsync(client, tag));

        return (body.RootElement.GetProperty("id").GetGuid(), tag);
    }

    private static async Task<HttpResponseMessage> PostItemAsync(HttpClient client, string tag)
    {
        using var category = await ReadJsonAsync(await client.PostAsJsonAsync(
            "/api/equipment-categories", new { name = $"Category {Guid.NewGuid():N}"[..20] }));

        return await client.PostAsJsonAsync("/api/equipment-items", new
        {
            name = "Infusion pump",
            category_id = category.RootElement.GetProperty("id").GetGuid(),
            model = "P-20",
            manufacturer = "Acme Medical",
            purchase_date = "2026-08-01",
            asset_tag = tag
        });
    }

    private static string NewTag() => $"CF-{Guid.NewGuid():N}"[..14];

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
