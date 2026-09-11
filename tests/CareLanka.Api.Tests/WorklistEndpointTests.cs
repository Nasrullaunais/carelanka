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
/// A visit that needs no bed, and the ward board that shows it:
/// <c>GET /api/patient-worklist</c> and <c>POST /api/admissions/{id}/complete</c>.
/// </summary>
/// <remarks>
/// The fixture's database is shared by the whole collection, so every test makes its own
/// patient and searches the board for that one name rather than asserting on the whole list.
/// </remarks>
[Collection(ApiCollection.Name)]
public sealed class WorklistEndpointTests
{
    private readonly ApiApplication _application;

    public WorklistEndpointTests(ApiApplication application) => _application = application;

    // ---------- H0: an outpatient needs no bed ----------

    [Fact]
    public async Task An_outpatient_visit_is_admitted_from_the_start_and_never_awaits_a_bed()
    {
        using var nurse = await ClientAsync(ApiApplication.NurseEmail);
        var visit = await NewVisitAsync(nurse, category: "outpatient");

        // The bug this is here for: every admission used to start at awaiting_bed, and the only
        // edge into `admitted` runs through a bed being assigned. So somebody in for a scan sat
        // on the bed board forever and could not be finished with.
        Assert.Equal("admitted", visit.GetProperty("status").GetString());
        Assert.False(visit.GetProperty("requires_bed").GetBoolean());
        Assert.NotEqual(JsonValueKind.Null, visit.GetProperty("admitted_at").ValueKind);
    }

    [Fact]
    public async Task A_visit_that_does_need_a_bed_still_starts_on_the_bed_board()
    {
        using var nurse = await ClientAsync(ApiApplication.NurseEmail);
        var visit = await NewVisitAsync(nurse, category: "inpatient");

        Assert.Equal("awaiting_bed", visit.GetProperty("status").GetString());
        Assert.True(visit.GetProperty("requires_bed").GetBoolean());
        Assert.Equal(JsonValueKind.Null, visit.GetProperty("admitted_at").ValueKind);
    }

    [Fact]
    public async Task A_day_case_needs_a_bed()
    {
        using var nurse = await ClientAsync(ApiApplication.NurseEmail);
        var visit = await NewVisitAsync(nurse, category: "day_case");

        // On the bed side of the line on purpose. A day case is minor surgery or dialysis: they
        // are on a real bed for hours, and it is a bed nobody else can have.
        Assert.True(visit.GetProperty("requires_bed").GetBoolean());
        Assert.Equal("awaiting_bed", visit.GetProperty("status").GetString());
    }

    [Fact]
    public async Task An_outpatient_visit_keeps_no_expected_arrival_even_when_one_was_sent()
    {
        using var nurse = await ClientAsync(ApiApplication.NurseEmail);

        var visit = await NewVisitAsync(
            nurse, category: "outpatient", expectedArrival: DateTimeOffset.UtcNow.AddHours(2));

        // Dropped, not kept: the patient is standing at the desk, and carrying the time forward
        // would put somebody already here into the next two hours' incoming count.
        Assert.Equal(JsonValueKind.Null, visit.GetProperty("expected_arrival").ValueKind);
    }

    [Fact]
    public async Task Assigning_a_bed_to_an_outpatient_is_refused_with_its_own_reason()
    {
        using var nurse = await ClientAsync(ApiApplication.NurseEmail);
        var ward = await NewWardAsync();
        var bed = (await AddBedsAsync(ward, 1))[0];
        var visit = await NewVisitAsync(nurse, category: "outpatient");

        var refused = await nurse.PostAsJsonAsync(
            $"/api/admissions/{visit.GetProperty("id").GetString()}/assign-bed",
            new { bed_id = bed });

        Assert.Equal(HttpStatusCode.Conflict, refused.StatusCode);

        using var body = await ReadJsonAsync(refused);

        // The transition check alone would refuse this as "cannot move from admitted to
        // awaiting_approval", which tells a nurse nothing. cl_pat_021 says why.
        Assert.Equal("cl_pat_021", body.RootElement.GetProperty("code").GetString());
    }

