using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using CareLanka.Api.Data;
using CareLanka.Api.Data.Entities.Patient;
using CareLanka.Api.Data.Enums;
using CareLanka.Api.Services.Equipment;
using CareLanka.Api.Services.Patient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PatientRecord = CareLanka.Api.Data.Entities.Patient.Patient;
using Xunit;

namespace CareLanka.Api.Tests;

[Collection(ApiCollection.Name)]
public sealed class BedEndpointTests
{
    private readonly ApiApplication _application;

    public BedEndpointTests(ApiApplication application) => _application = application;

    [Fact]
    public async Task Created_bed_comes_back_usable_and_echoes_what_was_sent()
    {
        using var client = await EquipmentClientAsync();
        var ward = Guid.NewGuid();

        var response = await CreateBedAsync(client, ward, "1", hasIsolation: true, distance: 3);
        using var body = await ReadJsonAsync(response);
        var bed = body.RootElement;

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(ward, bed.GetProperty("ward_id").GetGuid());
        Assert.Equal("1", bed.GetProperty("bed_number").GetString());
        Assert.True(bed.GetProperty("has_isolation").GetBoolean());
        Assert.Equal(3, bed.GetProperty("nurse_station_distance").GetInt32());

        Assert.Equal("usable", bed.GetProperty("condition").GetString());
    }

    [Fact]
    public async Task Ward_name_is_visibly_a_stub_rather_than_a_plausible_invented_name()
    {
        using var client = await EquipmentClientAsync();

        using var body = await ReadJsonAsync(await CreateBedAsync(client, Guid.NewGuid(), "1"));

        Assert.StartsWith("Stub ward ", body.RootElement.GetProperty("ward_name").GetString());
    }

