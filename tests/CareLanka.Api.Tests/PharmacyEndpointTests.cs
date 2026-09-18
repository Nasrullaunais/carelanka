using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace CareLanka.Api.Tests;

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

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(7, body.RootElement.GetProperty("quantity_on_hand").GetInt32());
    }

    [Fact]
    public async Task A_delivery_is_a_new_batch_and_not_a_plain_movement()
    {
        using var client = await EquipmentClientAsync();
        var id = await NewItemIdAsync(client, quantity: 2);

        var asMovement = await MoveAsync(client, id, "received", 12);
        using var refused = await ReadJsonAsync(asMovement);

        using var added = await ReadJsonAsync(await AddBatchAsync(client, id, 12, "2027-01-31"));

        Assert.Equal(HttpStatusCode.BadRequest, asMovement.StatusCode);
        Assert.Equal("cl_equ_026", refused.RootElement.GetProperty("code").GetString());
        Assert.Equal(2, added.RootElement.GetProperty("batch_number").GetInt32());
        Assert.Equal(12, added.RootElement.GetProperty("quantity_on_hand").GetInt32());
        Assert.Equal(14, await QuantityAsync(client, id));
    }

    [Fact]
    public async Task The_opening_stock_of_a_new_item_is_its_first_batch()
    {
        using var client = await EquipmentClientAsync();
        var id = await NewItemIdAsync(client, quantity: 6);

        using var body = await ReadJsonAsync(await client.GetAsync($"/api/pharmacy-items/{id}/batches"));
        var batch = Assert.Single(body.RootElement.EnumerateArray());

        Assert.Equal(1, batch.GetProperty("batch_number").GetInt32());
        Assert.Equal(6, batch.GetProperty("quantity_on_hand").GetInt32());
    }

    [Fact]
    public async Task Dispensing_empties_the_batch_that_expires_first_before_touching_the_next()
    {
        using var client = await EquipmentClientAsync();
        var id = await NewItemIdAsync(client, quantity: 0);

        await AddBatchAsync(client, id, 4, "2027-06-30");
        await AddBatchAsync(client, id, 10, "2026-12-31");

        await MoveAsync(client, id, "dispensed", 12);

        using var body = await ReadJsonAsync(await client.GetAsync($"/api/pharmacy-items/{id}/batches"));
        var batches = body.RootElement.EnumerateArray()
            .ToDictionary(b => b.GetProperty("batch_number").GetInt32(),
                          b => b.GetProperty("quantity_on_hand").GetInt32());

        // Batch 2 expires first, so it goes first and batch 1 covers the rest.
        Assert.Equal(0, batches[2]);
        Assert.Equal(2, batches[1]);
        Assert.Equal(2, await QuantityAsync(client, id));
    }

    [Fact]
    public async Task The_history_says_which_batch_each_movement_came_from()
    {
        using var client = await EquipmentClientAsync();
        var id = await NewItemIdAsync(client, quantity: 0);

        await AddBatchAsync(client, id, 3, "2026-11-30");
        await AddBatchAsync(client, id, 5, "2027-11-30");
        await MoveAsync(client, id, "dispensed", 4);

        using var body = await ReadJsonAsync(
            await client.GetAsync($"/api/pharmacy-items/{id}/transactions?pageSize=10"));
        var rows = body.RootElement.GetProperty("items").EnumerateArray().ToList();

        var dispensed = rows.Where(r => r.GetProperty("type").GetString() == "dispensed").ToList();

        Assert.Equal(2, dispensed.Count);
        Assert.Contains(dispensed, r => r.GetProperty("batch_number").GetInt32() == 1
                                        && r.GetProperty("quantity").GetInt32() == 3);
        Assert.Contains(dispensed, r => r.GetProperty("batch_number").GetInt32() == 2
                                        && r.GetProperty("quantity").GetInt32() == 1);
    }

    [Fact]
    public async Task A_medicine_the_hospital_no_longer_stocks_is_removed_once_the_shelf_is_empty()
    {
        using var client = await EquipmentClientAsync();
        var id = await NewItemIdAsync(client, quantity: 4);

        var withStock = await RemoveAsync(client, id, ApiApplication.EquipmentConfirmationCode);
        using var refused = await ReadJsonAsync(withStock);

        await MoveAsync(client, id, "dispensed", 4);
        var removed = await RemoveAsync(client, id, ApiApplication.EquipmentConfirmationCode);
        var afterwards = await client.GetAsync($"/api/pharmacy-items/{id}");

        Assert.Equal(HttpStatusCode.Conflict, withStock.StatusCode);
        Assert.Equal("cl_equ_027", refused.RootElement.GetProperty("code").GetString());
        Assert.Equal(HttpStatusCode.NoContent, removed.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, afterwards.StatusCode);
    }

    [Fact]
    public async Task Removing_a_medicine_needs_the_confirmation_code_and_pharmacy_staff()
    {
        using var client = await EquipmentClientAsync();
        using var nurse = await ClientAsync(ApiApplication.NurseEmail);
        var id = await NewItemIdAsync(client, quantity: 0);

        var wrongCode = await RemoveAsync(client, id, "not-the-code");
        var noCode = await client.DeleteAsync($"/api/pharmacy-items/{id}");
        var byNurse = await RemoveAsync(nurse, id, ApiApplication.EquipmentConfirmationCode);

        Assert.Equal(HttpStatusCode.Forbidden, wrongCode.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, noCode.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, byNurse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/pharmacy-items/{id}")).StatusCode);
    }

    [Fact]
    public async Task The_name_of_a_removed_medicine_can_be_used_again()
    {
        using var client = await EquipmentClientAsync();
        var name = $"Item {Guid.NewGuid():N}"[..20];

        using var first = await ReadJsonAsync(await CreateItemAsync(client, 0, name: name));
        await RemoveAsync(
            client, first.RootElement.GetProperty("id").GetGuid(),
            ApiApplication.EquipmentConfirmationCode);

        var again = await CreateItemAsync(client, 0, name: name);

        Assert.Equal(HttpStatusCode.Created, again.StatusCode);
    }

    [Fact]
    public async Task A_movement_can_name_the_batch_it_comes_out_of()
    {
        using var client = await EquipmentClientAsync();
        var id = await NewItemIdAsync(client, quantity: 0);

        await AddBatchAsync(client, id, 5, "2026-12-31");
        var second = await BatchIdAsync(client, id, 2, () => AddBatchAsync(client, id, 9, "2027-12-31"));

        // The first batch expires sooner, so this is stock the earliest-expiry rule would not have
        // touched.
        var response = await MoveBatchAsync(client, id, second, "dispensed", 4);

        using var batches = await ReadJsonAsync(await client.GetAsync($"/api/pharmacy-items/{id}/batches"));
        var quantities = batches.RootElement.EnumerateArray()
            .ToDictionary(b => b.GetProperty("batch_number").GetInt32(),
                          b => b.GetProperty("quantity_on_hand").GetInt32());

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(5, quantities[1]);
        Assert.Equal(5, quantities[2]);
        Assert.Equal(10, await QuantityAsync(client, id));
    }

    [Fact]
    public async Task A_batch_movement_cannot_take_more_than_that_batch_holds()
    {
        using var client = await EquipmentClientAsync();
        var id = await NewItemIdAsync(client, quantity: 0);

        var first = await BatchIdAsync(client, id, 1, () => AddBatchAsync(client, id, 2, "2026-12-31"));
        await AddBatchAsync(client, id, 50, "2027-12-31");

        var response = await MoveBatchAsync(client, id, first, "dispensed", 6);
        using var body = await ReadJsonAsync(response);

        // The shelf holds 52 in total; this batch holds 2, and that is what the message says.
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("cl_equ_010", body.RootElement.GetProperty("code").GetString());
        Assert.Equal(52, await QuantityAsync(client, id));
    }

    [Fact]
    public async Task A_batch_movement_against_an_unknown_batch_is_a_404()
    {
        using var client = await EquipmentClientAsync();
        var id = await NewItemIdAsync(client, quantity: 3);

        var response = await MoveBatchAsync(client, id, Guid.NewGuid(), "dispensed", 1);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task An_item_and_its_batches_always_add_up_to_the_same_number()
    {
        using var client = await EquipmentClientAsync();
        var id = await NewItemIdAsync(client, quantity: 5);

        await AddBatchAsync(client, id, 7, "2027-02-28");
        await MoveAsync(client, id, "dispensed", 6);
        await MoveAsync(client, id, "adjusted", 2, "Stocktake found two extra boxes.");

        using var body = await ReadJsonAsync(await client.GetAsync($"/api/pharmacy-items/{id}/batches"));
        var batched = body.RootElement.EnumerateArray()
            .Sum(b => b.GetProperty("quantity_on_hand").GetInt32());

        Assert.Equal(await QuantityAsync(client, id), batched);
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

        Assert.Equal(2, rows[0].GetProperty("quantity").GetInt32());
        Assert.Equal("dispensed", rows[0].GetProperty("type").GetString());

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

    private static Task<HttpResponseMessage> RemoveAsync(HttpClient client, Guid id, string code)
    {
        var request = new HttpRequestMessage(HttpMethod.Delete, $"/api/pharmacy-items/{id}");
        request.Headers.Add("X-Confirmation-Code", code);

        return client.SendAsync(request);
    }

    private static Task<HttpResponseMessage> MoveBatchAsync(
        HttpClient client, Guid id, Guid batchId, string type, int quantity, string? note = null)
        => client.PostAsJsonAsync(
            $"/api/pharmacy-items/{id}/batches/{batchId}/transactions", new { type, quantity, note });

    private static async Task<Guid> BatchIdAsync(
        HttpClient client, Guid id, int batchNumber, Func<Task<HttpResponseMessage>> add)
    {
        (await add()).EnsureSuccessStatusCode();

        using var body = await ReadJsonAsync(await client.GetAsync($"/api/pharmacy-items/{id}/batches"));

        return body.RootElement.EnumerateArray()
            .Single(b => b.GetProperty("batch_number").GetInt32() == batchNumber)
            .GetProperty("id").GetGuid();
    }

    private static Task<HttpResponseMessage> AddBatchAsync(
        HttpClient client, Guid id, int quantity, string? expiryDate = null)
        => client.PostAsJsonAsync(
            $"/api/pharmacy-items/{id}/batches", new { quantity, expiry_date = expiryDate });

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