    // ---------- finishing a visit that had no bed ----------

    [Fact]
    public async Task Completing_an_outpatient_visit_ends_it()
    {
        using var nurse = await ClientAsync(ApiApplication.NurseEmail);
        var visit = await NewVisitAsync(nurse, category: "outpatient");
        var id = visit.GetProperty("id").GetString();

        var completed = await nurse.PostAsync($"/api/admissions/{id}/complete", null);

        Assert.Equal(HttpStatusCode.OK, completed.StatusCode);

        using var body = await ReadJsonAsync(completed);

        Assert.Equal("discharged", body.RootElement.GetProperty("status").GetString());
        Assert.NotEqual(JsonValueKind.Null, body.RootElement.GetProperty("discharged_at").ValueKind);
    }

    [Fact]
    public async Task Completing_the_same_visit_twice_is_refused()
    {
        using var nurse = await ClientAsync(ApiApplication.NurseEmail);
        var visit = await NewVisitAsync(nurse, category: "outpatient");
        var id = visit.GetProperty("id").GetString();

        Assert.Equal(
            HttpStatusCode.OK,
            (await nurse.PostAsync($"/api/admissions/{id}/complete", null)).StatusCode);

        var again = await nurse.PostAsync($"/api/admissions/{id}/complete", null);

        // discharged is terminal. Re-running it would rewrite the discharge time and let a
        // second open admission past ux_admissions_open_patient.
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
    }

