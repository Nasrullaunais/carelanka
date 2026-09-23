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
public sealed class MeEndpointTests
{
    private readonly ApiApplication _application;

    public MeEndpointTests(ApiApplication application) => _application = application;

    [Fact]
    public async Task A_new_signup_has_no_record_until_they_fill_in_their_details()
    {
        using var patient = await NewPatientAccountAsync();

        var before = await patient.GetAsync("/api/me/profile");
        using var beforeBody = await ReadJsonAsync(before);

        Assert.Equal(HttpStatusCode.NotFound, before.StatusCode);
        Assert.Equal("cl_pat_033", beforeBody.RootElement.GetProperty("code").GetString());

        var saved = await PreRegisterAsync(patient, NewNic(), "Nimal Perera");
        using var savedBody = await ReadJsonAsync(saved);

        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
        Assert.Equal("Nimal Perera", savedBody.RootElement.GetProperty("full_name").GetString());

        Assert.Equal(8, savedBody.RootElement.GetProperty("patient_code").GetString()!.Length);

        var after = await patient.GetAsync("/api/me/profile");
        Assert.Equal(HttpStatusCode.OK, after.StatusCode);
    }

    [Fact]
    public async Task Filling_in_the_form_creates_no_admission_and_asks_for_no_arrival_date()
    {
        using var patient = await NewPatientAccountAsync();

        var saved = await patient.PostAsJsonAsync("/api/me/pre-register", new
        {
            nic = NewNic(),
            full_name = "No Admission Please",
            gender = "female",
            expected_arrival = DateTimeOffset.UtcNow.AddDays(2)
        });

        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);

        var admission = await patient.GetAsync("/api/me/admission");
        using var body = await ReadJsonAsync(admission);

        Assert.Equal(HttpStatusCode.NotFound, admission.StatusCode);
        Assert.Equal("cl_pat_034", body.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task A_returning_patient_is_linked_to_the_record_the_desk_already_holds()
    {
        using var nurse = await StaffClientAsync(ApiApplication.NurseEmail);
        var nic = NewNic();

        var existingId = await NewPatientAtTheDeskAsync(nurse, "Returning Patient", nic);

        using var patient = await NewPatientAccountAsync();
        var saved = await PreRegisterAsync(patient, nic, "Returning Patient");
        using var body = await ReadJsonAsync(saved);

        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);

        Assert.Equal(existingId, await LinkedRecordIdAsync(patient));
        Assert.Equal(nic, body.RootElement.GetProperty("nic").GetString());
    }

    [Fact]
    public async Task Linking_to_an_existing_record_never_overwrites_what_is_already_on_it()
    {
        using var nurse = await StaffClientAsync(ApiApplication.NurseEmail);
        var nic = NewNic();

        var existingId = await NewPatientAtTheDeskAsync(nurse, "Desk Typed This Name", nic);

        await nurse.PutAsJsonAsync($"/api/patients/{existingId}", new
        {
            full_name = "Desk Typed This Name",
            nic,
            gender = "male",
            address = "12 Galle Road, Colombo 3"
        });

        using var patient = await NewPatientAccountAsync();

        var saved = await patient.PostAsJsonAsync("/api/me/pre-register", new
        {
            nic,
            full_name = "Someone Elses Guess",
            gender = "female",
            address = "Not their address at all"
        });

        using var body = await ReadJsonAsync(saved);

        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);

