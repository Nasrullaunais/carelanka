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

/// <summary>
/// The status machine, exercised through every endpoint that can move an item. One guard
/// decides what is legal, so these are the tests that say what "legal" means.
/// </summary>
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

        // The one documented exemption from the transition table. Assigned -> maintenance is
        // refused everywhere else, but a person saying the machine is broken outranks the
        // table: the alternative is a known-faulty item still reading as usable. Detaching
        // the patient is the intended consequence, which is why it is asserted here.
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

        // Status and warning are one SaveChanges. If the exemption above ever stops writing
        // the warning, the item goes quiet in maintenance with nobody told why.
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

        // The exemption belongs to report-fault alone. Editing an assigned item straight into
        // maintenance would drop a patient's equipment with no fault recorded anywhere.
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

        // Retired stays terminal even for a fault. A scrapped item has nothing left to report,
        // and a replacement is a new row.
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

        // Routed through the same guard as everything else, but the caller still reads the
        // specific code. "Not available" tells a technician more than the transition wording.
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

        // Available -> assigned is a legal move, but only the assign endpoint carries the
        // admission id. Allowing it here would leave an item assigned to nobody.
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task An_item_in_maintenance_can_go_back_into_service_or_be_retired()
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

        Assert.Equal(HttpStatusCode.OK, back.StatusCode);
        Assert.Equal(HttpStatusCode.OK, gone.StatusCode);
    }

    [Fact]
    public async Task Any_staff_member_may_report_a_fault_even_though_only_equipment_may_assign()
    {
        using var equipment = await EquipmentClientAsync();
        using var nurse = await ClientAsync(ApiApplication.NurseEmail);
        var id = await NewItemIdAsync(equipment);

        var reported = await ReportFaultAsync(nurse, id, "Sparking at the plug.");
        var assigned = await AssignAsync(nurse, id, Guid.NewGuid());

        // A nurse at the bedside is the person who finds the fault, so the report is open to
        // any staff. Moving stock around the hospital is not.
        Assert.Equal(HttpStatusCode.OK, reported.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, assigned.StatusCode);
    }

    private static Task<HttpResponseMessage> AssignAsync(HttpClient client, Guid id, Guid admissionId)
        => client.PostAsJsonAsync($"/api/equipment-items/{id}/assign", new { admission_id = admissionId });

    private static Task<HttpResponseMessage> ReportFaultAsync(HttpClient client, Guid id, string description)
        => client.PostAsJsonAsync($"/api/equipment-items/{id}/report-fault", new { description });

    private static async Task<Guid> NewItemIdAsync(HttpClient client)
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