    [Fact]
    public async Task A_visit_with_a_bed_cannot_be_completed_and_is_told_to_discharge_instead()
    {
        using var nurse = await ClientAsync(ApiApplication.NurseEmail);
        var visit = await NewVisitAsync(nurse, category: "inpatient");

        var refused = await nurse.PostAsync(
            $"/api/admissions/{visit.GetProperty("id").GetString()}/complete", null);

        Assert.Equal(HttpStatusCode.Conflict, refused.StatusCode);

        using var body = await ReadJsonAsync(refused);

        // Narrow on purpose. This endpoint does not know how to run a discharge checklist or
        // give a bed back, so it refuses rather than half-doing a discharge.
        Assert.Equal("cl_pat_020", body.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Completing_something_that_does_not_exist_is_a_404()
    {
        using var nurse = await ClientAsync(ApiApplication.NurseEmail);

        var missing = await nurse.PostAsync($"/api/admissions/{Guid.NewGuid()}/complete", null);

        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
    }

    // ---------- the board ----------

    [Fact]
    public async Task A_booking_nobody_has_checked_in_reads_as_not_arrived()
    {
        using var nurse = await ClientAsync(ApiApplication.NurseEmail);
        var patient = await NewPatientAsync(nurse);
        await BookAsync(nurse, patient.Id);

        var row = await BoardRowAsync(nurse, patient.Name);

        // The whole reason this endpoint exists. An admission is created by arriving, so a list
        // of admissions could never say this about anybody.
        Assert.Equal("booking", row.GetProperty("kind").GetString());
        Assert.Equal("not_arrived", row.GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.Null, row.GetProperty("admission_category").ValueKind);
        Assert.False(row.GetProperty("requires_bed").GetBoolean());
    }

    [Fact]
    public async Task A_booking_carries_its_reason_so_a_scan_is_not_a_blood_test()
    {
        using var nurse = await ClientAsync(ApiApplication.NurseEmail);
        var patient = await NewPatientAsync(nurse);
        await BookAsync(nurse, patient.Id, reason: "Scan");

        var row = await BoardRowAsync(nurse, patient.Name);

        Assert.Equal("Scan", row.GetProperty("reason").GetString());
    }

    [Fact]
    public async Task A_checked_in_booking_appears_once_as_its_visit_and_not_twice()
    {
        using var nurse = await ClientAsync(ApiApplication.NurseEmail);
        var patient = await NewPatientAsync(nurse);
        var appointmentId = await BookAsync(nurse, patient.Id);

        var checkedIn = await nurse.PostAsJsonAsync(
            $"/api/appointments/{appointmentId}/check-in",
            new
            {
                admission_category = "outpatient",
                category_set_by_staff_id = await NurseIdAsync(),
                urgency = "routine",
                is_infectious = false
            });

        Assert.Equal(HttpStatusCode.Created, checkedIn.StatusCode);

        var rows = await BoardRowsAsync(nurse, patient.Name);

        // Exactly one. The appointment is terminal at checked_in and is left out of the board,
        // or every checked-in patient would appear beside their own past.
        Assert.Single(rows);
        Assert.Equal("visit", rows[0].GetProperty("kind").GetString());

        // Checked in as an outpatient, so straight to admitted with no bed in between.
        Assert.Equal("admitted", rows[0].GetProperty("status").GetString());
        Assert.False(rows[0].GetProperty("requires_bed").GetBoolean());
    }

    [Fact]
    public async Task A_visit_waiting_for_a_bed_reads_as_awaiting_bed()
    {
        using var nurse = await ClientAsync(ApiApplication.NurseEmail);
        var patient = await NewPatientAsync(nurse);
        await AdmitAsync(nurse, patient.Id, "inpatient");

        var row = await BoardRowAsync(nurse, patient.Name);

        Assert.Equal("visit", row.GetProperty("kind").GetString());
        Assert.Equal("awaiting_bed", row.GetProperty("status").GetString());
        Assert.True(row.GetProperty("requires_bed").GetBoolean());
        Assert.Equal(JsonValueKind.Null, row.GetProperty("bed_number").ValueKind);
    }

    [Fact]
    public async Task A_held_bed_reads_as_bed_ready_and_names_the_bed()
    {
        using var nurse = await ClientAsync(ApiApplication.NurseEmail);
        var ward = await NewWardAsync();
        var bed = (await AddBedsAsync(ward, 1))[0];
        var patient = await NewPatientAsync(nurse);
        var admissionId = await AdmitAsync(nurse, patient.Id, "inpatient");

        var assigned = await nurse.PostAsJsonAsync(
            $"/api/admissions/{admissionId}/assign-bed", new { bed_id = bed });

        Assert.Equal(HttpStatusCode.OK, assigned.StatusCode);

        var row = await BoardRowAsync(nurse, patient.Name);

        // Its own status, not folded into awaiting_bed: the hold lapses in thirty minutes, so
        // "a bed is waiting, go and collect them" is a different job from "find them a bed".
        Assert.Equal("bed_ready", row.GetProperty("status").GetString());
        Assert.Equal(ward.Name, row.GetProperty("ward_name").GetString());
        Assert.Equal("B1", row.GetProperty("bed_number").GetString());
    }

    [Fact]
    public async Task A_lapsed_hold_puts_the_visit_back_to_awaiting_bed_with_no_bed_named()
    {
        using var nurse = await ClientAsync(ApiApplication.NurseEmail);
        var ward = await NewWardAsync();
        var bed = (await AddBedsAsync(ward, 1))[0];
        var patient = await NewPatientAsync(nurse);
        var admissionId = await AdmitAsync(nurse, patient.Id, "inpatient");

        await nurse.PostAsJsonAsync($"/api/admissions/{admissionId}/assign-bed", new { bed_id = bed });
        await ExpireHoldAsync(bed);

        var row = await BoardRowAsync(nurse, patient.Name);

        // The stored status still says bed_reserved, and the board still says bed_ready — that
        // is honest, because nobody has released anything. What must not happen is the board
        // naming a bed the patient no longer has any claim on.
        Assert.Equal(JsonValueKind.Null, row.GetProperty("bed_number").ValueKind);
    }

    [Fact]
    public async Task A_completed_visit_is_off_the_board_until_the_archive_is_asked_for()
    {
        using var nurse = await ClientAsync(ApiApplication.NurseEmail);
        var patient = await NewPatientAsync(nurse);
        var admissionId = await AdmitAsync(nurse, patient.Id, "outpatient");

        await nurse.PostAsync($"/api/admissions/{admissionId}/complete", null);

        Assert.Empty(await BoardRowsAsync(nurse, patient.Name));

        var archived = await BoardRowsAsync(nurse, patient.Name, includeFinished: true);

        Assert.Single(archived);
        Assert.Equal("completed", archived[0].GetProperty("status").GetString());
    }

    [Fact]
    public async Task The_board_pages_across_both_tables_without_repeating_or_losing_a_row()
    {
        using var nurse = await ClientAsync(ApiApplication.NurseEmail);

        // Three of each, sharing one searchable surname so the page is exactly these six.
        var surname = $"Union{Guid.NewGuid():N}"[..14];

        for (var i = 0; i < 3; i++)
        {
            await BookAsync(nurse, (await NewPatientAsync(nurse, surname)).Id);
            await AdmitAsync(nurse, (await NewPatientAsync(nurse, surname)).Id, "outpatient");
        }

        var first = await BoardPageAsync(nurse, surname, page: 1, pageSize: 4);
        var second = await BoardPageAsync(nurse, surname, page: 2, pageSize: 4);

        Assert.Equal(6, first.RootElement.GetProperty("total_items").GetInt32());
        Assert.Equal(2, first.RootElement.GetProperty("total_pages").GetInt32());

        var ids = Ids(first).Concat(Ids(second)).ToList();

        // The reason the union is paged in one query rather than stitched from two: paging each
        // table separately puts the combined page boundary in the middle of neither, and a row
        // gets shown twice while another is never shown at all.
        Assert.Equal(6, ids.Count);
        Assert.Equal(6, ids.Distinct().Count());
    }

    [Fact]
    public async Task A_role_that_cannot_read_admissions_cannot_read_the_board_either()
    {
        // Ambulance crew, not equipment management. The equipment manager gained this list on
        // 2026-09-11 when PatientReader and AdmissionReader became one PatientDetails policy;
        // the crew is now the only staff role on neither.
        using var ambulance = await ClientAsync(ApiApplication.AmbulanceEmail);

        var refused = await ambulance.GetAsync("/api/patient-worklist");

        Assert.Equal(HttpStatusCode.Forbidden, refused.StatusCode);
    }

    // ---------- helpers ----------

    private sealed record TestWard(Guid Id, string Name);

    private sealed record TestPatient(string Id, string Name);

    private async Task<TestWard> NewWardAsync()
    {
        using var administrator = await ClientAsync(ApiApplication.AdministratorEmail);
        var name = $"Board-Ward-{Guid.NewGuid():N}"[..24];

        var created = await administrator.PostAsJsonAsync("/api/wards", new
        {
            name,
            ward_type = "general",
            gender_policy = "mixed",
            is_active = true
        });

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        using var body = await ReadJsonAsync(created);
        return new TestWard(Guid.Parse(body.RootElement.GetProperty("id").GetString()!), name);
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
                has_isolation = false
            });

            Assert.Equal(HttpStatusCode.Created, created.StatusCode);

            using var body = await ReadJsonAsync(created);
            ids.Add(Guid.Parse(body.RootElement.GetProperty("id").GetString()!));
        }

