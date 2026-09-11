using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using CareLanka.Api.Data;
using CareLanka.Api.Data.Entities.Patient;
using CareLanka.Api.Data.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CareLanka.Api.Tests;

/// <summary>
/// GET /api/capacity/wards and GET /api/wards/{id}/occupancy — the two reads Emergency and
/// Staff Management have been blocked on. Both answer counts and nothing else.
/// </summary>
/// <remarks>
/// The fixture's database is shared by the whole collection, so every test here creates its
/// own ward and asserts on that ward's row rather than on the shape of the whole list.
/// </remarks>
[Collection(ApiCollection.Name)]
public sealed class CapacityEndpointTests
{
    private readonly ApiApplication _application;

    public CapacityEndpointTests(ApiApplication application) => _application = application;

    // ---------- free bed counting ----------

    [Fact]
    public async Task A_ward_with_no_beds_reports_zero_free_rather_than_being_left_out()
    {
        using var client = await ClientAsync(ApiApplication.AdministratorEmail);
        var ward = await NewWardAsync(client);

        var row = await CapacityRowAsync(client, ward);

        // Equipment's register leaves a ward with no beds out of its result entirely. A ward
        // missing from the capacity list reads as "no such ward" to a dispatcher, which is a
        // different thing from "no beds here".
        Assert.Equal(0, row.GetProperty("total_beds").GetInt32());
        Assert.Equal(0, row.GetProperty("free_beds").GetInt32());
    }

    [Fact]
    public async Task An_empty_usable_bed_is_free()
    {
        using var client = await ClientAsync(ApiApplication.AdministratorEmail);
        var ward = await NewWardAsync(client);
        await AddBedsAsync(ward, 3);

        var row = await CapacityRowAsync(client, ward);

        Assert.Equal(3, row.GetProperty("total_beds").GetInt32());
        Assert.Equal(3, row.GetProperty("free_beds").GetInt32());
    }

    [Fact]
    public async Task A_bed_out_of_service_still_counts_in_the_total_and_never_as_free()
    {
        using var client = await ClientAsync(ApiApplication.AdministratorEmail);
        var ward = await NewWardAsync(client);
        var beds = await AddBedsAsync(ward, 3);
        await SetConditionAsync(beds[0], BedCondition.OutOfService);

        var row = await CapacityRowAsync(client, ward);

        // Total is every frame standing in the ward. Free is the ones a patient could go in.
        // Dropping a withdrawn bed out of the total would make the ward look permanently
        // smaller than it is and hide how much of it is broken.
        Assert.Equal(3, row.GetProperty("total_beds").GetInt32());
        Assert.Equal(2, row.GetProperty("free_beds").GetInt32());
    }

    [Fact]
    public async Task A_bed_with_a_patient_in_it_is_not_free()
    {
        using var client = await ClientAsync(ApiApplication.AdministratorEmail);
        var ward = await NewWardAsync(client);
        var beds = await AddBedsAsync(ward, 2);
        await OccupyAsync(beds[0]);

        var row = await CapacityRowAsync(client, ward);

        Assert.Equal(1, row.GetProperty("free_beds").GetInt32());
    }

    [Fact]
    public async Task A_hold_that_still_stands_takes_the_bed_out_of_the_free_count()
    {
        using var client = await ClientAsync(ApiApplication.AdministratorEmail);
        var ward = await NewWardAsync(client);
        var beds = await AddBedsAsync(ward, 2);
        await HoldAsync(beds[0], expiresIn: TimeSpan.FromMinutes(30));

        var row = await CapacityRowAsync(client, ward);

        Assert.Equal(1, row.GetProperty("free_beds").GetInt32());
    }

    // The promise this endpoint exists to keep: "A reservation past its reserved_until counts
    // as free. That expiry logic lives here, in the owning service, so no other component
    // re-implements it differently."
    [Fact]
    public async Task A_hold_past_its_expiry_frees_the_bed_with_nobody_having_done_anything()
    {
        using var client = await ClientAsync(ApiApplication.AdministratorEmail);
        var ward = await NewWardAsync(client);
        var beds = await AddBedsAsync(ward, 2);
        await HoldAsync(beds[0], expiresIn: TimeSpan.FromMinutes(-1));

        var row = await CapacityRowAsync(client, ward);

        // The row is still sitting in bed_assignments, untouched. Nothing swept it and no
        // human released it — it simply stopped counting the moment the clock passed it.
        Assert.Equal(2, row.GetProperty("free_beds").GetInt32());
    }

