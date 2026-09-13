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
public sealed class AppointmentEndpointTests
{
    private readonly ApiApplication _application;

    public AppointmentEndpointTests(ApiApplication application) => _application = application;

    [Fact]
    public async Task A_nurse_books_a_visit_and_it_lands_on_the_expected_visits_worklist()
    {
        using var nurse = await ClientAsync(ApiApplication.NurseEmail);
        var patientId = await NewPatientAsync(nurse, "Booking Lands On List");
        var when = SoonUtc();

        var created = await BookAsync(nurse, patientId, when, "Follow-up on a fracture");
        using var body = await ReadJsonAsync(created);

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.Equal("scheduled", body.RootElement.GetProperty("status").GetString());
        Assert.Equal("Follow-up on a fracture", body.RootElement.GetProperty("reason").GetString());
        Assert.Equal(
            patientId,
            body.RootElement.GetProperty("patient").GetProperty("id").GetString());

        Assert.Equal(JsonValueKind.Null, body.RootElement.GetProperty("admission_id").ValueKind);

        var id = body.RootElement.GetProperty("id").GetString()!;
        using var listed = await ReadJsonAsync(await nurse.GetAsync("/api/appointments?pageSize=100"));

        Assert.Contains(
            listed.RootElement.GetProperty("items").EnumerateArray(),
            item => item.GetProperty("id").GetString() == id);
    }

    [Fact]
    public async Task Who_took_the_booking_comes_off_the_token_and_not_off_the_body()
    {
        using var nurse = await ClientAsync(ApiApplication.NurseEmail);
        var patientId = await NewPatientAsync(nurse, "Booked By Comes From Token");

        var created = await nurse.PostAsJsonAsync("/api/appointments", new
        {
            patient_id = patientId,
            scheduled_at = SoonUtc(),
            booked_by_staff_id = Guid.NewGuid()
        });

        using var body = await ReadJsonAsync(created);

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.Equal(await NurseIdAsync(), body.RootElement.GetProperty("booked_by_staff_id").GetString());
    }

