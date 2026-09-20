using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using CareLanka.Api.Data;
using CareLanka.Api.Data.Entities.Patient;
using CareLanka.Api.Data.Enums;
using CareLanka.Api.Services.Patient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PatientRecord = CareLanka.Api.Data.Entities.Patient.Patient;
using Xunit;

namespace CareLanka.Api.Tests;

[Collection(ApiCollection.Name)]
public sealed class MaintenanceEndpointTests
{
    private readonly ApiApplication _application;

    public MaintenanceEndpointTests(ApiApplication application) => _application = application;

    [Fact]
    public async Task A_booked_service_names_the_asset_rather_than_printing_its_id()
    {
        using var client = await EquipmentClientAsync();
        using var administrator = await ClientAsync(ApiApplication.AdministratorEmail);
        var item = await NewItemAsync(client);

        var response = await ScheduleAsync(administrator, item.Id, DateTime.UtcNow.AddDays(30));
        using var body = await ReadJsonAsync(response);
        var schedule = body.RootElement;

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal("scheduled", schedule.GetProperty("status").GetString());

        Assert.Equal(
            $"{item.Name} (asset tag {item.AssetTag})",
            schedule.GetProperty("asset_label").GetString());

        Assert.Equal("user", schedule.GetProperty("created_by").GetString());
    }

    [Fact]
    public async Task A_service_whose_date_has_passed_reads_as_overdue_without_being_stored_that_way()
    {
        using var client = await EquipmentClientAsync();
        using var administrator = await ClientAsync(ApiApplication.AdministratorEmail);
        var item = await NewItemAsync(client);

        using var body = await ReadJsonAsync(
            await ScheduleAsync(administrator, item.Id, DateTime.UtcNow.AddDays(-3)));

        Assert.Equal("overdue", body.RootElement.GetProperty("status").GetString());
    }

    [Fact]
    public async Task The_overdue_filter_finds_it_and_the_not_overdue_filter_does_not()
    {
        using var client = await EquipmentClientAsync();
        using var administrator = await ClientAsync(ApiApplication.AdministratorEmail);
        var item = await NewItemAsync(client);

        using var created = await ReadJsonAsync(
            await ScheduleAsync(administrator, item.Id, DateTime.UtcNow.AddDays(-3)));
        var id = created.RootElement.GetProperty("id").GetGuid();

        Assert.Contains(id, await ScheduleIdsAsync(administrator, "?overdue=true&pageSize=100"));
        Assert.DoesNotContain(id, await ScheduleIdsAsync(administrator, "?overdue=false&pageSize=100"));

        Assert.Contains(id, await ScheduleIdsAsync(administrator, "?status=overdue&pageSize=100"));
    }

    [Fact]
    public async Task Confirming_a_service_done_puts_the_item_back_to_work_and_books_the_next_one()
    {
        using var client = await EquipmentClientAsync();
        using var administrator = await ClientAsync(ApiApplication.AdministratorEmail);
        var item = await NewItemAsync(client);

        await client.PostAsJsonAsync(
            $"/api/equipment-items/{item.Id}/report-fault", new { description = "Rattling fan." });

        using var created = await ReadJsonAsync(
            await ScheduleAsync(administrator, item.Id, DateTime.UtcNow, type: "repair"));
        var scheduleId = created.RootElement.GetProperty("id").GetGuid();

        var response = await ConfirmDoneAsync(scheduleId);
        using var done = await ReadJsonAsync(response);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("completed", done.RootElement.GetProperty("status").GetString());

        Assert.NotEqual(
            Guid.Empty, done.RootElement.GetProperty("performed_by_staff_id").GetGuid());

        using var detail = await ReadJsonAsync(
            await client.GetAsync($"/api/equipment-items/{item.Id}"));
        Assert.Equal("available", detail.RootElement.GetProperty("status").GetString());

        Assert.Empty(detail.RootElement.GetProperty("open_warnings").EnumerateArray());
    }