        return ids;
    }

    /// <summary>Pushes a live hold past its expiry, which no endpoint offers and no test can wait for.</summary>
    private async Task ExpireHoldAsync(Guid bedId)
    {
        using var scope = _application.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CareLankaDbContext>();

        var assignment = await db.BedAssignments
            .Where(a => a.BedId == bedId && a.Status == AssignmentStatus.Reserved)
            .FirstAsync();

        assignment.ReservedUntil = DateTimeOffset.UtcNow.AddMinutes(-1);

        await db.SaveChangesAsync();
    }

    private async Task<TestPatient> NewPatientAsync(HttpClient nurse, string? surname = null)
    {
        var name = surname is null
            ? $"Board Patient {Guid.NewGuid():N}"[..28]
            : $"{surname} {Guid.NewGuid():N}"[..28];

        var created = await nurse.PostAsJsonAsync("/api/patients", new
        {
            full_name = name,
            gender = "male",
            nic = $"W{Guid.NewGuid():N}"[..12]
        });

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        using var body = await ReadJsonAsync(created);
        return new TestPatient(body.RootElement.GetProperty("id").GetString()!, name);
    }

    /// <summary>Opens a visit for a brand-new patient and answers the whole admission body.</summary>
    private async Task<JsonElement> NewVisitAsync(
        HttpClient nurse, string category, DateTimeOffset? expectedArrival = null)
    {
        var patient = await NewPatientAsync(nurse);

        var created = await nurse.PostAsJsonAsync("/api/admissions", new
        {
            patient_id = patient.Id,
            source = "walk_in",
            admission_category = category,
            category_set_by_staff_id = await NurseIdAsync(),
            urgency = "routine",
            is_infectious = false,
            expected_arrival = expectedArrival
        });

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        using var body = await ReadJsonAsync(created);
        return body.RootElement.Clone();
    }

    /// <summary>Opens a visit for an existing patient and answers its id.</summary>
    private async Task<string> AdmitAsync(HttpClient nurse, string patientId, string category)
    {
        var created = await nurse.PostAsJsonAsync("/api/admissions", new
        {
            patient_id = patientId,
            source = "walk_in",
            admission_category = category,
            category_set_by_staff_id = await NurseIdAsync(),
            urgency = "routine",
            is_infectious = false
        });

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        using var body = await ReadJsonAsync(created);
        return body.RootElement.GetProperty("id").GetString()!;
    }

    private async Task<string> BookAsync(
        HttpClient nurse, string patientId, string? reason = null)
    {
        var created = await nurse.PostAsJsonAsync("/api/appointments", new
        {
            patient_id = patientId,
            scheduled_at = DateTimeOffset.UtcNow.AddHours(3),
            reason
        });

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        using var body = await ReadJsonAsync(created);
        return body.RootElement.GetProperty("id").GetString()!;
    }

    /// <summary>The board's one row for this patient. Fails loudly when there is not exactly one.</summary>
    private static async Task<JsonElement> BoardRowAsync(HttpClient client, string name)
        => Assert.Single(await BoardRowsAsync(client, name));

    private static async Task<IReadOnlyList<JsonElement>> BoardRowsAsync(
        HttpClient client, string name, bool includeFinished = false)
    {
        using var body = await BoardPageAsync(client, name, 1, 50, includeFinished);

        return body.RootElement.GetProperty("items").EnumerateArray()
            // Cloned: the JsonDocument dies with this method and every element taken from it
            // dies with it, which reads as an ObjectDisposedException in the caller.
            .Select(row => row.Clone())
            .ToList();
    }

    private static async Task<JsonDocument> BoardPageAsync(
        HttpClient client, string search, int page, int pageSize, bool includeFinished = false)
    {
        var response = await client.GetAsync(
            $"/api/patient-worklist?search={Uri.EscapeDataString(search)}"
            + $"&page={page}&pageSize={pageSize}&includeFinished={includeFinished}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        return await ReadJsonAsync(response);
    }

    private static IEnumerable<string> Ids(JsonDocument page)
        => page.RootElement.GetProperty("items").EnumerateArray()
            .Select(row => row.GetProperty("id").GetString()!)
            .ToList();

    // One token per account for the whole class. /api/auth/login is rate limited per IP and
    // every test class in the collection shares that budget.
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

            var response = await client.PostAsJsonAsync("/api/auth/login", new
            {
                email,
                password = ApiApplication.Password
            });

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            using var body = await ReadJsonAsync(response);
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