    [Fact]
    public async Task A_visit_booked_for_a_time_that_has_passed_is_refused()
    {
        using var nurse = await ClientAsync(ApiApplication.NurseEmail);
        var patientId = await NewPatientAsync(nurse, "Booking In The Past");

        var created = await BookAsync(nurse, patientId, DateTimeOffset.UtcNow.AddHours(-1));
        using var body = await ReadJsonAsync(created);

        Assert.Equal(HttpStatusCode.BadRequest, created.StatusCode);
        Assert.Equal("cl_pat_010", body.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task A_booking_with_no_time_on_it_is_refused_rather_than_booked_for_the_year_1()
    {
        using var nurse = await ClientAsync(ApiApplication.NurseEmail);
        var patientId = await NewPatientAsync(nurse, "Booking With No Time");

        var created = await nurse.PostAsJsonAsync("/api/appointments", new { patient_id = patientId });

        Assert.Equal(HttpStatusCode.BadRequest, created.StatusCode);
        Assert.Equal("application/problem+json", created.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task One_open_booking_at_a_time()
    {
        using var nurse = await ClientAsync(ApiApplication.NurseEmail);
        var patientId = await NewPatientAsync(nurse, "Only One Open Booking");

        await BookAsync(nurse, patientId, SoonUtc());
        var second = await BookAsync(nurse, patientId, SoonUtc());
        using var body = await ReadJsonAsync(second);

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        Assert.Equal("cl_pat_009", body.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task A_patient_who_is_already_in_the_building_cannot_be_booked_a_visit()
    {
        using var nurse = await ClientAsync(ApiApplication.NurseEmail);
        var patientId = await NewPatientAsync(nurse, "Already Admitted No Booking");
        await NewAdmissionAsync(nurse, patientId);

        var created = await BookAsync(nurse, patientId, SoonUtc());
        using var body = await ReadJsonAsync(created);

        Assert.Equal(HttpStatusCode.Conflict, created.StatusCode);
        Assert.Equal("cl_pat_006", body.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Booking_for_a_patient_who_does_not_exist_is_a_404()
    {
        using var nurse = await ClientAsync(ApiApplication.NurseEmail);

        var created = await BookAsync(nurse, Guid.NewGuid().ToString(), SoonUtc());

        Assert.Equal(HttpStatusCode.NotFound, created.StatusCode);
    }

    [Fact]
    public async Task A_booking_freed_by_checking_in_lets_the_patient_book_again_later()
    {
        using var nurse = await ClientAsync(ApiApplication.NurseEmail);
        var patientId = await NewPatientAsync(nurse, "Books Again After Discharge");

        var appointmentId = await BookIdAsync(nurse, patientId, SoonUtc());
        await CheckInAsync(nurse, appointmentId);
        await DischargeAsync(patientId);

        var again = await BookAsync(nurse, patientId, SoonUtc());

        Assert.Equal(HttpStatusCode.Created, again.StatusCode);
    }

    [Fact]
    public async Task The_worklist_reads_down_in_time_order_because_that_is_how_a_desk_reads_it()
    {
        using var nurse = await ClientAsync(ApiApplication.NurseEmail);
        var day = DateTimeOffset.UtcNow.AddDays(Random.Shared.Next(400, 4000)).Date;

        await BookAsync(nurse, await NewPatientAsync(nurse, "Third In The Day"), At(day, 15));
        await BookAsync(nurse, await NewPatientAsync(nurse, "First In The Day"), At(day, 8));
        await BookAsync(nurse, await NewPatientAsync(nurse, "Second In The Day"), At(day, 11));

        using var body = await ReadJsonAsync(
            await nurse.GetAsync($"/api/appointments?date={day:yyyy-MM-dd}"));

        Assert.Equal(
            new[] { "First In The Day", "Second In The Day", "Third In The Day" },
            body.RootElement.GetProperty("items").EnumerateArray()
                .Select(item => item.GetProperty("patient").GetProperty("full_name").GetString())
                .ToArray());
    }

    [Fact]
    public async Task Filtering_by_date_leaves_out_the_day_either_side()
    {
        using var nurse = await ClientAsync(ApiApplication.NurseEmail);
        var day = DateTimeOffset.UtcNow.AddDays(Random.Shared.Next(400, 4000)).Date;

        var wanted = await BookIdAsync(nurse, await NewPatientAsync(nurse, "On The Day"), At(day, 12));
        var tomorrow = await BookIdAsync(
            nurse, await NewPatientAsync(nurse, "The Next Day"), At(day.AddDays(1), 12));

        using var body = await ReadJsonAsync(
            await nurse.GetAsync($"/api/appointments?date={day:yyyy-MM-dd}"));
        var ids = Ids(body);

        Assert.Contains(wanted, ids);
        Assert.DoesNotContain(tomorrow, ids);
    }

    [Fact]
    public async Task Filtering_by_status_leaves_out_the_bookings_already_dealt_with()
    {
        using var nurse = await ClientAsync(ApiApplication.NurseEmail);
        var day = DateTimeOffset.UtcNow.AddDays(Random.Shared.Next(400, 4000)).Date;

        var stillBooked = await BookIdAsync(
            nurse, await NewPatientAsync(nurse, "Still Expected"), At(day, 9));
        var arrived = await BookIdAsync(
            nurse, await NewPatientAsync(nurse, "Already Arrived"), At(day, 10));
        await CheckInAsync(nurse, arrived);

        using var body = await ReadJsonAsync(
            await nurse.GetAsync($"/api/appointments?date={day:yyyy-MM-dd}&status=scheduled"));
        var ids = Ids(body);

        Assert.Contains(stillBooked, ids);
        Assert.DoesNotContain(arrived, ids);
    }

    [Fact]
    public async Task An_unknown_status_is_a_400_and_not_a_silently_ignored_filter()
    {
        using var nurse = await ClientAsync(ApiApplication.NurseEmail);

        var response = await nurse.GetAsync("/api/appointments?status=turned_up");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(
            HttpStatusCode.OK,
            (await nurse.GetAsync("/api/appointments?status=checked_in")).StatusCode);
    }

    [Fact]
    public async Task The_worklist_pages()
    {
        using var nurse = await ClientAsync(ApiApplication.NurseEmail);
        var day = DateTimeOffset.UtcNow.AddDays(Random.Shared.Next(400, 4000)).Date;

        for (var hour = 8; hour < 11; hour++)
        {
            await BookAsync(nurse, await NewPatientAsync(nurse, $"Paged Booking {hour}"), At(day, hour));
        }

        using var body = await ReadJsonAsync(
            await nurse.GetAsync($"/api/appointments?date={day:yyyy-MM-dd}&page=2&pageSize=2"));

        Assert.Equal(3, body.RootElement.GetProperty("total_items").GetInt32());
        Assert.Equal(2, body.RootElement.GetProperty("total_pages").GetInt32());
        Assert.Single(body.RootElement.GetProperty("items").EnumerateArray());
    }

    [Fact]
    public async Task Checking_in_creates_an_admission_that_says_the_patient_pre_registered()
    {
        using var nurse = await ClientAsync(ApiApplication.NurseEmail);
        var patientId = await NewPatientAsync(nurse, "Checks In As Pre Registered");
        var appointmentId = await BookIdAsync(nurse, patientId, SoonUtc());

        var response = await CheckInAsync(nurse, appointmentId);
        using var admission = await ReadJsonAsync(response);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        Assert.Equal("pre_registered", admission.RootElement.GetProperty("source").GetString());

        Assert.Equal("awaiting_bed", admission.RootElement.GetProperty("status").GetString());
        Assert.Equal("inpatient", admission.RootElement.GetProperty("admission_category").GetString());
        Assert.Equal(
            patientId,
            admission.RootElement.GetProperty("patient").GetProperty("id").GetString());

        Assert.Equal(
            await NurseIdAsync(),
            admission.RootElement.GetProperty("category_set_by_staff_id").GetString());
    }

    [Fact]
    public async Task Checking_in_closes_the_booking_and_links_it_to_the_admission_it_became()
    {
        using var nurse = await ClientAsync(ApiApplication.NurseEmail);
        var patientId = await NewPatientAsync(nurse, "Booking Links To Admission");
        var appointmentId = await BookIdAsync(nurse, patientId, SoonUtc());

        using var admission = await ReadJsonAsync(await CheckInAsync(nurse, appointmentId));
        var admissionId = admission.RootElement.GetProperty("id").GetString();

        using var listed = await ReadJsonAsync(
            await nurse.GetAsync("/api/appointments?status=checked_in&pageSize=100"));
        var booking = listed.RootElement.GetProperty("items").EnumerateArray()
            .Single(item => item.GetProperty("id").GetString() == appointmentId);

        Assert.Equal("checked_in", booking.GetProperty("status").GetString());
        Assert.Equal(admissionId, booking.GetProperty("admission_id").GetString());
    }

    [Fact]
    public async Task Checking_the_same_person_in_twice_is_refused_on_the_booking_not_on_the_admission()
    {
        using var nurse = await ClientAsync(ApiApplication.NurseEmail);
        var patientId = await NewPatientAsync(nurse, "Checked In Twice");
        var appointmentId = await BookIdAsync(nurse, patientId, SoonUtc());

        await CheckInAsync(nurse, appointmentId);
        var again = await CheckInAsync(nurse, appointmentId);
        using var body = await ReadJsonAsync(again);

        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
        Assert.Equal("cl_err_409_transition", body.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task A_nurse_may_not_check_somebody_in_at_icu_or_hdu_but_the_duty_manager_may()
    {
        using var nurse = await ClientAsync(ApiApplication.NurseEmail);
        using var manager = await ClientAsync(ApiApplication.ManagerEmail);

        var refusedFor = await BookIdAsync(
            nurse, await NewPatientAsync(nurse, "Nurse Tries ICU"), SoonUtc());
        var allowedFor = await BookIdAsync(
            nurse, await NewPatientAsync(nurse, "Manager Does ICU"), SoonUtc());

        var refused = await CheckInAsync(nurse, refusedFor, category: "icu");
        using var body = await ReadJsonAsync(refused);
        var allowed = await CheckInAsync(manager, allowedFor, category: "icu");

        Assert.Equal(HttpStatusCode.Forbidden, refused.StatusCode);
        Assert.Equal("cl_pat_011", body.RootElement.GetProperty("code").GetString());
        Assert.Equal(HttpStatusCode.Created, allowed.StatusCode);
    }

    [Fact]
    public async Task A_nurse_may_still_check_somebody_in_at_the_three_levels_that_are_theirs()
    {
        using var nurse = await ClientAsync(ApiApplication.NurseEmail);

        foreach (var category in new[] { "outpatient", "day_case", "inpatient" })
        {
            var appointmentId = await BookIdAsync(
                nurse, await NewPatientAsync(nurse, $"Nurse Checks In {category}"), SoonUtc());

            var response = await CheckInAsync(nurse, appointmentId, category: category);

            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        }
    }

    [Fact]
    public async Task A_check_in_with_no_care_level_is_refused_rather_than_filed_as_intensive_care()
    {
        using var nurse = await ClientAsync(ApiApplication.NurseEmail);
        var appointmentId = await BookIdAsync(
            nurse, await NewPatientAsync(nurse, "Check In With No Category"), SoonUtc());

        var response = await nurse.PostAsJsonAsync(
            $"/api/appointments/{appointmentId}/check-in",
            new { category_set_by_staff_id = await NurseIdAsync(), urgency = "routine" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task A_check_in_naming_a_clinician_who_does_not_exist_is_refused()
    {
        using var nurse = await ClientAsync(ApiApplication.NurseEmail);
        var appointmentId = await BookIdAsync(
            nurse, await NewPatientAsync(nurse, "Check In With Unknown Staff"), SoonUtc());

        var response = await CheckInAsync(nurse, appointmentId, staffId: Guid.NewGuid().ToString());
        using var body = await ReadJsonAsync(response);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("cl_pat_008", body.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Checking_in_an_appointment_that_does_not_exist_is_a_404()
    {
        using var nurse = await ClientAsync(ApiApplication.NurseEmail);

        var response = await CheckInAsync(nurse, Guid.NewGuid().ToString());

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task A_failed_check_in_leaves_the_booking_untouched_because_it_is_one_transaction()
    {
        using var nurse = await ClientAsync(ApiApplication.NurseEmail);
        var patientId = await NewPatientAsync(nurse, "Failed Check In Rolls Back");
        var appointmentId = await BookIdAsync(nurse, patientId, SoonUtc());

        await NewAdmissionAsync(nurse, patientId);

        var response = await CheckInAsync(nurse, appointmentId);
        using var body = await ReadJsonAsync(response);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("cl_pat_006", body.RootElement.GetProperty("code").GetString());

        using var listed = await ReadJsonAsync(
            await nurse.GetAsync("/api/appointments?status=scheduled&pageSize=100"));

        Assert.Contains(appointmentId, Ids(listed));
    }

    [Fact]
    public async Task The_desk_is_the_ward_nurse_and_the_duty_manager_and_nobody_else()
    {
        using var doctor = await ClientAsync(ApiApplication.DoctorEmail);
        using var administrator = await ClientAsync(ApiApplication.AdministratorEmail);
        using var manager = await ClientAsync(ApiApplication.ManagerEmail);

        Assert.Equal(
            HttpStatusCode.Forbidden,
            (await doctor.GetAsync("/api/appointments")).StatusCode);
        Assert.Equal(
            HttpStatusCode.Forbidden,
            (await administrator.GetAsync("/api/appointments")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await manager.GetAsync("/api/appointments")).StatusCode);
    }

    [Fact]
    public async Task None_of_the_three_is_open_without_a_token()
    {
        using var anonymous = _application.CreateClient();
        var id = Guid.NewGuid().ToString();

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            (await anonymous.GetAsync("/api/appointments")).StatusCode);
        Assert.Equal(
            HttpStatusCode.Unauthorized,
            (await BookAsync(anonymous, id, SoonUtc())).StatusCode);
        Assert.Equal(
            HttpStatusCode.Unauthorized,
            (await CheckInAsync(anonymous, id)).StatusCode);
    }

    private static DateTimeOffset SoonUtc()
        => DateTimeOffset.UtcNow.AddDays(Random.Shared.Next(1, 300)).AddHours(3);

    private static DateTimeOffset At(DateTime day, int hourUtc)
        => new(DateTime.SpecifyKind(day.Date.AddHours(hourUtc), DateTimeKind.Utc));

    private static Task<HttpResponseMessage> BookAsync(
        HttpClient client, string patientId, DateTimeOffset scheduledAt, string? reason = null)
        => client.PostAsJsonAsync("/api/appointments", new
        {
            patient_id = patientId,
            scheduled_at = scheduledAt,
            reason
        });

    private static async Task<string> BookIdAsync(
        HttpClient client, string patientId, DateTimeOffset scheduledAt)
    {
        var created = await BookAsync(client, patientId, scheduledAt);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        using var body = await ReadJsonAsync(created);
        return body.RootElement.GetProperty("id").GetString()!;
    }

    private async Task<HttpResponseMessage> CheckInAsync(
        HttpClient client,
        string appointmentId,
        string category = "inpatient",
        string? staffId = null)
        => await client.PostAsJsonAsync($"/api/appointments/{appointmentId}/check-in", new
        {
            admission_category = category,
            category_set_by_staff_id = staffId ?? await NurseIdAsync(),
            urgency = "routine",
            is_infectious = false
        });

    private static async Task<string> NewPatientAsync(HttpClient nurse, string fullName)
    {
        var created = await nurse.PostAsJsonAsync("/api/patients", new
        {
            full_name = fullName,
            gender = "male",
            nic = $"P{Guid.NewGuid():N}"[..12]
        });

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        using var body = await ReadJsonAsync(created);
        return body.RootElement.GetProperty("id").GetString()!;
    }

    private async Task NewAdmissionAsync(HttpClient nurse, string patientId)
    {
        var created = await nurse.PostAsJsonAsync("/api/admissions", new
        {
            patient_id = patientId,
            source = "walk_in",
            admission_category = "inpatient",
            category_set_by_staff_id = await NurseIdAsync(),
            urgency = "routine",
            is_infectious = false
        });

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
    }

    private async Task DischargeAsync(string patientId)
    {
        using var scope = _application.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CareLankaDbContext>();

        var id = Guid.Parse(patientId);
        var open = await db.Admissions
            .Where(a => a.PatientId == id
                && a.Status != AdmissionStatus.Discharged
                && a.Status != AdmissionStatus.Cancelled)
            .ToListAsync();

        foreach (var admission in open)
        {
            admission.Status = AdmissionStatus.Discharged;
            admission.DischargedAt = DateTimeOffset.UtcNow;
        }

        await db.SaveChangesAsync();
    }

    private static IReadOnlyList<string> Ids(JsonDocument page)
        => page.RootElement.GetProperty("items").EnumerateArray()
            .Select(item => item.GetProperty("id").GetString()!)
            .ToList();

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
