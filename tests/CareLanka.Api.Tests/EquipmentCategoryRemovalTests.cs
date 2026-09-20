using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace CareLanka.Api.Tests;

[Collection(ApiCollection.Name)]
public sealed class EquipmentCategoryRemovalTests
{
    private const string CodeHeader = "X-Confirmation-Code";

    private readonly ApiApplication _application;

    public EquipmentCategoryRemovalTests(ApiApplication application) => _application = application;

    [Fact]
    public async Task An_unused_category_is_removed_and_its_name_is_free_again()
    {
        using var equipment = await ClientAsync(ApiApplication.EquipmentEmail);
        var name = $"Unwanted {Guid.NewGuid():N}"[..20];
        var categoryId = await NewCategoryAsync(equipment, name);

        using var administrator = await AdministratorAsync();
        using var before = await ReadJsonAsync(
            await administrator.GetAsync("/api/equipment-categories/for-removal"));
        var listed = Assert.Single(
            before.RootElement.EnumerateArray(), c => c.GetProperty("id").GetGuid() == categoryId);

        var removed = await administrator.DeleteAsync($"/api/equipment-categories/{categoryId}");
        using var picker = await ReadJsonAsync(await equipment.GetAsync("/api/equipment-categories"));
        var again = await equipment.PostAsJsonAsync("/api/equipment-categories", new { name });

        Assert.Equal(0, listed.GetProperty("item_count").GetInt32());
        Assert.Equal(HttpStatusCode.NoContent, removed.StatusCode);
        Assert.DoesNotContain(picker.RootElement.EnumerateArray(), c => c.GetProperty("id").GetGuid() == categoryId);
        Assert.Equal(HttpStatusCode.Created, again.StatusCode);
    }

    [Fact]
    public async Task A_category_with_items_is_refused_and_counts_them()
    {
        using var equipment = await ClientAsync(ApiApplication.EquipmentEmail);
        var categoryId = await NewCategoryAsync(equipment, $"In use {Guid.NewGuid():N}"[..20]);
        var created = await equipment.PostAsJsonAsync("/api/equipment-items", new
        {
            name = "Suction unit",
            category_id = categoryId,
            model = "S-3",
            manufacturer = "Acme Medical",
            purchase_date = "2026-07-01",
            asset_tag = $"CR-{Guid.NewGuid():N}"[..14]
        });
        created.EnsureSuccessStatusCode();

        using var administrator = await AdministratorAsync();
        using var list = await ReadJsonAsync(
            await administrator.GetAsync("/api/equipment-categories/for-removal"));
        var removed = await administrator.DeleteAsync($"/api/equipment-categories/{categoryId}");
        using var body = await ReadJsonAsync(removed);

        var listed = Assert.Single(
            list.RootElement.EnumerateArray(), c => c.GetProperty("id").GetGuid() == categoryId);
        Assert.Equal(1, listed.GetProperty("item_count").GetInt32());
        Assert.Equal(HttpStatusCode.Conflict, removed.StatusCode);
        Assert.Equal("cl_equ_030", body.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Removing_needs_the_code_and_the_administrator()
    {
        using var equipment = await ClientAsync(ApiApplication.EquipmentEmail);
        var categoryId = await NewCategoryAsync(equipment, $"Guarded {Guid.NewGuid():N}"[..20]);

        using var noCode = await ClientAsync(ApiApplication.AdministratorEmail);
        var listWithoutCode = await noCode.GetAsync("/api/equipment-categories/for-removal");
        noCode.DefaultRequestHeaders.Add(CodeHeader, "not-the-code");
        var wrongCode = await noCode.DeleteAsync($"/api/equipment-categories/{categoryId}");

        equipment.DefaultRequestHeaders.Add(CodeHeader, ApiApplication.EquipmentConfirmationCode);
        var byEquipmentManager = await equipment.DeleteAsync($"/api/equipment-categories/{categoryId}");

        Assert.Equal(HttpStatusCode.Forbidden, listWithoutCode.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, wrongCode.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, byEquipmentManager.StatusCode);
    }

    private static async Task<Guid> NewCategoryAsync(HttpClient client, string name)
    {
        using var body = await ReadJsonAsync(
            await client.PostAsJsonAsync("/api/equipment-categories", new { name }));

        return body.RootElement.GetProperty("id").GetGuid();
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
    {
        var text = await response.Content.ReadAsStringAsync();
        return JsonDocument.Parse(text.Length == 0 ? "{}" : text);
    }
}
