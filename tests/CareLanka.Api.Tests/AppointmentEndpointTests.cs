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

        var appointmentId = await ConfirmedIdAsync(nurse, patientId, SoonUtc());
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
        var arrived = await ConfirmedIdAsync(
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
            (await nurse.GetAsync("/api/appointments?status=confirmed")).StatusCode);
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
        var appointmentId = await ConfirmedIdAsync(nurse, patientId, SoonUtc());

        var response = await CheckInAsync(nurse, appointmentId);
        using var admission = await ReadJsonAsync(response);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        Assert.Equal("pre_registered", admission.RootElement.GetProperty("source").GetString());

        Assert.Equal("awaiting_bed", admission.RootElement.GetProperty("status").GetString());
        Assert.Equal("general", admission.RootElement.GetProperty("admission_category").GetString());
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
        var appointmentId = await ConfirmedIdAsync(nurse, patientId, SoonUtc());

        using var admission = await ReadJsonAsync(await CheckInAsync(nurse, appointmentId));
        var admissionId = admission.RootElement.GetProperty("id").GetString();

        using var listed = await ReadJsonAsync(
            await nurse.GetAsync("/api/appointments?status=completed&pageSize=100"));
        var booking = listed.RootElement.GetProperty("items").EnumerateArray()
            .Single(item => item.GetProperty("id").GetString() == appointmentId);

        // Both endings read as completed, so admission_id is the only thing that says the
        // patient stayed in rather than went home.
        Assert.Equal("completed", booking.GetProperty("status").GetString());
        Assert.Equal(admissionId, booking.GetProperty("admission_id").GetString());
        Assert.False(booking.GetProperty("can_complete").GetBoolean());
    }

    [Fact]
    public async Task Checking_the_same_person_in_twice_is_refused_on_the_booking_not_on_the_admission()
    {
        using var nurse = await ClientAsync(ApiApplication.NurseEmail);
        var patientId = await NewPatientAsync(nurse, "Checked In Twice");
        var appointmentId = await ConfirmedIdAsync(nurse, patientId, SoonUtc());

        await CheckInAsync(nurse, appointmentId);
        var again = await CheckInAsync(nurse, appointmentId);
        using var body = await ReadJsonAsync(again);

        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
        Assert.Equal("cl_err_409_transition", body.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task A_nurse_may_check_somebody_in_at_every_level_walk_in_intake_offers()
    {
        using var nurse = await ClientAsync(ApiApplication.NurseEmail);

        foreach (var category in new[] { "icu", "general", "surgical", "emergency" })
        {
            var appointmentId = await ConfirmedIdAsync(
                nurse, await NewPatientAsync(nurse, $"Nurse Checks In {category}"), SoonUtc());

            var response = await CheckInAsync(nurse, appointmentId, category: category);

            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        }
    }

    [Fact]
    public async Task A_check_in_with_no_care_level_is_refused_rather_than_filed_as_intensive_care()
    {
        using var nurse = await ClientAsync(ApiApplication.NurseEmail);
        var appointmentId = await ConfirmedIdAsync(
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
        var appointmentId = await ConfirmedIdAsync(
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
        var appointmentId = await ConfirmedIdAsync(nurse, patientId, SoonUtc());

        await NewAdmissionAsync(nurse, patientId);

        var response = await CheckInAsync(nurse, appointmentId);
        using var body = await ReadJsonAsync(response);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("cl_pat_006", body.RootElement.GetProperty("code").GetString());

        using var listed = await ReadJsonAsync(
            await nurse.GetAsync("/api/appointments?status=confirmed&pageSize=100"));

        Assert.Contains(appointmentId, Ids(listed));
    }

    [Fact]
    public async Task The_desk_is_reception_the_ward_nurse_and_the_duty_manager_and_nobody_else()
    {
        using var doctor = await ClientAsync(ApiApplication.DoctorEmail);
        using var administrator = await ClientAsync(ApiApplication.AdministratorEmail);
        using var reception = await ClientAsync(ApiApplication.ReceptionEmail);
        using var manager = await ClientAsync(ApiApplication.ManagerEmail);

        // Reading the list is wider than acting on it: the administrator reaches a booking to
        // bill it, and cannot check anyone in.
        Assert.Equal(
            HttpStatusCode.Forbidden,
            (await doctor.GetAsync("/api/appointments")).StatusCode);
        Assert.Equal(
            HttpStatusCode.OK,
            (await administrator.GetAsync("/api/appointments")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await reception.GetAsync("/api/appointments")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await manager.GetAsync("/api/appointments")).StatusCode);

        var id = Guid.NewGuid().ToString();

        foreach (var refused in new[] { doctor, administrator })
        {
            Assert.Equal(HttpStatusCode.Forbidden, (await CheckInAsync(refused, id)).StatusCode);
        }

        // Past the policy, so a missing booking is a 404 rather than a 403.
        foreach (var allowed in new[] { reception, manager })
        {
            Assert.Equal(HttpStatusCode.NotFound, (await CheckInAsync(allowed, id)).StatusCode);
        }
    }

    [Fact]
    public async Task Reception_books_checks_in_completes_and_calls_off_a_booking()
    {
        using var nurse = await ClientAsync(ApiApplication.NurseEmail);
        using var reception = await ClientAsync(ApiApplication.ReceptionEmail);

        var booked = await ConfirmedIdAsync(
            reception, await NewPatientAsync(nurse, "Reception Books"), SoonUtc());

        var checkedIn = await CheckInAsync(
            reception,
            await ConfirmedIdAsync(reception, await NewPatientAsync(nurse, "Reception Admits"), SoonUtc()));

        var completed = await reception.PostAsync(
            $"/api/appointments/{booked}/complete", content: null);

        var calledOff = await reception.PostAsJsonAsync(
            $"/api/appointments/{await BookIdAsync(reception, await NewPatientAsync(nurse, "Reception Calls Off"), SoonUtc())}/cancel",
            new { reason = "Clinic full at 09:00. Please rebook after 14:00." });

        Assert.Equal(HttpStatusCode.Created, checkedIn.StatusCode);
        Assert.Equal(HttpStatusCode.OK, completed.StatusCode);
        Assert.Equal(HttpStatusCode.OK, calledOff.StatusCode);
    }

    [Fact]
    public async Task The_nurse_who_records_a_visit_as_seen_can_also_raise_and_settle_its_bill()
    {
        using var nurse = await ClientAsync(ApiApplication.NurseEmail);

        var appointmentId = await ConfirmedIdAsync(
            nurse, await NewPatientAsync(nurse, "Nurse Bills The Visit"), SoonUtc());

        var completed = await nurse.PostAsync(
            $"/api/appointments/{appointmentId}/complete", content: null);

        var raised = await nurse.PostAsync(
            $"/api/appointments/{appointmentId}/bill", content: null);

        var charged = await nurse.PostAsJsonAsync(
            $"/api/appointments/{appointmentId}/bill/charges",
            new { description = "Chest X-ray", quantity = 1, unit_price = 3500 });

        var settled = await nurse.PostAsJsonAsync(
            $"/api/appointments/{appointmentId}/bill/settle",
            new { settlement_note = "Cash" });

        Assert.Equal(HttpStatusCode.OK, completed.StatusCode);
        Assert.Equal(HttpStatusCode.OK, raised.StatusCode);
        Assert.Equal(HttpStatusCode.OK, charged.StatusCode);
        Assert.Equal(HttpStatusCode.OK, settled.StatusCode);
    }

    /// An admission bill is settled at discharge and that tick is reception's alone, so
    /// widening the outpatient bill must not have widened this one with it.
    [Fact]
    public async Task A_nurse_still_may_not_touch_an_admission_bill()
    {
        using var nurse = await ClientAsync(ApiApplication.NurseEmail);
        var admissionId = Guid.NewGuid().ToString();

        Assert.Equal(
            HttpStatusCode.Forbidden,
            (await nurse.PostAsync($"/api/admissions/{admissionId}/bill", content: null))
                .StatusCode);

        Assert.Equal(
            HttpStatusCode.Forbidden,
            (await nurse.PostAsJsonAsync(
                $"/api/admissions/{admissionId}/bill/settle", new { settlement_note = "Cash" }))
                .StatusCode);
    }

    /// The whole point of the confirm step: a booking three weeks out must not be one click
    /// away from creating an admission and starting a bed search for an empty chair.
    [Fact]
    public async Task Nothing_on_the_day_is_reachable_until_the_desk_confirms_the_booking()
    {
        using var nurse = await ClientAsync(ApiApplication.NurseEmail);
        var patientId = await NewPatientAsync(nurse, "Not Confirmed Yet");
        var appointmentId = await BookIdAsync(nurse, patientId, SoonUtc());

        var admit = await CheckInAsync(nurse, appointmentId);
        var seen = await nurse.PostAsync($"/api/appointments/{appointmentId}/complete", null);
        var absent = await nurse.PostAsync($"/api/appointments/{appointmentId}/no-show", null);

        foreach (var refused in new[] { admit, seen, absent })
        {
            Assert.Equal(HttpStatusCode.Conflict, refused.StatusCode);
        }

        using var body = await ReadJsonAsync(admit);
        Assert.Equal("cl_err_409_transition", body.RootElement.GetProperty("code").GetString());

        Assert.Equal(HttpStatusCode.OK, (await ConfirmAsync(nurse, appointmentId)).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await CheckInAsync(nurse, appointmentId)).StatusCode);
    }

    [Fact]
    public async Task Confirming_stamps_who_did_it_and_opens_the_day_of_actions()
    {
        using var nurse = await ClientAsync(ApiApplication.NurseEmail);
        var appointmentId = await BookIdAsync(
            nurse, await NewPatientAsync(nurse, "Gets Confirmed"), SoonUtc());

        using var booked = await ReadJsonAsync(
            await nurse.GetAsync($"/api/appointments?status=scheduled&pageSize=100"));
        Assert.Contains(appointmentId, Ids(booked));

        using var body = await ReadJsonAsync(await ConfirmAsync(nurse, appointmentId));

        Assert.Equal("confirmed", body.RootElement.GetProperty("status").GetString());
        Assert.False(body.RootElement.GetProperty("can_confirm").GetBoolean());
        Assert.True(body.RootElement.GetProperty("can_complete").GetBoolean());
        Assert.True(body.RootElement.GetProperty("can_cancel").GetBoolean());
        Assert.Equal(
            await NurseIdAsync(),
            body.RootElement.GetProperty("confirmed_by_staff_id").GetString());
        Assert.NotEqual(
            JsonValueKind.Null, body.RootElement.GetProperty("confirmed_at").ValueKind);
    }

    [Fact]
    public async Task Confirming_the_same_booking_twice_is_refused()
    {
        using var nurse = await ClientAsync(ApiApplication.NurseEmail);
        var appointmentId = await ConfirmedIdAsync(
            nurse, await NewPatientAsync(nurse, "Confirmed Twice"), SoonUtc());

        Assert.Equal(
            HttpStatusCode.Conflict, (await ConfirmAsync(nurse, appointmentId)).StatusCode);
    }

    [Fact]
    public async Task A_patient_who_never_came_is_recorded_as_such_and_can_book_again()
    {
        using var nurse = await ClientAsync(ApiApplication.NurseEmail);
        var patientId = await NewPatientAsync(nurse, "Never Turned Up");
        var appointmentId = await ConfirmedIdAsync(nurse, patientId, SoonUtc());

        using var body = await ReadJsonAsync(
            await nurse.PostAsync($"/api/appointments/{appointmentId}/no-show", null));

        Assert.Equal("no_show", body.RootElement.GetProperty("status").GetString());
        Assert.False(body.RootElement.GetProperty("can_cancel").GetBoolean());

        // The booking is closed, so it no longer blocks the next one.
        Assert.Equal(
            HttpStatusCode.Created,
            (await BookAsync(nurse, patientId, SoonUtc())).StatusCode);
    }

    /// An admitted patient is billed on their admission at discharge. Both endings read as
    /// completed, so without this guard the same visit could be charged twice.
    [Fact]
    public async Task An_admitted_booking_cannot_also_be_billed_as_an_outpatient_visit()
    {
        using var nurse = await ClientAsync(ApiApplication.NurseEmail);
        var appointmentId = await ConfirmedIdAsync(
            nurse, await NewPatientAsync(nurse, "Admitted Not Billed Here"), SoonUtc());

        Assert.Equal(HttpStatusCode.Created, (await CheckInAsync(nurse, appointmentId)).StatusCode);

        var raised = await nurse.PostAsync($"/api/appointments/{appointmentId}/bill", null);
        using var body = await ReadJsonAsync(raised);

        Assert.Equal(HttpStatusCode.Conflict, raised.StatusCode);
        Assert.Equal("cl_pat_035", body.RootElement.GetProperty("code").GetString());
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

    private static Task<HttpResponseMessage> ConfirmAsync(HttpClient client, string id)
        => client.PostAsync($"/api/appointments/{id}/confirm", content: null);

    /// What most tests want: a booking the desk has read and accepted, which is the only
    /// state the day-of actions are reachable from.
    private static async Task<string> ConfirmedIdAsync(
        HttpClient client, string patientId, DateTimeOffset scheduledAt)
    {
        var id = await BookIdAsync(client, patientId, scheduledAt);

        Assert.Equal(HttpStatusCode.OK, (await ConfirmAsync(client, id)).StatusCode);

        return id;
    }

    private async Task<HttpResponseMessage> CheckInAsync(
        HttpClient client,
        string appointmentId,
        string category = "general",
        string? staffId = null)
        => await client.PostAsJsonAsync($"/api/appointments/{appointmentId}/check-in", new
        {
            admission_category = category,
            category_set_by_staff_id = staffId ?? await NurseIdAsync(),
            urgency = "routine",
            is_infectious = false
        });

    [Fact]
    public async Task Maternity_is_refused_for_a_male_patient_and_allowed_for_a_female_one()
    {
        using var nurse = await ClientAsync(ApiApplication.NurseEmail);

        var forHim = await ConfirmedIdAsync(
            nurse, await NewPatientAsync(nurse, "Maternity Him"), SoonUtc());
        var forHer = await ConfirmedIdAsync(
            nurse, await NewPatientAsync(nurse, "Maternity Her", gender: "female"), SoonUtc());

        var refused = await CheckInAsync(nurse, forHim, category: "maternity");
        using var body = await ReadJsonAsync(refused);
        var allowed = await CheckInAsync(nurse, forHer, category: "maternity");

        Assert.Equal(HttpStatusCode.Conflict, refused.StatusCode);
        Assert.Equal("cl_pat_048", body.RootElement.GetProperty("code").GetString());
        Assert.Equal(HttpStatusCode.Created, allowed.StatusCode);
    }

    private static async Task<string> NewPatientAsync(
        HttpClient nurse, string fullName, string gender = "male")
    {
        var created = await nurse.PostAsJsonAsync("/api/patients", new
        {
            full_name = fullName,
            gender,
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
            admission_category = "general",
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