    [Fact]
    public async Task A_released_assignment_claims_nothing_because_it_is_history()
    {
        using var client = await ClientAsync(ApiApplication.AdministratorEmail);
        var ward = await NewWardAsync(client);
        var beds = await AddBedsAsync(ward, 1);
        await AssignAsync(beds[0], AssignmentStatus.Released, null, AdmissionStatus.Discharged);

        var row = await CapacityRowAsync(client, ward);

        Assert.Equal(1, row.GetProperty("free_beds").GetInt32());
    }

    [Fact]
    public async Task A_retired_ward_is_not_offered_as_capacity()
    {
        using var client = await ClientAsync(ApiApplication.AdministratorEmail);
        var ward = await NewWardAsync(client, isActive: false);

        using var body = await ReadJsonAsync(await client.GetAsync("/api/capacity/wards"));

        // Sending an ambulance to a ward nobody admits to is sending it to a closed door.
        Assert.DoesNotContain(
            body.RootElement.GetProperty("wards").EnumerateArray(),
            row => row.GetProperty("ward_id").GetString() == ward.Id);
    }

    [Fact]
    public async Task The_summary_carries_the_gender_policy_because_a_free_bed_can_be_unusable()
    {
        using var client = await ClientAsync(ApiApplication.AdministratorEmail);
        var ward = await NewWardAsync(client, wardType: "maternity", genderPolicy: "female");
        await AddBedsAsync(ward, 2);

        var row = await CapacityRowAsync(client, ward);

        // Two free beds in a female-only ward are no use to a male patient, and a dispatcher
        // has to be able to see that without a second request.
        Assert.Equal("maternity", row.GetProperty("ward_type").GetString());
        Assert.Equal("female", row.GetProperty("gender_policy").GetString());
    }

    [Fact]
    public async Task The_summary_says_when_it_was_counted_because_free_beds_go_stale_in_seconds()
    {
        using var client = await ClientAsync(ApiApplication.AdministratorEmail);
        var before = DateTimeOffset.UtcNow.AddSeconds(-5);

        using var body = await ReadJsonAsync(await client.GetAsync("/api/capacity/wards"));
        var generated = body.RootElement.GetProperty("generated_at").GetDateTimeOffset();

        Assert.InRange(generated, before, DateTimeOffset.UtcNow.AddSeconds(5));
    }

    // ---------- one ward's occupancy ----------

    [Fact]
    public async Task Occupancy_splits_the_beds_into_occupied_held_and_out_of_service()
    {
        using var client = await ClientAsync(ApiApplication.AdministratorEmail);
        var ward = await NewWardAsync(client);
        var beds = await AddBedsAsync(ward, 5);

        await OccupyAsync(beds[0]);
        await OccupyAsync(beds[1]);
        await HoldAsync(beds[2], expiresIn: TimeSpan.FromMinutes(30));
        await SetConditionAsync(beds[3], BedCondition.OutOfService);

        using var body = await ReadJsonAsync(await client.GetAsync($"/api/wards/{ward.Id}/occupancy"));
        var occupancy = body.RootElement;

        Assert.Equal(ward.Id, occupancy.GetProperty("ward_id").GetString());
        Assert.Equal(ward.Name, occupancy.GetProperty("name").GetString());
        Assert.Equal(5, occupancy.GetProperty("total_beds").GetInt32());
        Assert.Equal(2, occupancy.GetProperty("occupied_beds").GetInt32());
        Assert.Equal(1, occupancy.GetProperty("reserved_beds").GetInt32());
        Assert.Equal(1, occupancy.GetProperty("out_of_service_beds").GetInt32());
    }