    [Fact]
    public async Task A_routine_service_advances_the_next_due_date_and_a_repair_does_not()
    {
        using var client = await EquipmentClientAsync();
        using var administrator = await ClientAsync(ApiApplication.AdministratorEmail);

        var serviced = await NewItemAsync(client);
        using var a = await ReadJsonAsync(
            await ScheduleAsync(administrator, serviced.Id, DateTime.UtcNow, type: "routine_service"));
        await ConfirmDoneAsync(a.RootElement.GetProperty("id").GetGuid());

        var repaired = await NewItemAsync(client);
        using var b = await ReadJsonAsync(
            await ScheduleAsync(administrator, repaired.Id, DateTime.UtcNow, type: "repair"));
        await ConfirmDoneAsync(b.RootElement.GetProperty("id").GetGuid());

        using var servicedDetail = await ReadJsonAsync(
            await client.GetAsync($"/api/equipment-items/{serviced.Id}"));
        using var repairedDetail = await ReadJsonAsync(
            await client.GetAsync($"/api/equipment-items/{repaired.Id}"));

        Assert.Equal(
            JsonValueKind.String,
            servicedDetail.RootElement.GetProperty("next_maintenance_due").ValueKind);

        Assert.Equal(
            JsonValueKind.Null,
            repairedDetail.RootElement.GetProperty("next_maintenance_due").ValueKind);
    }

