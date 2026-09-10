using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace CareLanka.Api.Tests;

/// <summary>
/// The pharmacy catalog and the one rule that matters in it: stock moves only through a
/// transaction, and it can never go below zero however many people push at once.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class PharmacyEndpointTests
{
    private readonly ApiApplication _application;

    public PharmacyEndpointTests(ApiApplication application) => _application = application;

    [Fact]
    public async Task A_new_item_reports_its_own_availability_rather_than_storing_it()
    {
        using var client = await EquipmentClientAsync();

        var response = await CreateItemAsync(client, quantity: 4, threshold: 10);
        using var body = await ReadJsonAsync(response);
        var item = body.RootElement;

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(4, item.GetProperty("quantity_on_hand").GetInt32());

        // Both computed from the quantity at read time. A stored flag is a second source of
        // truth, and the one that goes stale.
        Assert.True(item.GetProperty("is_available").GetBoolean());
        Assert.True(item.GetProperty("below_threshold").GetBoolean());
    }

    [Fact]
    public async Task An_item_with_nothing_on_the_shelf_reads_as_unavailable()
    {
        using var client = await EquipmentClientAsync();

        using var body = await ReadJsonAsync(await CreateItemAsync(client, quantity: 0));

        Assert.False(body.RootElement.GetProperty("is_available").GetBoolean());
    }

    [Fact]
    public async Task Dispensing_takes_stock_off_the_shelf()
    {
        using var client = await EquipmentClientAsync();
        var id = await NewItemIdAsync(client, quantity: 10);

        var response = await MoveAsync(client, id, "dispensed", 3);
        using var body = await ReadJsonAsync(response);

        // The endpoint answers with the item, not the transaction: what the caller needs to
        // see is the shelf after the movement.
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(7, body.RootElement.GetProperty("quantity_on_hand").GetInt32());
    }

    [Fact]
    public async Task Receiving_a_delivery_puts_stock_back()
    {
        using var client = await EquipmentClientAsync();
        var id = await NewItemIdAsync(client, quantity: 2);

        using var body = await ReadJsonAsync(await MoveAsync(client, id, "received", 12));

        Assert.Equal(14, body.RootElement.GetProperty("quantity_on_hand").GetInt32());
    }

    [Fact]
    public async Task Dispensing_more_than_is_there_is_refused_and_changes_nothing()
    {
        using var client = await EquipmentClientAsync();
        var id = await NewItemIdAsync(client, quantity: 5);

        var response = await MoveAsync(client, id, "dispensed", 6);
        using var body = await ReadJsonAsync(response);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("cl_equ_010", body.RootElement.GetProperty("code").GetString());

        // The refusal is the WHERE clause on the update, so nothing was written. If this
        // ever reads 5 while the transaction list has grown, the two came apart.
        Assert.Equal(5, await QuantityAsync(client, id));
        using var history = await ReadJsonAsync(await client.GetAsync($"/api/pharmacy-items/{id}/transactions"));
        Assert.Equal(0, history.RootElement.GetProperty("total_items").GetInt32());
    }

    [Fact]
    public async Task Two_people_dispensing_the_last_box_at_once_do_not_both_succeed()
    {
        using var first = await EquipmentClientAsync();
        using var second = await EquipmentClientAsync();
        var id = await NewItemIdAsync(first, quantity: 1);

        // The whole reason the quantity is changed by one conditional UPDATE rather than a
        // read followed by a write. Read-then-write lets both callers see 1, both write 0,
        // and two boxes leave a shelf that held one.
        var both = await Task.WhenAll(
            MoveAsync(first, id, "dispensed", 1),
            MoveAsync(second, id, "dispensed", 1));

        Assert.Equal(1, both.Count(r => r.StatusCode == HttpStatusCode.Created));
        Assert.Equal(1, both.Count(r => r.StatusCode == HttpStatusCode.Conflict));
        Assert.Equal(0, await QuantityAsync(first, id));

        foreach (var response in both)
        {
            response.Dispose();
        }
    }

    [Fact]
    public async Task An_adjustment_without_a_note_is_refused()
    {
        using var client = await EquipmentClientAsync();
        var id = await NewItemIdAsync(client, quantity: 5);

        var response = await MoveAsync(client, id, "adjusted", 2);

        // A stocktake correction nobody explained cannot be audited afterwards, and this is
        // the one movement with no delivery or prescription behind it.
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task An_adjustment_with_a_note_goes_through()
    {
        using var client = await EquipmentClientAsync();
        var id = await NewItemIdAsync(client, quantity: 5);

        var response = await MoveAsync(client, id, "adjusted", 2, "Stocktake found two extra boxes.");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task The_history_records_who_moved_the_stock_and_is_newest_first()
    {
        using var client = await EquipmentClientAsync();
        var id = await NewItemIdAsync(client, quantity: 20);

        await MoveAsync(client, id, "dispensed", 1);
        await MoveAsync(client, id, "dispensed", 2);

        using var body = await ReadJsonAsync(
            await client.GetAsync($"/api/pharmacy-items/{id}/transactions"));
        var rows = body.RootElement.GetProperty("items").EnumerateArray().ToList();

        Assert.Equal(2, rows.Count);

        // Newest first, and quantity is always positive - the type is what gives it a sign.
        Assert.Equal(2, rows[0].GetProperty("quantity").GetInt32());
        Assert.Equal("dispensed", rows[0].GetProperty("type").GetString());

        // Taken from the token, never the body, so nobody can record a movement under
        // somebody else's name.
        Assert.NotEqual(Guid.Empty, rows[0].GetProperty("performed_by_staff_id").GetGuid());
    }

    [Fact]
    public async Task Search_matches_the_name_without_caring_about_capitals()
    {
        using var client = await EquipmentClientAsync();
        var name = $"Paracetamol {Guid.NewGuid():N}"[..24];
        await CreateItemAsync(client, quantity: 5, name: name);

        using var body = await ReadJsonAsync(
            await client.GetAsync($"/api/pharmacy-items?search={Uri.EscapeDataString(name.ToLower())}"));

        Assert.Equal(1, body.RootElement.GetProperty("total_items").GetInt32());
    }

    [Fact]
    public async Task Available_only_leaves_out_what_is_off_the_shelf()
    {
        using var client = await EquipmentClientAsync();
        var id = await NewItemIdAsync(client, quantity: 1);

        await MoveAsync(client, id, "dispensed", 1);

        using var body = await ReadJsonAsync(
            await client.GetAsync("/api/pharmacy-items?availableOnly=true&pageSize=100"));
        var ids = body.RootElement.GetProperty("items").EnumerateArray()
            .Select(i => i.GetProperty("id").GetGuid());

        Assert.DoesNotContain(id, ids);
    }

    [Fact]
    public async Task Two_categories_with_the_same_name_are_a_conflict()
    {
        using var client = await EquipmentClientAsync();
        var name = $"Chronic {Guid.NewGuid():N}"[..18];

        await CreateCategoryAsync(client, name);
        var second = await CreateCategoryAsync(client, name.ToUpper());
        using var body = await ReadJsonAsync(second);

        // Compared without case: two rows that look identical on a dispensing screen are
        // worse than a 409.
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        Assert.Equal("cl_equ_008", body.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Any_nurse_may_search_the_pharmacy_but_not_change_what_is_in_it()
    {
        using var nurse = await ClientAsync(ApiApplication.NurseEmail);
        using var equipment = await EquipmentClientAsync();
        var id = await NewItemIdAsync(equipment, quantity: 5);

        var search = await nurse.GetAsync("/api/pharmacy-items?availableOnly=true");
        var move = await MoveAsync(nurse, id, "dispensed", 1);

        // "Do we have this medicine" is a question anyone in the hospital may ask, which is
        // the literal requirement in the component plan. Moving stock is not.
        Assert.Equal(HttpStatusCode.OK, search.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, move.StatusCode);
    }

    [Fact]
    public async Task A_movement_against_an_unknown_item_is_a_404()
    {
        using var client = await EquipmentClientAsync();

        var response = await MoveAsync(client, Guid.NewGuid(), "dispensed", 1);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private static Task<HttpResponseMessage> MoveAsync(
        HttpClient client, Guid id, string type, int quantity, string? note = null)
        => client.PostAsJsonAsync(
            $"/api/pharmacy-items/{id}/transactions", new { type, quantity, note });

    private static async Task<int> QuantityAsync(HttpClient client, Guid id)
    {
        using var body = await ReadJsonAsync(await client.GetAsync($"/api/pharmacy-items/{id}"));
        return body.RootElement.GetProperty("quantity_on_hand").GetInt32();
    }

    private static Task<HttpResponseMessage> CreateCategoryAsync(HttpClient client, string name)
        => client.PostAsJsonAsync(
            "/api/pharmacy-categories", new { name, requires_prescription = false });

    private static async Task<HttpResponseMessage> CreateItemAsync(
        HttpClient client, int quantity, int threshold = 10, string? name = null)
    {
        using var category = await ReadJsonAsync(
            await CreateCategoryAsync(client, $"Category {Guid.NewGuid():N}"[..20]));

        return await client.PostAsJsonAsync("/api/pharmacy-items", new
        {
            name = name ?? $"Item {Guid.NewGuid():N}"[..20],
            category_id = category.RootElement.GetProperty("id").GetGuid(),
            manufacturer = "Acme Pharma",
            unit = "box",
            quantity_on_hand = quantity,
            reorder_threshold = threshold,
            unit_price = 12.50m
        });
    }

    private static async Task<Guid> NewItemIdAsync(HttpClient client, int quantity)
    {
        using var body = await ReadJsonAsync(await CreateItemAsync(client, quantity));
        return body.RootElement.GetProperty("id").GetGuid();
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

    private static async Task<JsonDocument> ReadJsonAsync(HttpResponseMessage response)
        => JsonDocument.Parse(await response.Content.ReadAsStringAsync());
}