    [Fact]
    public async Task A_lapsed_hold_is_not_a_reserved_bed_either()
    {
        using var client = await ClientAsync(ApiApplication.AdministratorEmail);
        var ward = await NewWardAsync(client);
        var beds = await AddBedsAsync(ward, 1);
        await HoldAsync(beds[0], expiresIn: TimeSpan.FromMinutes(-1));

        using var body = await ReadJsonAsync(await client.GetAsync($"/api/wards/{ward.Id}/occupancy"));

        // The same rule as the capacity summary, from the same helper. Two endpoints reading
        // expiry differently is exactly what putting it in one service is meant to prevent.
        Assert.Equal(0, body.RootElement.GetProperty("reserved_beds").GetInt32());
    }

    [Fact]
    public async Task The_care_mix_is_what_staffing_is_actually_worked_out_from()
    {
        using var client = await ClientAsync(ApiApplication.AdministratorEmail);
        var ward = await NewWardAsync(client);
        var beds = await AddBedsAsync(ward, 4);

        await OccupyAsync(beds[0], category: AdmissionCategory.Inpatient);
        await OccupyAsync(beds[1], category: AdmissionCategory.Inpatient);
        await OccupyAsync(beds[2], category: AdmissionCategory.Hdu);

        using var body = await ReadJsonAsync(await client.GetAsync($"/api/wards/{ward.Id}/occupancy"));
        var mix = body.RootElement.GetProperty("patients_by_category");

        // Two routine inpatients and one high-dependency patient is three patients and two
        // very different staffing answers. This split is the whole point of the endpoint.
        Assert.Equal(2, mix.GetProperty("inpatient").GetInt32());
        Assert.Equal(1, mix.GetProperty("hdu").GetInt32());
    }

    [Fact]
    public async Task Every_care_category_is_present_even_when_nobody_is_in_it()
    {
        using var client = await ClientAsync(ApiApplication.AdministratorEmail);
        var ward = await NewWardAsync(client);

        using var body = await ReadJsonAsync(await client.GetAsync($"/api/wards/{ward.Id}/occupancy"));
        var mix = body.RootElement.GetProperty("patients_by_category");

        // A key that disappears when it hits zero drops off a chart instead of falling to the
        // floor of it, and makes every reader write the same `?? 0`. day_case is the one that
        // proves the wire values are not just the C# names lowercased.
        Assert.Equal(
            new[] { "day_case", "hdu", "icu", "inpatient", "outpatient" },
            mix.EnumerateObject().Select(property => property.Name).Order().ToArray());
        Assert.All(mix.EnumerateObject(), property => Assert.Equal(0, property.Value.GetInt32()));
    }

    [Fact]
    public async Task Somebody_holding_a_bed_is_not_counted_as_a_patient_in_the_ward()
    {
        using var client = await ClientAsync(ApiApplication.AdministratorEmail);
        var ward = await NewWardAsync(client);
        var beds = await AddBedsAsync(ward, 2);

        await OccupyAsync(beds[0], category: AdmissionCategory.Icu);
        await HoldAsync(beds[1], expiresIn: TimeSpan.FromMinutes(30), category: AdmissionCategory.Icu);

        using var body = await ReadJsonAsync(await client.GetAsync($"/api/wards/{ward.Id}/occupancy"));

        // Counting the held bed here would tell a shift lead to staff for a patient who is not
        // in the building. They are reported as incoming instead, which is the honest answer.
        Assert.Equal(1, body.RootElement.GetProperty("patients_by_category").GetProperty("icu").GetInt32());
        Assert.Equal(1, body.RootElement.GetProperty("incoming_next_2h").GetInt32());
    }

    [Fact]
    public async Task Incoming_counts_a_held_bed_due_soon_and_not_one_due_tomorrow()
    {
        using var client = await ClientAsync(ApiApplication.AdministratorEmail);
        var ward = await NewWardAsync(client);
        var beds = await AddBedsAsync(ward, 3);

        await HoldAsync(beds[0], TimeSpan.FromMinutes(30), expectedArrival: TimeSpan.FromMinutes(20));
        await HoldAsync(beds[1], TimeSpan.FromMinutes(30), expectedArrival: TimeSpan.FromHours(26));
        await HoldAsync(beds[2], TimeSpan.FromMinutes(30), expectedArrival: null);

        using var body = await ReadJsonAsync(await client.GetAsync($"/api/wards/{ward.Id}/occupancy"));

        // Three held beds, two of them incoming. The one due tomorrow is somebody else's shift.
        // The one with no stated arrival counts: its hold lapses in thirty minutes, so it is
        // arriving sooner than two hours or losing the bed.
        Assert.Equal(3, body.RootElement.GetProperty("reserved_beds").GetInt32());
        Assert.Equal(2, body.RootElement.GetProperty("incoming_next_2h").GetInt32());
    }