    [Fact]
    public async Task Finishing_the_same_task_twice_is_refused()
    {
        using var client = await EquipmentClientAsync();
        using var administrator = await ClientAsync(ApiApplication.AdministratorEmail);
        var item = await NewItemAsync(client);

        using var created = await ReadJsonAsync(
            await ScheduleAsync(administrator, item.Id, DateTime.UtcNow));
        var id = created.RootElement.GetProperty("id").GetGuid();

        await ConfirmDoneAsync(id);
        var second = await ConfirmDoneAsync(id);
        using var body = await ReadJsonAsync(second);

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        Assert.Equal("cl_equ_012", body.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Booking_a_service_on_an_occupied_bed_is_refused_and_writes_nothing()
    {
        using var client = await EquipmentClientAsync();
        using var administrator = await ClientAsync(ApiApplication.AdministratorEmail);

        using var bed = await ReadJsonAsync(await client.PostAsJsonAsync("/api/beds", new
        {
            ward_id = Guid.NewGuid(),
            bed_number = "1",
            has_isolation = false,
            nurse_station_distance = 1
        }));
        var bedId = bed.RootElement.GetProperty("id").GetGuid();
        await OccupyAsync(bedId);

        var response = await administrator.PostAsJsonAsync("/api/maintenance-schedules", new
        {
            asset_type = "bed",
            asset_id = bedId,
            schedule_type = "routine_service",
            scheduled_date = DateTime.UtcNow.ToString("yyyy-MM-dd")
        });
        using var body = await ReadJsonAsync(response);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("cl_equ_003", body.RootElement.GetProperty("code").GetString());

        Assert.DoesNotContain(
            bedId,
            await AssetIdsAsync(administrator, "?assetType=bed&pageSize=100"));
    }

    [Fact]
    public async Task Booking_against_an_asset_that_does_not_exist_is_a_404()
    {
        using var client = await EquipmentClientAsync();
        using var administrator = await ClientAsync(ApiApplication.AdministratorEmail);

        var response = await ScheduleAsync(administrator, Guid.NewGuid(), DateTime.UtcNow);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task The_work_list_is_closed_to_a_nurse()
    {
        using var nurse = await ClientAsync(ApiApplication.NurseEmail);

        var read = await nurse.GetAsync("/api/maintenance-schedules");

        Assert.Equal(HttpStatusCode.Forbidden, read.StatusCode);
    }

    [Fact]
    public async Task The_equipment_manager_reports_faults_but_does_not_run_the_maintenance_unit()
    {
        using var equipment = await EquipmentClientAsync();
        var item = await NewItemAsync(equipment);

        var read = await equipment.GetAsync("/api/maintenance-schedules");
        var booked = await ScheduleAsync(equipment, item.Id, DateTime.UtcNow.AddDays(7));
        var faulted = await equipment.PostAsJsonAsync(
            $"/api/equipment-items/{item.Id}/report-fault", new { description = "Will not power on." });

        Assert.Equal(HttpStatusCode.Forbidden, read.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, booked.StatusCode);
        Assert.Equal(HttpStatusCode.OK, faulted.StatusCode);
    }

    [Fact]
    public async Task The_administrator_can_retire_a_machine_beyond_repair()
    {
        using var equipment = await EquipmentClientAsync();
        using var administrator = await ClientAsync(ApiApplication.AdministratorEmail);
        var item = await NewItemAsync(equipment);
        await equipment.PostAsJsonAsync(
            $"/api/equipment-items/{item.Id}/report-fault", new { description = "Cracked frame." });

        administrator.DefaultRequestHeaders.Add(
            "X-Confirmation-Code", ApiApplication.EquipmentConfirmationCode);
        var retired = await administrator.PostAsync($"/api/equipment-items/{item.Id}/retire", null);

        Assert.Equal(HttpStatusCode.OK, retired.StatusCode);
    }

    private async Task OccupyAsync(Guid bedId)
    {
        using var scope = _application.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CareLankaDbContext>();

        var patient = new PatientRecord
        {
            Id = Guid.NewGuid(),

            PatientCode = PatientCodes.Next(),
            FullName = "Bed Occupant",
            Nic = $"M{Guid.NewGuid():N}"[..12],
            Gender = Gender.Male
        };

        var admission = new Admission
        {
            Id = Guid.NewGuid(),
            PatientId = patient.Id,
            Source = AdmissionSource.WalkIn,
            Category = AdmissionCategory.Inpatient,
            Urgency = AdmissionUrgency.Routine,
            Status = AdmissionStatus.Admitted,
            CategorySetByStaffMemberId = await db.StaffMembers.Select(staff => staff.Id).FirstAsync(),
            CategorySetAt = DateTimeOffset.UtcNow,
            MissingFields = []
        };

        db.Add(patient);
        db.Add(admission);
        db.Add(new BedAssignment
        {
            Id = Guid.NewGuid(),
            AdmissionId = admission.Id,
            BedId = bedId,
            Status = AssignmentStatus.Occupied,
            AssignedBy = AssignedBy.User
        });

        await db.SaveChangesAsync();
    }

    private record Item(Guid Id, string Name, string AssetTag);

    // Only the hospital administrator, with the confirmation code, can say a job is done.
    private async Task<HttpResponseMessage> ConfirmDoneAsync(Guid scheduleId)
    {
        using var administrator = await ClientAsync(ApiApplication.AdministratorEmail);
        administrator.DefaultRequestHeaders.Add(
            "X-Confirmation-Code", ApiApplication.EquipmentConfirmationCode);

        return await administrator.PostAsync($"/api/maintenance-schedules/{scheduleId}/confirm", null);
    }

    private async Task<Item> NewItemAsync(HttpClient client)
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

        var id = body.RootElement.GetProperty("id").GetGuid();

        await ConfirmAsync(id);

        return new Item(id, "Ventilator", tag);
    }

    private async Task ConfirmAsync(Guid id)
    {
        using var administrator = await ClientAsync(ApiApplication.AdministratorEmail);
        administrator.DefaultRequestHeaders.Add(
            "X-Confirmation-Code", ApiApplication.EquipmentConfirmationCode);

        var response = await administrator.PostAsync($"/api/equipment-items/{id}/confirm", null);
        response.EnsureSuccessStatusCode();
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
