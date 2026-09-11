using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace CareLanka.Api.Tests;

/// <summary>
/// Servicing, and the two rules around it: overdue is worked out when you ask rather than
/// stored, and maintenance never evicts a patient.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class MaintenanceEndpointTests
{
    private readonly ApiApplication _application;

    public MaintenanceEndpointTests(ApiApplication application) => _application = application;

    [Fact]
    public async Task A_booked_service_names_the_asset_rather_than_printing_its_id()
    {
        using var client = await EquipmentClientAsync();
        var item = await NewItemAsync(client);

        var response = await ScheduleAsync(client, item.Id, DateTime.UtcNow.AddDays(30));
        using var body = await ReadJsonAsync(response);
        var schedule = body.RootElement;

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal("scheduled", schedule.GetProperty("status").GetString());

        // A task list of GUIDs is unusable on a phone, which is where this is read.
        Assert.Equal(
            $"{item.Name} (asset tag {item.AssetTag})",
            schedule.GetProperty("asset_label").GetString());

        // A person booked it, not the sweep. The agent-performance report is exactly that
        // question, so it is recorded rather than assumed.
        Assert.Equal("user", schedule.GetProperty("created_by").GetString());
    }

    [Fact]
    public async Task A_service_whose_date_has_passed_reads_as_overdue_without_being_stored_that_way()
    {
        using var client = await EquipmentClientAsync();
        var item = await NewItemAsync(client);

        using var body = await ReadJsonAsync(
            await ScheduleAsync(client, item.Id, DateTime.UtcNow.AddDays(-3)));

        // Written as scheduled, read back as overdue. Nothing swept the table to make that
        // true, which is the point: there is no nightly job to forget to run.
        Assert.Equal("overdue", body.RootElement.GetProperty("status").GetString());
    }

    [Fact]
    public async Task The_overdue_filter_finds_it_and_the_not_overdue_filter_does_not()
    {
        using var client = await EquipmentClientAsync();
        var item = await NewItemAsync(client);

        using var created = await ReadJsonAsync(
            await ScheduleAsync(client, item.Id, DateTime.UtcNow.AddDays(-3)));
        var id = created.RootElement.GetProperty("id").GetGuid();

        Assert.Contains(id, await ScheduleIdsAsync(client, "?overdue=true&pageSize=100"));
        Assert.DoesNotContain(id, await ScheduleIdsAsync(client, "?overdue=false&pageSize=100"));

        // Asking by status is the same question in different words, so it has to agree.
        Assert.Contains(id, await ScheduleIdsAsync(client, "?status=overdue&pageSize=100"));
    }

    [Fact]
    public async Task Completing_a_service_puts_the_item_back_to_work_and_books_the_next_one()
    {
        using var client = await EquipmentClientAsync();
        var item = await NewItemAsync(client);

        // Reporting a fault is what puts an item into maintenance in the first place.
        await client.PostAsJsonAsync(
            $"/api/equipment-items/{item.Id}/report-fault", new { description = "Rattling fan." });

        using var created = await ReadJsonAsync(
            await ScheduleAsync(client, item.Id, DateTime.UtcNow, type: "repair"));
        var scheduleId = created.RootElement.GetProperty("id").GetGuid();

        var response = await client.PostAsJsonAsync(
            $"/api/maintenance-schedules/{scheduleId}/complete", new { notes = "Fan replaced." });
        using var done = await ReadJsonAsync(response);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("completed", done.RootElement.GetProperty("status").GetString());
        Assert.Equal("Fan replaced.", done.RootElement.GetProperty("notes").GetString());

        // Who did the work comes from the token. There is no field in the body for it.
        Assert.NotEqual(
            Guid.Empty, done.RootElement.GetProperty("performed_by_staff_id").GetGuid());

        // An item left in maintenance after its service is finished is invisible stock.
        using var detail = await ReadJsonAsync(
            await client.GetAsync($"/api/equipment-items/{item.Id}"));
        Assert.Equal("available", detail.RootElement.GetProperty("status").GetString());

        // The warning that led here is answered by the work, not by someone remembering.
        Assert.Empty(detail.RootElement.GetProperty("open_warnings").EnumerateArray());
    }

    [Fact]
    public async Task A_routine_service_advances_the_next_due_date_and_a_repair_does_not()
    {
        using var client = await EquipmentClientAsync();

        var serviced = await NewItemAsync(client);
        using var a = await ReadJsonAsync(
            await ScheduleAsync(client, serviced.Id, DateTime.UtcNow, type: "routine_service"));
        await client.PostAsJsonAsync(
            $"/api/maintenance-schedules/{a.RootElement.GetProperty("id").GetGuid()}/complete",
            new { });

        var repaired = await NewItemAsync(client);
        using var b = await ReadJsonAsync(
            await ScheduleAsync(client, repaired.Id, DateTime.UtcNow, type: "repair"));
        await client.PostAsJsonAsync(
            $"/api/maintenance-schedules/{b.RootElement.GetProperty("id").GetGuid()}/complete",
            new { });

        using var servicedDetail = await ReadJsonAsync(
            await client.GetAsync($"/api/equipment-items/{serviced.Id}"));
        using var repairedDetail = await ReadJsonAsync(
            await client.GetAsync($"/api/equipment-items/{repaired.Id}"));

        // A routine service restarts the clock.
        Assert.Equal(
            JsonValueKind.String,
            servicedDetail.RootElement.GetProperty("next_maintenance_due").ValueKind);

        // A repair is unplanned work and deliberately does not. An item repaired in March is
        // still due its routine service in June.
        Assert.Equal(
            JsonValueKind.Null,
            repairedDetail.RootElement.GetProperty("next_maintenance_due").ValueKind);
    }

    [Fact]
    public async Task Finishing_the_same_task_twice_is_refused()
    {
        using var client = await EquipmentClientAsync();
        var item = await NewItemAsync(client);

        using var created = await ReadJsonAsync(
            await ScheduleAsync(client, item.Id, DateTime.UtcNow));
        var id = created.RootElement.GetProperty("id").GetGuid();

        await client.PostAsJsonAsync($"/api/maintenance-schedules/{id}/complete", new { });
        var second = await client.PostAsJsonAsync(
            $"/api/maintenance-schedules/{id}/complete", new { });
        using var body = await ReadJsonAsync(second);

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        Assert.Equal("cl_equ_012", body.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Booking_a_service_on_an_occupied_bed_is_refused_and_writes_nothing()
    {
        using var client = await EquipmentClientAsync();

        using var bed = await ReadJsonAsync(await client.PostAsJsonAsync("/api/beds", new
        {
            ward_id = Guid.NewGuid(),
            bed_number = "1",
            has_isolation = false,
            nurse_station_distance = 1
        }));
        var bedId = bed.RootElement.GetProperty("id").GetGuid();

        var response = await client.PostAsJsonAsync("/api/maintenance-schedules", new
        {
            asset_type = "bed",
            asset_id = bedId,
            schedule_type = "routine_service",
            scheduled_date = DateTime.UtcNow.ToString("yyyy-MM-dd")
        });
        using var body = await ReadJsonAsync(response);

        // STUBS.md row 3: the occupancy stub answers "occupied" on purpose, so this is the
        // fail-safe path rather than a bug. Maintenance never evicts a patient, and the
        // check happens before anything is written.
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("cl_equ_003", body.RootElement.GetProperty("code").GetString());

        Assert.DoesNotContain(
            bedId,
            await AssetIdsAsync(client, "?assetType=bed&pageSize=100"));
    }

    [Fact]
    public async Task Booking_against_an_asset_that_does_not_exist_is_a_404()
    {
        using var client = await EquipmentClientAsync();

        var response = await ScheduleAsync(client, Guid.NewGuid(), DateTime.UtcNow);

        // The reference is polymorphic, so there is no foreign key to catch this for us.
        // Without the check the row would point at nothing and read as "Unknown asset".
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task The_work_list_is_closed_to_a_nurse()
    {
        using var nurse = await ClientAsync(ApiApplication.NurseEmail);

        var read = await nurse.GetAsync("/api/maintenance-schedules");

        // Reporting a fault is open to any staff member, because the nurse at the bedside is
        // who finds it. Scheduling and closing the work is not.
        Assert.Equal(HttpStatusCode.Forbidden, read.StatusCode);
    }

    private record Item(Guid Id, string Name, string AssetTag);

    private static async Task<Item> NewItemAsync(HttpClient client)
    {
        using var category = await ReadJsonAsync(await client.PostAsJsonAsync(
            "/api/equipment-categories", new { name = $"Cat {Guid.NewGuid():N}"[..18] }));

        var tag = $"EQ-{Guid.NewGuid():N}"[..14];

        using var body = await ReadJsonAsync(await client.PostAsJsonAsync("/api/equipment-items", new
        {
            name = "Ventilator",
            category_id = category.RootElement.GetProperty("id").GetGuid(),
            model = "V-100",
            manufacturer = "Acme Medical",
            purchase_date = "2026-01-05",
            asset_tag = tag,
            ward_id = Guid.NewGuid()
        }));

        return new Item(body.RootElement.GetProperty("id").GetGuid(), "Ventilator", tag);
    }

    private static Task<HttpResponseMessage> ScheduleAsync(
        HttpClient client, Guid assetId, DateTime on, string type = "routine_service")
        => client.PostAsJsonAsync("/api/maintenance-schedules", new
        {
            asset_type = "equipment_item",
            asset_id = assetId,
            schedule_type = type,
            scheduled_date = on.ToString("yyyy-MM-dd")
        });

    private static async Task<List<Guid>> ScheduleIdsAsync(HttpClient client, string query)
        => await IdsAsync(client, query, "id");

    private static async Task<List<Guid>> AssetIdsAsync(HttpClient client, string query)
        => await IdsAsync(client, query, "asset_id");

    private static async Task<List<Guid>> IdsAsync(HttpClient client, string query, string field)
    {
        using var body = await ReadJsonAsync(
            await client.GetAsync($"/api/maintenance-schedules{query}"));

        return body.RootElement.GetProperty("items").EnumerateArray()
            .Select(row => row.GetProperty(field).GetGuid())
            .ToList();
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