    [Fact]
    public async Task Incoming_still_counts_somebody_who_is_running_late()
    {
        using var client = await ClientAsync(ApiApplication.AdministratorEmail);
        var ward = await NewWardAsync(client);
        var beds = await AddBedsAsync(ward, 1);

        await HoldAsync(beds[0], TimeSpan.FromMinutes(30), expectedArrival: TimeSpan.FromMinutes(-45));

        using var body = await ReadJsonAsync(await client.GetAsync($"/api/wards/{ward.Id}/occupancy"));

        // Somebody late is still expected, and still needs staffing for.
        Assert.Equal(1, body.RootElement.GetProperty("incoming_next_2h").GetInt32());
    }

    [Fact]
    public async Task An_admitted_patient_is_not_counted_as_incoming()
    {
        using var client = await ClientAsync(ApiApplication.AdministratorEmail);
        var ward = await NewWardAsync(client);
        var beds = await AddBedsAsync(ward, 1);
        await OccupyAsync(beds[0]);

        using var body = await ReadJsonAsync(await client.GetAsync($"/api/wards/{ward.Id}/occupancy"));

        // Incoming is read off the admission's status, not off the assignment's. Somebody who
        // has arrived is in patients_by_category and nowhere else.
        Assert.Equal(0, body.RootElement.GetProperty("incoming_next_2h").GetInt32());
    }

