using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using CareLanka.Api.Data;
using CareLanka.Api.Data.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CareLanka.Api.Tests;

[Collection(ApiCollection.Name)]
public sealed class EquipmentItemLifecycleTests
{
    private readonly ApiApplication _application;

    public EquipmentItemLifecycleTests(ApiApplication application) => _application = application;

    [Fact]
    public async Task A_fault_reported_on_an_assigned_item_wins_over_the_assignment()
    {
        using var client = await EquipmentClientAsync();
        var id = await NewItemIdAsync(client);
        await AssignAsync(client, id, Guid.NewGuid());

        var response = await ReportFaultAsync(client, id, "Screen dead mid-round.");
        using var body = await ReadJsonAsync(response);
        var item = body.RootElement;

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("maintenance", item.GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.Null, item.GetProperty("assigned_to_admission_id").ValueKind);
    }

    [Fact]
    public async Task That_fault_raises_a_high_warning_rather_than_leaving_it_to_the_sweep()
    {
        using var client = await EquipmentClientAsync();
        var id = await NewItemIdAsync(client);
        await AssignAsync(client, id, Guid.NewGuid());

        await ReportFaultAsync(client, id, "Alarm silent on self-test.");

        using var scope = _application.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CareLankaDbContext>();
        var warning = await db.Warnings.AsNoTracking()
            .SingleAsync(w => w.RelatedEntityId == id);

        Assert.Equal(WarningType.EquipmentFaulty, warning.Type);
        Assert.Equal(WarningSeverity.High, warning.Severity);
        Assert.Equal(WarningStatus.Open, warning.Status);
        Assert.Equal("Alarm silent on self-test.", warning.RecommendedAction);
    }