        Assert.Equal("Desk Typed This Name", body.RootElement.GetProperty("full_name").GetString());
        Assert.Equal("12 Galle Road, Colombo 3", body.RootElement.GetProperty("address").GetString());
    }

    [Fact]
    public async Task A_record_already_signed_in_to_on_another_login_cannot_be_taken_over()
    {
        var nic = NewNic();

        using var first = await NewPatientAccountAsync();
        Assert.Equal(HttpStatusCode.OK, (await PreRegisterAsync(first, nic, "First Owner")).StatusCode);

        using var second = await NewPatientAccountAsync();
        var stolen = await PreRegisterAsync(second, nic, "Second Comer");
        using var body = await ReadJsonAsync(stolen);

        Assert.Equal(HttpStatusCode.Conflict, stolen.StatusCode);
        Assert.Equal("cl_pat_031", body.RootElement.GetProperty("code").GetString());

        Assert.Equal(HttpStatusCode.NotFound, (await second.GetAsync("/api/me/profile")).StatusCode);
    }

    [Fact]
    public async Task You_may_correct_your_own_details_but_not_which_human_you_are()
    {
        using var patient = await NewPatientAccountAsync();
        var nic = NewNic();

        await PreRegisterAsync(patient, nic, "Typo Inthisname");

        var corrected = await patient.PostAsJsonAsync("/api/me/pre-register", new
        {
            nic,
            full_name = "Fixed Name",
            gender = "male",
            phone = "0771234567"
        });

        using var body = await ReadJsonAsync(corrected);

        Assert.Equal(HttpStatusCode.OK, corrected.StatusCode);
        Assert.Equal("Fixed Name", body.RootElement.GetProperty("full_name").GetString());
        Assert.Equal("0771234567", body.RootElement.GetProperty("phone").GetString());

        var swapped = await PreRegisterAsync(patient, NewNic(), "Fixed Name");
        using var swappedBody = await ReadJsonAsync(swapped);

        Assert.Equal(HttpStatusCode.Conflict, swapped.StatusCode);
        Assert.Equal("cl_pat_032", swappedBody.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task A_blank_left_on_the_form_does_not_wipe_what_is_already_stored()
    {
        using var patient = await NewPatientAccountAsync();
        var nic = NewNic();

        await patient.PostAsJsonAsync("/api/me/pre-register", new
        {
            nic,
            full_name = "Half Filled Form",
            gender = "male",
            address = "45 Marine Drive, Colombo 6",
            emergency_contact_name = "Kumari Perera"
        });

        var second = await patient.PostAsJsonAsync("/api/me/pre-register", new
        {
            nic,
            full_name = "Half Filled Form",
            gender = "male",
            phone = "0779876543"
        });

        using var body = await ReadJsonAsync(second);

        Assert.Equal("0779876543", body.RootElement.GetProperty("phone").GetString());

        Assert.Equal("45 Marine Drive, Colombo 6", body.RootElement.GetProperty("address").GetString());
        Assert.Equal("Kumari Perera", body.RootElement.GetProperty("emergency_contact_name").GetString());
    }

    [Fact]
    public async Task The_profile_says_what_is_still_missing_rather_than_only_that_something_is()
    {
        using var patient = await NewPatientAccountAsync();

        await PreRegisterAsync(patient, NewNic(), "Missing Some Things");

        using var body = await ReadJsonAsync(await patient.GetAsync("/api/me/profile"));
        var missing = body.RootElement.GetProperty("missing_fields")
            .EnumerateArray().Select(field => field.GetString()).ToArray();

        Assert.False(body.RootElement.GetProperty("details_complete").GetBoolean());

        Assert.Contains("phone", missing);
        Assert.Contains("address", missing);
        Assert.Contains("date_of_birth", missing);
        Assert.DoesNotContain("full_name", missing);
        Assert.DoesNotContain("nic", missing);
    }

    [Fact]
    public async Task A_patient_books_their_own_visit_and_the_desk_sees_it_on_the_worklist()
    {
        using var patient = await NewPatientAccountAsync();
        await PreRegisterAsync(patient, NewNic(), "Books Their Own Visit");

        var when = DateTimeOffset.UtcNow.AddDays(3);
        var booked = await BookAsync(patient, when, "A cough that will not go");
        using var body = await ReadJsonAsync(booked);

        Assert.Equal(HttpStatusCode.Created, booked.StatusCode);
        Assert.Equal("scheduled", body.RootElement.GetProperty("status").GetString());
        Assert.True(body.RootElement.GetProperty("can_cancel").GetBoolean());
        Assert.Equal("A cough that will not go", body.RootElement.GetProperty("reason").GetString());

        var id = body.RootElement.GetProperty("appointment_id").GetString()!;

        using var nurse = await StaffClientAsync(ApiApplication.NurseEmail);
        using var desk = await ReadJsonAsync(await nurse.GetAsync("/api/appointments?pageSize=100"));

        Assert.Contains(
            desk.RootElement.GetProperty("items").EnumerateArray(),
            item => item.GetProperty("id").GetString() == id
                && item.GetProperty("booked_by_staff_id").ValueKind == JsonValueKind.Null);
    }

    [Fact]
    public async Task Booking_before_filling_in_your_details_is_refused_with_the_reason()
    {
        using var patient = await NewPatientAccountAsync();

        var booked = await BookAsync(patient, DateTimeOffset.UtcNow.AddDays(1));
        using var body = await ReadJsonAsync(booked);

        Assert.Equal(HttpStatusCode.Conflict, booked.StatusCode);
        Assert.Equal("cl_pat_033", body.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task A_self_booking_obeys_the_same_rules_as_one_taken_at_the_desk()
    {
        using var patient = await NewPatientAccountAsync();
        await PreRegisterAsync(patient, NewNic(), "Same Rules Apply");

        var past = await BookAsync(patient, DateTimeOffset.UtcNow.AddHours(-1));
        using var pastBody = await ReadJsonAsync(past);

        Assert.Equal(HttpStatusCode.BadRequest, past.StatusCode);
        Assert.Equal("cl_pat_010", pastBody.RootElement.GetProperty("code").GetString());

        Assert.Equal(
            HttpStatusCode.Created,
            (await BookAsync(patient, DateTimeOffset.UtcNow.AddDays(4))).StatusCode);

        var second = await BookAsync(patient, DateTimeOffset.UtcNow.AddDays(5));
        using var secondBody = await ReadJsonAsync(second);

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        Assert.Equal("cl_pat_009", secondBody.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task You_cancel_your_own_booking_and_cannot_cancel_it_twice()
    {
        using var patient = await NewPatientAccountAsync();
        await PreRegisterAsync(patient, NewNic(), "Cancels Their Booking");

        using var booked = await ReadJsonAsync(await BookAsync(patient, DateTimeOffset.UtcNow.AddDays(6)));
        var id = booked.RootElement.GetProperty("appointment_id").GetString()!;

        var cancelled = await patient.PostAsync($"/api/me/appointments/{id}/cancel", null);
        using var body = await ReadJsonAsync(cancelled);

        Assert.Equal(HttpStatusCode.OK, cancelled.StatusCode);
        Assert.Equal("cancelled", body.RootElement.GetProperty("status").GetString());

        Assert.False(body.RootElement.GetProperty("can_cancel").GetBoolean());

        var again = await patient.PostAsync($"/api/me/appointments/{id}/cancel", null);
        using var againBody = await ReadJsonAsync(again);

        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
        Assert.Equal("cl_err_409_transition", againBody.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Your_stay_reads_back_in_plain_language_with_no_staff_detail_on_it()
    {
        using var nurse = await StaffClientAsync(ApiApplication.NurseEmail);
        using var patient = await NewPatientAccountAsync();

        await PreRegisterAsync(patient, NewNic(), "Currently Admitted");
        var recordId = await LinkedRecordIdAsync(patient);

        await AdmitAsync(nurse, recordId);

        using var body = await ReadJsonAsync(await patient.GetAsync("/api/me/admission"));
        var stay = body.RootElement;

        Assert.Equal("awaiting_bed", stay.GetProperty("status").GetString());
        Assert.Equal("A bed is being arranged for you.", stay.GetProperty("status_text").GetString());

        foreach (var staffOnly in new[]
                 { "agent_message", "rejection_reason", "category_set_by_staff_id", "patient", "urgency" })
        {
            Assert.False(stay.TryGetProperty(staffOnly, out _), $"{staffOnly} leaked into MyAdmission");
        }
    }

    [Fact]
    public async Task History_is_finished_visits_only_so_the_current_stay_is_not_listed_twice()
    {
        using var nurse = await StaffClientAsync(ApiApplication.NurseEmail);
        using var patient = await NewPatientAccountAsync();

        await PreRegisterAsync(patient, NewNic(), "Has A History");
        var recordId = await LinkedRecordIdAsync(patient);

        await AdmitAsync(nurse, recordId);
        await DischargeAsync(recordId);
        await AdmitAsync(nurse, recordId);

        using var history = await ReadJsonAsync(await patient.GetAsync("/api/me/history"));
        var openId = (await ReadJsonAsync(await patient.GetAsync("/api/me/admission")))
            .RootElement.GetProperty("admission_id").GetString();

        Assert.Equal(1, history.RootElement.GetProperty("total_items").GetInt32());
        Assert.DoesNotContain(
            history.RootElement.GetProperty("items").EnumerateArray(),
            item => item.GetProperty("admission_id").GetString() == openId);
        Assert.Equal(
            "You have been discharged.",
            history.RootElement.GetProperty("items")[0].GetProperty("status_text").GetString());
    }

    [Fact]
    public async Task Never_having_been_treated_here_is_an_empty_list_and_not_an_error()
    {
        using var patient = await NewPatientAccountAsync();

        using var history = await ReadJsonAsync(await patient.GetAsync("/api/me/history"));
        using var appointments = await ReadJsonAsync(await patient.GetAsync("/api/me/appointments"));

        Assert.Equal(0, history.RootElement.GetProperty("total_items").GetInt32());
        Assert.Equal(0, appointments.RootElement.GetProperty("total_items").GetInt32());

        Assert.Equal(1, history.RootElement.GetProperty("total_pages").GetInt32());
    }

    [Fact]
    public async Task One_patient_cannot_read_or_touch_another_patients_anything()
    {
        using var nurse = await StaffClientAsync(ApiApplication.NurseEmail);

        using var first = await NewPatientAccountAsync();
        await PreRegisterAsync(first, NewNic(), "First Patient");
        var firstRecord = await LinkedRecordIdAsync(first);

        using var firstBooking = await ReadJsonAsync(
            await BookAtTheDeskAsync(nurse, firstRecord, DateTimeOffset.UtcNow.AddDays(9)));
        var firstAppointmentId = firstBooking.RootElement.GetProperty("id").GetString()!;

        await AdmitAsync(nurse, firstRecord);

        using var second = await NewPatientAccountAsync();
        await PreRegisterAsync(second, NewNic(), "Second Patient");

        var theirs = await second.PostAsync($"/api/me/appointments/{firstAppointmentId}/cancel", null);
        Assert.Equal(HttpStatusCode.NotFound, theirs.StatusCode);

        using var stillThere = await ReadJsonAsync(await first.GetAsync("/api/me/appointments"));
        Assert.Contains(
            stillThere.RootElement.GetProperty("items").EnumerateArray(),
            item => item.GetProperty("appointment_id").GetString() == firstAppointmentId
                && item.GetProperty("status").GetString() == "scheduled");

        Assert.Equal(HttpStatusCode.NotFound, (await second.GetAsync("/api/me/admission")).StatusCode);
        using var secondHistory = await ReadJsonAsync(await second.GetAsync("/api/me/history"));
        Assert.Equal(0, secondHistory.RootElement.GetProperty("total_items").GetInt32());
    }

    [Fact]
    public async Task Staff_are_refused_on_the_patient_surface_even_though_they_can_see_more()
    {
        using var nurse = await StaffClientAsync(ApiApplication.NurseEmail);

        foreach (var route in new[] { "/api/me/profile", "/api/me/admission", "/api/me/history", "/api/me/appointments" })
        {
            Assert.Equal(HttpStatusCode.Forbidden, (await nurse.GetAsync(route)).StatusCode);
        }

        Assert.Equal(
            HttpStatusCode.Forbidden,
            (await nurse.PostAsJsonAsync("/api/me/appointments", new { scheduled_at = DateTimeOffset.UtcNow.AddDays(1) })).StatusCode);
    }

    [Fact]
    public async Task Signed_out_is_a_401_and_not_an_empty_body()
    {
        using var anonymous = _application.CreateClient();

        var response = await anonymous.GetAsync("/api/me/profile");
        using var body = await ReadJsonAsync(response);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("cl_err_401_missing", body.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task A_walk_in_who_installs_the_app_afterwards_claims_the_record_the_desk_made()
    {
        using var nurse = await StaffClientAsync(ApiApplication.NurseEmail);
        var nic = NewNic();

        var (patientId, patientCode) =
            await NewWalkInAtTheDeskAsync(nurse, "Sunil Fernando", nic);

        await AdmitAsync(nurse, patientId);

        using var patient = await NewPatientAccountAsync();

        var stayBefore = await patient.GetAsync("/api/me/admission");
        using var beforeBody = await ReadJsonAsync(stayBefore);
        Assert.Equal("cl_pat_033", beforeBody.RootElement.GetProperty("code").GetString());

        var claimed = await ClaimAsync(patient, patientCode, nic);
        Assert.Equal(HttpStatusCode.OK, claimed.StatusCode);

        var stayAfter = await patient.GetAsync("/api/me/admission");
        using var afterBody = await ReadJsonAsync(stayAfter);

        Assert.Equal(HttpStatusCode.OK, stayAfter.StatusCode);
        Assert.Equal(patientId, await LinkedRecordIdAsync(patient));
        Assert.NotEqual(Guid.Empty.ToString(), afterBody.RootElement
            .GetProperty("admission_id").GetString());

        using var profile = await ReadJsonAsync(await patient.GetAsync("/api/me/profile"));
        Assert.Equal("Sunil Fernando", profile.RootElement.GetProperty("full_name").GetString());
    }

    [Fact]
    public async Task The_preview_masks_the_name_so_a_found_slip_shows_a_stranger_almost_nothing()
    {
        using var nurse = await StaffClientAsync(ApiApplication.NurseEmail);
        var nic = NewNic();

        var (_, patientCode) =
            await NewWalkInAtTheDeskAsync(nurse, "Kamala Jayasuriya", nic);

        using var patient = await NewPatientAccountAsync();

        var preview = await patient.PostAsJsonAsync("/api/me/claim/preview", new
        {
            patient_code = patientCode,
            nic
        });

        using var body = await ReadJsonAsync(preview);
        var masked = body.RootElement.GetProperty("masked_full_name").GetString()!;

        Assert.Equal(HttpStatusCode.OK, preview.StatusCode);
        Assert.DoesNotContain("Kamala", masked);
        Assert.DoesNotContain("Jayasuriya", masked);
        Assert.StartsWith("K", masked);

        // Preview writes nothing: the record is still unclaimed.
        using var stillUnlinked = await ReadJsonAsync(await patient.GetAsync("/api/me/profile"));
        Assert.Equal("cl_pat_033", stillUnlinked.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task A_wrong_nic_gives_the_same_answer_as_a_code_that_does_not_exist()
    {
        using var nurse = await StaffClientAsync(ApiApplication.NurseEmail);
        var nic = NewNic();

        var (_, patientCode) =
            await NewWalkInAtTheDeskAsync(nurse, "Ravi Bandara", nic);

        using var patient = await NewPatientAccountAsync();

        var wrongNic = await ClaimAsync(patient, patientCode, NewNic());
        using var wrongNicBody = await ReadJsonAsync(wrongNic);

        var noSuchCode = await ClaimAsync(patient, "PZZZZZZZ", nic);
        using var noSuchCodeBody = await ReadJsonAsync(noSuchCode);

        Assert.Equal(HttpStatusCode.NotFound, wrongNic.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, noSuchCode.StatusCode);

        // Identical, deliberately: a different answer would confirm the code is real.
        Assert.Equal("cl_pat_037", wrongNicBody.RootElement.GetProperty("code").GetString());
        Assert.Equal("cl_pat_037", noSuchCodeBody.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task A_record_somebody_has_already_claimed_cannot_be_claimed_again()
    {
        using var nurse = await StaffClientAsync(ApiApplication.NurseEmail);
        var nic = NewNic();

        var (_, patientCode) =
            await NewWalkInAtTheDeskAsync(nurse, "Already Taken", nic);

        using var first = await NewPatientAccountAsync();
        Assert.Equal(HttpStatusCode.OK, (await ClaimAsync(first, patientCode, nic)).StatusCode);

        using var second = await NewPatientAccountAsync();
        var stolen = await ClaimAsync(second, patientCode, nic);
        using var body = await ReadJsonAsync(stolen);

        Assert.Equal(HttpStatusCode.NotFound, stolen.StatusCode);
        Assert.Equal("cl_pat_037", body.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task A_login_that_already_has_a_record_is_refused_a_second_one()
    {
        using var nurse = await StaffClientAsync(ApiApplication.NurseEmail);
        var nic = NewNic();

        var (_, patientCode) = await NewWalkInAtTheDeskAsync(nurse, "Second Record", nic);

        using var patient = await NewPatientAccountAsync();
        Assert.Equal(HttpStatusCode.OK, (await PreRegisterAsync(patient, NewNic(), "Own Record")).StatusCode);

        var claimed = await ClaimAsync(patient, patientCode, nic);
        using var body = await ReadJsonAsync(claimed);

        Assert.Equal(HttpStatusCode.Conflict, claimed.StatusCode);
        Assert.Equal("cl_pat_004", body.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task A_claim_without_a_nic_never_reaches_the_service()
    {
        using var patient = await NewPatientAccountAsync();

        var response = await patient.PostAsJsonAsync(
            "/api/me/claim", new { patient_code = "PK4M9XB2" });

        using var body = await ReadJsonAsync(response);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("cl_err_400", body.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task A_stay_the_billing_desk_has_not_priced_reads_as_no_bill_rather_than_an_error()
    {
        using var nurse = await StaffClientAsync(ApiApplication.NurseEmail);
        using var patient = await NewPatientAccountAsync();

        var admissionId = await AdmittedPatientWithAppAccountAsync(nurse, patient);

        var bill = await patient.GetAsync($"/api/me/admissions/{admissionId}/bill");
        using var body = await ReadJsonAsync(bill);

        Assert.Equal(HttpStatusCode.NotFound, bill.StatusCode);
        Assert.Equal("cl_pat_036", body.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task A_bill_is_provisional_while_they_are_in_a_bed_and_final_once_discharged()
    {
        using var nurse = await StaffClientAsync(ApiApplication.NurseEmail);
        using var manager = await StaffClientAsync(ApiApplication.ManagerEmail);
        using var patient = await NewPatientAccountAsync();

        var admissionId = await AdmittedPatientWithAppAccountAsync(nurse, patient);

        var prepared = await manager.PostAsJsonAsync(
            $"/api/admissions/{admissionId}/bill", new { });
        Assert.Equal(HttpStatusCode.OK, prepared.StatusCode);

        var whileIn = await patient.GetAsync($"/api/me/admissions/{admissionId}/bill");
        using var whileInBody = await ReadJsonAsync(whileIn);

        Assert.Equal(HttpStatusCode.OK, whileIn.StatusCode);
        Assert.False(whileInBody.RootElement.GetProperty("is_final").GetBoolean());
        Assert.NotEmpty(whileInBody.RootElement.GetProperty("lines").EnumerateArray());

        // Staff-only fields must not be on the patient's copy at all.
        foreach (var staffOnly in new[] { "raised_by_staff_name", "settlement_note", "patient" })
        {
            Assert.False(whileInBody.RootElement.TryGetProperty(staffOnly, out _));
        }

        await DischargeAsync(await LinkedRecordIdAsync(patient));

        var afterwards = await patient.GetAsync($"/api/me/admissions/{admissionId}/bill");
        using var afterBody = await ReadJsonAsync(afterwards);

        Assert.Equal(HttpStatusCode.OK, afterwards.StatusCode);
        Assert.True(afterBody.RootElement.GetProperty("is_final").GetBoolean());
    }

    [Fact]
    public async Task One_patient_cannot_read_another_patients_bill()
    {
        using var nurse = await StaffClientAsync(ApiApplication.NurseEmail);
        using var manager = await StaffClientAsync(ApiApplication.ManagerEmail);
        using var owner = await NewPatientAccountAsync();
        using var stranger = await NewPatientAccountAsync();

        var admissionId = await AdmittedPatientWithAppAccountAsync(nurse, owner);
        Assert.Equal(
            HttpStatusCode.OK,
            (await manager.PostAsJsonAsync($"/api/admissions/{admissionId}/bill", new { })).StatusCode);

        Assert.Equal(
            HttpStatusCode.OK,
            (await PreRegisterAsync(stranger, NewNic(), "Nosy Neighbour")).StatusCode);

        var peek = await stranger.GetAsync($"/api/me/admissions/{admissionId}/bill");

        // 404, not 403: a stranger learns nothing about whether that admission exists.
        Assert.Equal(HttpStatusCode.NotFound, peek.StatusCode);
    }

    private async Task<string> AdmittedPatientWithAppAccountAsync(
        HttpClient nurse, HttpClient patient)
    {
        var nic = NewNic();
        var patientId = await NewPatientAtTheDeskAsync(nurse, "Billed Patient", nic);

        Assert.Equal(HttpStatusCode.OK, (await PreRegisterAsync(patient, nic, "Billed Patient")).StatusCode);

        await AdmitAsync(nurse, patientId);

        using var body = await ReadJsonAsync(await patient.GetAsync("/api/me/admission"));

        return body.RootElement.GetProperty("admission_id").GetString()!;
    }

    private static Task<HttpResponseMessage> ClaimAsync(
        HttpClient patient, string patientCode, string nic)
        => patient.PostAsJsonAsync(
            "/api/me/claim", new { patient_code = patientCode, nic });

    private static async Task<(string Id, string PatientCode)> NewWalkInAtTheDeskAsync(
        HttpClient nurse, string fullName, string nic)
    {
        var created = await nurse.PostAsJsonAsync("/api/patients", new
        {
            full_name = fullName,
            gender = "male",
            nic
        });

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        using var body = await ReadJsonAsync(created);

        return (
            body.RootElement.GetProperty("id").GetString()!,
            body.RootElement.GetProperty("patient_code").GetString()!);
    }

    private async Task<HttpClient> NewPatientAccountAsync()
    {
        var client = _application.CreateClient();

        var registered = await client.PostAsJsonAsync("/api/auth/patient/register", new
        {
            username = $"app.patient.{Random.Shared.Next(1_000_000, 9_999_999)}",
            password = ApiApplication.Password
        });

        Assert.Equal(HttpStatusCode.Created, registered.StatusCode);

        using var body = await ReadJsonAsync(registered);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", body.RootElement.GetProperty("access_token").GetString());

        return client;
    }

    private static Task<HttpResponseMessage> PreRegisterAsync(
        HttpClient patient, string nic, string fullName)
        => patient.PostAsJsonAsync("/api/me/pre-register", new
        {
            nic,
            full_name = fullName,
            gender = "male"
        });

    private static Task<HttpResponseMessage> BookAsync(
        HttpClient patient, DateTimeOffset when, string? reason = null)
        => patient.PostAsJsonAsync("/api/me/appointments", new { scheduled_at = when, reason });

    private static async Task<HttpResponseMessage> BookAtTheDeskAsync(
        HttpClient nurse, string patientId, DateTimeOffset when)
    {
        var booked = await nurse.PostAsJsonAsync(
            "/api/appointments", new { patient_id = patientId, scheduled_at = when });

        Assert.Equal(HttpStatusCode.Created, booked.StatusCode);

        return booked;
    }

    private static async Task<string> NewPatientAtTheDeskAsync(
        HttpClient nurse, string fullName, string nic)
    {
        var created = await nurse.PostAsJsonAsync("/api/patients", new
        {
            full_name = fullName,
            gender = "male",
            nic
        });

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        using var body = await ReadJsonAsync(created);
        return body.RootElement.GetProperty("id").GetString()!;
    }

    private async Task AdmitAsync(HttpClient nurse, string patientId)
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

    private async Task<string> LinkedRecordIdAsync(HttpClient patient)
    {
        using var body = await ReadJsonAsync(await patient.GetAsync("/api/me/profile"));
        var code = body.RootElement.GetProperty("patient_code").GetString();

        using var scope = _application.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CareLankaDbContext>();

        var record = await db.Patients.FirstAsync(p => p.PatientCode == code);

        return record.Id.ToString();
    }

    private static string NewNic() => $"N{Guid.NewGuid():N}"[..12];

    private static readonly SemaphoreSlim TokenLock = new(1, 1);
    private static readonly Dictionary<string, string> Tokens = new();
    private static string? _nurseId;

    private async Task<string> NurseIdAsync()
    {
        if (_nurseId is not null)
        {
            return _nurseId;
        }

        using var client = await StaffClientAsync(ApiApplication.NurseEmail);
        using var body = await ReadJsonAsync(await client.GetAsync("/api/auth/me"));

        return _nurseId = body.RootElement.GetProperty("id").GetString()!;
    }

    private async Task<HttpClient> StaffClientAsync(string email)
    {
        var client = _application.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", await StaffTokenAsync(email));

        return client;
    }

    private async Task<string> StaffTokenAsync(string email)
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

            return Tokens[email] = body.RootElement.GetProperty("access_token").GetString()!;
        }
        finally
        {
            TokenLock.Release();
        }
    }

    private static async Task<JsonDocument> ReadJsonAsync(HttpResponseMessage response)
        => JsonDocument.Parse(await response.Content.ReadAsStringAsync());
}