    [Fact]
    public async Task Occupancy_of_a_ward_that_does_not_exist_is_a_404()
    {
        using var client = await ClientAsync(ApiApplication.AdministratorEmail);

        var response = await client.GetAsync($"/api/wards/{Guid.NewGuid()}/occupancy");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Occupancy_of_a_retired_ward_is_a_404_and_not_an_empty_ward()
    {
        using var client = await ClientAsync(ApiApplication.AdministratorEmail);
        var ward = await NewWardAsync(client, isActive: false);

        var response = await client.GetAsync($"/api/wards/{ward.Id}/occupancy");

        // Zero beds and zero patients would read as a real, empty ward. It is a closed one.
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ---------- the boundary itself ----------

    [Fact]
    public async Task Neither_read_lets_a_patient_identity_across_the_boundary()
    {
        using var client = await ClientAsync(ApiApplication.AdministratorEmail);
        var ward = await NewWardAsync(client);
        var beds = await AddBedsAsync(ward, 1);
        var name = $"Capacity Leak Check {Guid.NewGuid():N}"[..30];
        await OccupyAsync(beds[0], patientName: name);

        var summary = await client.GetStringAsync("/api/capacity/wards");
        var occupancy = await client.GetStringAsync($"/api/wards/{ward.Id}/occupancy");

        // Both endpoints are consumed by other components. "Counts only, no patient
        // identities" is the promise integration_of_functions.md 9 makes on our behalf, and a
        // name arriving in an aggregate is a section 16.1 failure, not a cosmetic one.
        Assert.DoesNotContain(name, summary, StringComparison.Ordinal);
        Assert.DoesNotContain(name, occupancy, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Any_staff_role_may_read_both_because_three_components_depend_on_them()
    {
        using var nurse = await ClientAsync(ApiApplication.NurseEmail);
        using var equipment = await ClientAsync(ApiApplication.EquipmentEmail);
        var ward = await NewWardAsync(await ClientAsync(ApiApplication.AdministratorEmail));

        Assert.Equal(HttpStatusCode.OK, (await nurse.GetAsync("/api/capacity/wards")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await equipment.GetAsync("/api/capacity/wards")).StatusCode);
        Assert.Equal(
            HttpStatusCode.OK,
            (await equipment.GetAsync($"/api/wards/{ward.Id}/occupancy")).StatusCode);
    }

    [Fact]
    public async Task Neither_read_is_open_without_a_token()
    {
        using var anonymous = _application.CreateClient();

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            (await anonymous.GetAsync("/api/capacity/wards")).StatusCode);
        Assert.Equal(
            HttpStatusCode.Unauthorized,
            (await anonymous.GetAsync($"/api/wards/{Guid.NewGuid()}/occupancy")).StatusCode);
    }

    // ---------- helpers ----------

    private sealed record TestWard(string Id, string Name);

    private static async Task<TestWard> NewWardAsync(
        HttpClient administrator,
        string wardType = "general",
        string genderPolicy = "mixed",
        bool isActive = true)
    {
        var name = $"Cap-Ward-{Guid.NewGuid():N}"[..24];

        var created = await administrator.PostAsJsonAsync("/api/wards", new
        {
            name,
            ward_type = wardType,
            gender_policy = genderPolicy,
            is_active = isActive
        });

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        using var body = await ReadJsonAsync(created);
        return new TestWard(body.RootElement.GetProperty("id").GetString()!, name);
    }

    /// <summary>Registers real beds through Equipment Management's own endpoint. Their table, their write.</summary>
    private async Task<IReadOnlyList<Guid>> AddBedsAsync(TestWard ward, int count)
    {
        using var equipment = await ClientAsync(ApiApplication.EquipmentEmail);
        var ids = new List<Guid>();

        for (var number = 1; number <= count; number++)
        {
            var created = await equipment.PostAsJsonAsync("/api/beds", new
            {
                ward_id = ward.Id,
                bed_number = $"B{number}",
                has_isolation = false,
                nurse_station_distance = number
            });

            Assert.Equal(HttpStatusCode.Created, created.StatusCode);

            using var body = await ReadJsonAsync(created);
            ids.Add(Guid.Parse(body.RootElement.GetProperty("id").GetString()!));
        }

        return ids;
    }

    /// <summary>
    /// Withdraws a bed by writing the column, because PATCH /api/beds/{id} cannot do it yet.
    /// </summary>
    /// <remarks>
    /// Equipment asks Patient Management whether a bed is occupied before withdrawing it, and
    /// that answer is still <c>StubBedOccupancyPort</c> — STUBS.md row 3 — which always says
    /// occupied and fails safe. So every withdrawal through the API is refused with
    /// <c>cl_equ_003</c> until GET /beds/{id}/occupancy is real. The column is what the real
    /// PATCH would set, so these tests keep passing when it lands.
    /// </remarks>
    private async Task SetConditionAsync(Guid bedId, BedCondition condition)
    {
        using var scope = _application.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CareLankaDbContext>();

        var bed = await db.Beds.FirstAsync(b => b.Id == bedId);
        bed.Condition = condition;

        await db.SaveChangesAsync();
    }

    private Task OccupyAsync(
        Guid bedId,
        AdmissionCategory category = AdmissionCategory.Inpatient,
        string? patientName = null)
        => AssignAsync(bedId, AssignmentStatus.Occupied, null, AdmissionStatus.Admitted, category,
            expectedArrival: null, patientName: patientName);

    private Task HoldAsync(
        Guid bedId,
        TimeSpan expiresIn,
        TimeSpan? expectedArrival = null,
        AdmissionCategory category = AdmissionCategory.Inpatient)
        => AssignAsync(bedId, AssignmentStatus.Reserved, DateTimeOffset.UtcNow + expiresIn,
            AdmissionStatus.BedReserved, category, expectedArrival);

    /// <summary>
    /// Puts a real patient and a real admission behind a bed, then writes the assignment row
    /// straight to the database.
    /// </summary>
    /// <remarks>
    /// Nothing writes a BedAssignment yet — that is step 6 — and waiting for it would leave
    /// both of these endpoints untested until then, while two people are blocked on them
    /// today. The rows are exactly what step 6 will write, so these tests should keep passing
    /// when it lands and this helper can be deleted.
    /// </remarks>
    private async Task AssignAsync(
        Guid bedId,
        AssignmentStatus status,
        DateTimeOffset? reservedUntil,
        AdmissionStatus admissionStatus,
        AdmissionCategory category = AdmissionCategory.Inpatient,
        TimeSpan? expectedArrival = null,
        string? patientName = null)
    {
        using var nurse = await ClientAsync(ApiApplication.NurseEmail);
        var patientId = await NewPatientAsync(nurse, patientName ?? $"Cap Patient {Guid.NewGuid():N}"[..28]);
        var admissionId = await NewAdmissionAsync(nurse, patientId, category);

        using var scope = _application.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CareLankaDbContext>();

        var admission = await db.Admissions.FirstAsync(a => a.Id == admissionId);
        admission.Status = admissionStatus;
        admission.ExpectedArrivalAt = expectedArrival is { } offset
            ? DateTimeOffset.UtcNow + offset
            : null;

        db.Add(new BedAssignment
        {
            Id = Guid.NewGuid(),
            AdmissionId = admissionId,
            BedId = bedId,
            Status = status,
            ReservedUntil = reservedUntil,
            AssignedBy = AssignedBy.User
        });

        await db.SaveChangesAsync();
    }

    private static async Task<string> NewPatientAsync(HttpClient nurse, string fullName)
    {
        var created = await nurse.PostAsJsonAsync("/api/patients", new
        {
            full_name = fullName,
            gender = "female",
            nic = $"C{Guid.NewGuid():N}"[..12]
        });

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        using var body = await ReadJsonAsync(created);
        return body.RootElement.GetProperty("id").GetString()!;
    }

    private async Task<Guid> NewAdmissionAsync(
        HttpClient nurse, string patientId, AdmissionCategory category)
    {
        var created = await nurse.PostAsJsonAsync("/api/admissions", new
        {
            patient_id = patientId,
            source = "walk_in",
            admission_category = Wire(category),
            category_set_by_staff_id = await NurseIdAsync(),
            urgency = "routine",
            is_infectious = false
        });

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        using var body = await ReadJsonAsync(created);
        return Guid.Parse(body.RootElement.GetProperty("id").GetString()!);
    }

    private static string Wire(AdmissionCategory category) => category switch
    {
        AdmissionCategory.Icu => "icu",
        AdmissionCategory.Hdu => "hdu",
        AdmissionCategory.Inpatient => "inpatient",
        AdmissionCategory.DayCase => "day_case",
        _ => "outpatient"
    };

    /// <summary>This ward's row in the capacity summary. The list is every ward the collection ever made.</summary>
    private static async Task<JsonElement> CapacityRowAsync(HttpClient client, TestWard ward)
    {
        var response = await client.GetAsync("/api/capacity/wards");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var body = await ReadJsonAsync(response);

        // Cloned: the JsonDocument is disposed at the end of this method and every JsonElement
        // taken from it dies with it, which reads as an ObjectDisposedException in the caller.
        return body.RootElement.GetProperty("wards").EnumerateArray()
            .Single(row => row.GetProperty("ward_id").GetString() == ward.Id)
            .Clone();
    }

    // One token per account for the whole class, and one lookup of the nurse's own id.
    // /api/auth/login is rate limited per IP and every test class shares that budget.
    private static readonly SemaphoreSlim TokenLock = new(1, 1);
    private static readonly Dictionary<string, string> Tokens = new();
    private static string? _nurseId;

    private async Task<string> NurseIdAsync()
    {
        if (_nurseId is not null)
        {
            return _nurseId;
        }

        using var client = await ClientAsync(ApiApplication.NurseEmail);
        using var body = await ReadJsonAsync(await client.GetAsync("/api/auth/me"));

        return _nurseId = body.RootElement.GetProperty("id").GetString()!;
    }

    private async Task<HttpClient> ClientAsync(string email)
    {
        var client = _application.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", await TokenAsync(email));

        return client;
    }

    private async Task<string> TokenAsync(string email)
    {
        await TokenLock.WaitAsync();

        try
        {
            if (Tokens.TryGetValue(email, out var cached))
            {
                return cached;
            }

            using var client = _application.CreateClient();
            var login = await client.PostAsJsonAsync(
                "/api/auth/login", new { email, password = ApiApplication.Password });

            Assert.Equal(HttpStatusCode.OK, login.StatusCode);

            using var body = await ReadJsonAsync(login);
            var token = body.RootElement.GetProperty("access_token").GetString()!;
            Tokens[email] = token;

            return token;
        }
        finally
        {
            TokenLock.Release();
        }
    }

    private static async Task<JsonDocument> ReadJsonAsync(HttpResponseMessage response)
        => JsonDocument.Parse(await response.Content.ReadAsStringAsync());
}