    [Fact]
    public async Task Same_bed_number_twice_in_one_ward_is_a_conflict()
    {
        using var client = await EquipmentClientAsync();
        var ward = Guid.NewGuid();

        await CreateBedAsync(client, ward, "7");
        var second = await CreateBedAsync(client, ward, "7");
        using var body = await ReadJsonAsync(second);

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        Assert.Equal("application/problem+json", second.Content.Headers.ContentType?.MediaType);
        Assert.Equal("cl_equ_001", body.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task The_same_bed_number_in_a_different_ward_is_fine()
    {
        using var client = await EquipmentClientAsync();

        await CreateBedAsync(client, Guid.NewGuid(), "1");
        var other = await CreateBedAsync(client, Guid.NewGuid(), "1");

        Assert.Equal(HttpStatusCode.Created, other.StatusCode);
    }

    [Fact]
    public async Task An_asset_tag_cannot_be_used_twice()
    {
        using var client = await EquipmentClientAsync();
        var tag = $"BED-{Guid.NewGuid():N}"[..12];

        await CreateBedAsync(client, Guid.NewGuid(), "1", assetTag: tag);
        var second = await CreateBedAsync(client, Guid.NewGuid(), "1", assetTag: tag);
        using var body = await ReadJsonAsync(second);

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        Assert.Equal("cl_equ_002", body.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Two_beds_with_no_asset_tag_do_not_collide()
    {
        using var client = await EquipmentClientAsync();

        await CreateBedAsync(client, Guid.NewGuid(), "1", assetTag: null);
        var second = await CreateBedAsync(client, Guid.NewGuid(), "2", assetTag: "   ");

        Assert.Equal(HttpStatusCode.Created, second.StatusCode);
    }

    [Fact]
    public async Task Listing_filters_by_ward()
    {
        using var client = await EquipmentClientAsync();
        var ward = Guid.NewGuid();

        await CreateBedAsync(client, ward, "1");
        await CreateBedAsync(client, ward, "2");
        await CreateBedAsync(client, Guid.NewGuid(), "1");

        using var body = await ReadJsonAsync(await client.GetAsync($"/api/beds?wardId={ward}"));
        var root = body.RootElement;

        Assert.Equal(2, root.GetProperty("total_items").GetInt32());
        Assert.Equal(2, root.GetProperty("items").GetArrayLength());
        Assert.All(
            root.GetProperty("items").EnumerateArray(),
            bed => Assert.Equal(ward, bed.GetProperty("ward_id").GetGuid()));
    }

    [Fact]
    public async Task An_empty_page_still_reports_one_page_rather_than_zero()
    {
        using var client = await EquipmentClientAsync();

        using var body = await ReadJsonAsync(
            await client.GetAsync($"/api/beds?wardId={Guid.NewGuid()}"));

        Assert.Equal(0, body.RootElement.GetProperty("total_items").GetInt32());
        Assert.Equal(1, body.RootElement.GetProperty("total_pages").GetInt32());
    }

    [Fact]
    public async Task Taking_an_empty_bed_out_of_service_is_allowed()
    {
        using var client = await EquipmentClientAsync();
        var id = await NewBedIdAsync(client);

        var response = await client.PatchAsJsonAsync(
            $"/api/beds/{id}", new { condition = "out_of_service" });
        using var body = await ReadJsonAsync(response);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("out_of_service", body.RootElement.GetProperty("condition").GetString());
    }

    [Fact]
    public async Task Taking_a_bed_out_of_service_is_refused_while_a_patient_is_in_it()
    {
        using var client = await EquipmentClientAsync();
        var id = await NewBedIdAsync(client);
        await OccupyAsync(id);

        var response = await client.PatchAsJsonAsync(
            $"/api/beds/{id}", new { condition = "out_of_service" });
        using var body = await ReadJsonAsync(response);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("cl_equ_003", body.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Retiring_an_empty_bed_is_allowed()
    {
        using var client = await EquipmentClientAsync();
        var id = await NewBedIdAsync(client);

        var response = await client.PostAsync($"/api/beds/{id}/retire", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Retiring_a_bed_is_refused_while_a_patient_is_in_it()
    {
        using var client = await EquipmentClientAsync();
        var id = await NewBedIdAsync(client);
        await OccupyAsync(id);

        var response = await client.PostAsync($"/api/beds/{id}/retire", null);
        using var body = await ReadJsonAsync(response);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("cl_equ_003", body.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task An_edit_that_does_not_withdraw_the_bed_never_asks_about_occupancy()
    {
        using var client = await EquipmentClientAsync();
        var id = await NewBedIdAsync(client);

        var response = await client.PatchAsJsonAsync(
            $"/api/beds/{id}", new { has_isolation = true, nurse_station_distance = 9 });
        using var body = await ReadJsonAsync(response);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(body.RootElement.GetProperty("has_isolation").GetBoolean());
        Assert.Equal(9, body.RootElement.GetProperty("nurse_station_distance").GetInt32());
    }

    [Fact]
    public async Task An_absent_asset_tag_is_left_alone_and_an_explicit_null_clears_it()
    {
        using var client = await EquipmentClientAsync();
        var tag = $"KEEP-{Guid.NewGuid():N}"[..12];

        using var created = await ReadJsonAsync(
            await CreateBedAsync(client, Guid.NewGuid(), "1", assetTag: tag));
        var id = created.RootElement.GetProperty("id").GetGuid();

        using var untouched = await ReadJsonAsync(
            await client.PatchAsJsonAsync($"/api/beds/{id}", new { has_isolation = true }));
        Assert.Equal(tag, untouched.RootElement.GetProperty("asset_tag").GetString());

        using var cleared = await ReadJsonAsync(
            await client.PatchAsJsonAsync($"/api/beds/{id}", new { asset_tag = (string?)null }));
        Assert.Equal(JsonValueKind.Null, cleared.RootElement.GetProperty("asset_tag").ValueKind);
    }

    [Fact]
    public async Task A_bed_that_does_not_exist_is_a_404()
    {
        using var client = await EquipmentClientAsync();

        var response = await client.PatchAsJsonAsync(
            $"/api/beds/{Guid.NewGuid()}", new { has_isolation = true });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task A_nurse_may_read_the_register_but_not_write_to_it()
    {
        using var nurse = await ClientAsync(ApiApplication.NurseEmail);

        var read = await nurse.GetAsync("/api/beds");
        var write = await CreateBedAsync(nurse, Guid.NewGuid(), "1");

        Assert.Equal(HttpStatusCode.OK, read.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, write.StatusCode);
    }

    [Fact]
    public async Task The_register_is_not_readable_without_a_token()
    {
        using var anonymous = _application.CreateClient();

        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/beds")).StatusCode);
    }

    [Fact]
    public async Task Counting_by_ward_leaves_a_ward_with_no_beds_out_rather_than_at_zero()
    {
        using var client = await EquipmentClientAsync();
        var stocked = Guid.NewGuid();
        var empty = Guid.NewGuid();

        await CreateBedAsync(client, stocked, "1");
        await CreateBedAsync(client, stocked, "2");

        using var scope = _application.Services.CreateScope();
        var beds = scope.ServiceProvider.GetRequiredService<IBedService>();
        var counts = await beds.CountBedsByWardAsync(new[] { stocked, empty });

        Assert.Equal(2, counts[stocked]);
        Assert.DoesNotContain(empty, counts.Keys);
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
            Nic = $"E{Guid.NewGuid():N}"[..12],
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
            CategorySetByStaffMemberId = await SomeStaffIdAsync(db),
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
            Status = AssignmentStatus.Occupied
        });

        await db.SaveChangesAsync();
    }

    private static async Task<Guid> SomeStaffIdAsync(CareLankaDbContext db)
        => await db.StaffMembers.Select(staff => staff.Id).FirstAsync();

    private async Task<Guid> NewBedIdAsync(HttpClient client)
    {
        using var body = await ReadJsonAsync(await CreateBedAsync(client, Guid.NewGuid(), "1"));
        return body.RootElement.GetProperty("id").GetGuid();
    }

    private static Task<HttpResponseMessage> CreateBedAsync(
        HttpClient client,
        Guid wardId,
        string bedNumber,
        bool hasIsolation = false,
        int distance = 1,
        string? assetTag = null)
        => client.PostAsJsonAsync("/api/beds", new
        {
            ward_id = wardId,
            bed_number = bedNumber,
            has_isolation = hasIsolation,
            nurse_station_distance = distance,
            asset_tag = assetTag
        });

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