    [Fact]
    public async Task The_same_move_asked_for_as_a_plain_edit_is_still_refused()
    {
        using var client = await EquipmentClientAsync();
        var id = await NewItemIdAsync(client);
        await AssignAsync(client, id, Guid.NewGuid());

        var response = await client.PutAsJsonAsync(
            $"/api/equipment-items/{id}", new { status = "maintenance" });
        using var body = await ReadJsonAsync(response);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("cl_err_409_transition", body.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task A_fault_cannot_bring_a_retired_item_back()
    {
        using var client = await EquipmentClientAsync();
        var id = await NewItemIdAsync(client);
        await client.PutAsJsonAsync($"/api/equipment-items/{id}", new { status = "retired" });

        var response = await ReportFaultAsync(client, id, "Found in the corridor.");
        using var body = await ReadJsonAsync(response);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("cl_equ_006", body.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task An_item_already_with_a_patient_cannot_be_assigned_to_a_second_one()
    {
        using var client = await EquipmentClientAsync();
        var id = await NewItemIdAsync(client);
        await AssignAsync(client, id, Guid.NewGuid());

        var response = await AssignAsync(client, id, Guid.NewGuid());
        using var body = await ReadJsonAsync(response);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("cl_equ_006", body.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Releasing_an_item_nobody_is_using_is_refused()
    {
        using var client = await EquipmentClientAsync();
        var id = await NewItemIdAsync(client);

        var response = await client.PostAsync($"/api/equipment-items/{id}/release", null);
        using var body = await ReadJsonAsync(response);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("cl_equ_007", body.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Releasing_an_assigned_item_hands_it_back_and_clears_the_patient()
    {
        using var client = await EquipmentClientAsync();
        var id = await NewItemIdAsync(client);
        await AssignAsync(client, id, Guid.NewGuid());

        var response = await client.PostAsync($"/api/equipment-items/{id}/release", null);
        using var body = await ReadJsonAsync(response);
        var item = body.RootElement;

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("available", item.GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.Null, item.GetProperty("assigned_to_admission_id").ValueKind);
    }

    [Fact]
    public async Task The_edit_endpoint_will_not_assign_because_it_has_no_admission_to_name()
    {
        using var client = await EquipmentClientAsync();
        var id = await NewItemIdAsync(client);

        var response = await client.PutAsJsonAsync(
            $"/api/equipment-items/{id}", new { status = "assigned" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task An_item_with_the_maintenance_unit_cannot_be_talked_back_into_service()
    {
        using var client = await EquipmentClientAsync();
        var repaired = await NewItemIdAsync(client);
        var scrapped = await NewItemIdAsync(client);

        await ReportFaultAsync(client, repaired, "Loose casing.");
        await ReportFaultAsync(client, scrapped, "Beyond economic repair.");

        var back = await client.PutAsJsonAsync(
            $"/api/equipment-items/{repaired}", new { status = "available" });
        var gone = await client.PutAsJsonAsync(
            $"/api/equipment-items/{scrapped}", new { status = "retired" });

        Assert.Equal(HttpStatusCode.Conflict, back.StatusCode);

        Assert.Equal(HttpStatusCode.OK, gone.StatusCode);
    }

    [Fact]
    public async Task Reporting_a_fault_opens_one_repair_job_for_the_maintenance_unit()
    {
        using var client = await EquipmentClientAsync();
        var id = await NewItemIdAsync(client);

        await ReportFaultAsync(client, id, "Sparking at the plug.");
        await ReportFaultAsync(client, id, "Also rattling.");

        var jobs = await OpenRepairJobsAsync(id);

        var job = Assert.Single(jobs);
        Assert.Equal(MaintenanceType.Repair, job.ScheduleType);
        Assert.Equal("Sparking at the plug.", job.Notes);
    }

    [Fact]
    public async Task Completing_the_repair_is_what_returns_the_item_to_service()
    {
        using var client = await EquipmentClientAsync();
        var id = await NewItemIdAsync(client);
        await ReportFaultAsync(client, id, "Screen flickering.");

        var job = Assert.Single(await OpenRepairJobsAsync(id));
        var completed = await client.PostAsJsonAsync(
            $"/api/maintenance-schedules/{job.Id}/complete", new { notes = "New backlight." });

        using var body = await ReadJsonAsync(await client.GetAsync($"/api/equipment-items/{id}"));

        Assert.Equal(HttpStatusCode.OK, completed.StatusCode);
        Assert.Equal("available", body.RootElement.GetProperty("status").GetString());

        Assert.Empty(await OpenFaultWarningsAsync(id));
    }

    [Fact]
    public async Task Retiring_a_faulty_item_clears_it_out_of_the_unit_queue()
    {
        using var client = await EquipmentClientAsync();
        var id = await NewItemIdAsync(client);
        await ReportFaultAsync(client, id, "Cracked housing, not worth repairing.");

        var retired = await client.PutAsJsonAsync(
            $"/api/equipment-items/{id}", new { status = "retired" });

        Assert.Equal(HttpStatusCode.OK, retired.StatusCode);

        Assert.Empty(await OpenRepairJobsAsync(id));
        Assert.Empty(await OpenFaultWarningsAsync(id));
    }

    private async Task<List<Data.Entities.Equipment.MaintenanceSchedule>> OpenRepairJobsAsync(Guid itemId)
    {
        using var scope = _application.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CareLankaDbContext>();

        return await db.MaintenanceSchedules
            .Where(s => s.AssetType == AssetType.EquipmentItem
                        && s.AssetId == itemId
                        && (s.Status == MaintenanceStatus.Scheduled
                            || s.Status == MaintenanceStatus.InProgress))
            .ToListAsync();
    }

    private async Task<List<Data.Entities.Equipment.Warning>> OpenFaultWarningsAsync(Guid itemId)
    {
        using var scope = _application.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CareLankaDbContext>();

        return await db.Warnings
            .Where(w => w.RelatedEntityType == RelatedEntityType.EquipmentItem
                        && w.RelatedEntityId == itemId
                        && w.Status == WarningStatus.Open)
            .ToListAsync();
    }

    [Fact]
    public async Task Any_staff_member_may_report_a_fault_even_though_only_equipment_may_assign()
    {
        using var equipment = await EquipmentClientAsync();
        using var nurse = await ClientAsync(ApiApplication.NurseEmail);
        var id = await NewItemIdAsync(equipment);

        var reported = await ReportFaultAsync(nurse, id, "Sparking at the plug.");
        var assigned = await AssignAsync(nurse, id, Guid.NewGuid());

        Assert.Equal(HttpStatusCode.OK, reported.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, assigned.StatusCode);
    }

    private static Task<HttpResponseMessage> AssignAsync(HttpClient client, Guid id, Guid admissionId)
        => client.PostAsJsonAsync($"/api/equipment-items/{id}/assign", new { admission_id = admissionId });

    private static Task<HttpResponseMessage> ReportFaultAsync(HttpClient client, Guid id, string description)
        => client.PostAsJsonAsync($"/api/equipment-items/{id}/report-fault", new { description });

    private async Task<Guid> NewItemIdAsync(HttpClient client)
    {
        using var category = await ReadJsonAsync(await client.PostAsJsonAsync(
            "/api/equipment-categories", new { name = $"Category {Guid.NewGuid():N}"[..20] }));

        using var body = await ReadJsonAsync(await client.PostAsJsonAsync("/api/equipment-items", new
        {
            name = "Ventilator",
            category_id = category.RootElement.GetProperty("id").GetGuid(),
            model = "V-100",
            manufacturer = "Acme Medical",
            purchase_date = "2026-01-05",
            asset_tag = $"EQ-{Guid.NewGuid():N}"[..14],
            ward_id = Guid.NewGuid()
        }));

        var id = body.RootElement.GetProperty("id").GetGuid();

        await ConfirmAsync(id);

        return id;
    }

    // A registered item cannot be assigned, faulted or serviced until the hospital administrator
    // confirms it, so every test that needs a working item confirms it first.
    private async Task ConfirmAsync(Guid id)
    {
        using var administrator = await ClientAsync(ApiApplication.AdministratorEmail);
        administrator.DefaultRequestHeaders.Add(
            "X-Confirmation-Code", ApiApplication.EquipmentConfirmationCode);

        var response = await administrator.PostAsync($"/api/equipment-items/{id}/confirm", null);
        response.EnsureSuccessStatusCode();
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
