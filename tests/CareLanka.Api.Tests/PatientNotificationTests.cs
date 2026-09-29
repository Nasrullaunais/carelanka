using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CareLanka.Api.Data;
using CareLanka.Api.Data.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CareLanka.Api.Tests;

[Collection(ApiCollection.Name)]
public sealed class PatientNotificationTests
{
    private readonly ApiApplication _application;
    private readonly NotificationTestKit _kit;

    public PatientNotificationTests(ApiApplication application)
    {
        _application = application;
        _kit = new NotificationTestKit(application);
    }

    [Fact]
    public async Task Booking_an_appointment_at_the_desk_tells_the_patient()
    {
        using var nurse = await _kit.StaffAsync(ApiApplication.NurseEmail);
        var patient = await _kit.NewPatientAsync();

        var appointmentId = await BookAsync(nurse, patient);

        await _kit.AssertSentAsync(NotificationType.AppointmentBooked, appointmentId,
            staff: [], patientAccounts: [patient.AccountId],
            actorStaffId: await _kit.StaffIdAsync(ApiApplication.NurseEmail));
    }

    [Fact]
    public async Task A_patient_booking_their_own_appointment_is_not_told_about_it()
    {
        var patient = await _kit.NewPatientAsync();

        var booked = await patient.Client.PostAsJsonAsync(
            "/api/me/appointments", new { scheduled_at = SoonUtc() });
        Assert.Equal(HttpStatusCode.Created, booked.StatusCode);
        var appointmentId = await IdAsync(booked);

        await _kit.AssertSentAsync(NotificationType.AppointmentBooked, appointmentId,
            staff: [], patientAccounts: [], actorPatientAccountId: patient.AccountId);
    }

    [Fact]
    public async Task Cancelling_an_appointment_at_the_desk_tells_the_patient()
    {
        using var nurse = await _kit.StaffAsync(ApiApplication.NurseEmail);
        var patient = await _kit.NewPatientAsync();
        var appointmentId = await BookAsync(nurse, patient);

        var cancelled = await nurse.PostAsJsonAsync(
            $"/api/appointments/{appointmentId}/cancel", new { reason = "Clinic is closed that day." });
        Assert.Equal(HttpStatusCode.OK, cancelled.StatusCode);

        await _kit.AssertSentAsync(NotificationType.AppointmentCancelled, appointmentId,
            staff: [], patientAccounts: [patient.AccountId],
            actorStaffId: await _kit.StaffIdAsync(ApiApplication.NurseEmail));
    }

    [Fact]
    public async Task A_patient_cancelling_their_own_appointment_is_not_told_about_it()
    {
        var patient = await _kit.NewPatientAsync();
        var booked = await patient.Client.PostAsJsonAsync(
            "/api/me/appointments", new { scheduled_at = SoonUtc() });
        var appointmentId = await IdAsync(booked);

        var cancelled = await patient.Client.PostAsync($"/api/me/appointments/{appointmentId}/cancel", null);
        Assert.Equal(HttpStatusCode.OK, cancelled.StatusCode);

        await _kit.AssertSentAsync(NotificationType.AppointmentCancelled, appointmentId,
            staff: [], patientAccounts: [], actorPatientAccountId: patient.AccountId);
    }

    [Fact]
    public async Task A_new_admission_alerts_every_duty_manager_except_the_one_who_made_it()
    {
        using var manager = await _kit.StaffAsync(ApiApplication.ManagerEmail);
        var managerId = await _kit.StaffIdAsync(ApiApplication.ManagerEmail);
        await _kit.SeedStaffAsync(StaffRole.DutyManager);
        var patient = await _kit.NewPatientAsync();

        var admissionId = await AdmitAsync(manager, patient);

        await _kit.AssertSentAsync(NotificationType.AdmissionAwaitingApproval, admissionId,
            staff: await _kit.RoleAsync(StaffRole.DutyManager, managerId), patientAccounts: [],
            actorStaffId: managerId);
    }

    [Fact]
    public async Task Assigning_a_bed_tells_the_patient_the_admission_is_approved()
    {
        using var nurse = await _kit.StaffAsync(ApiApplication.NurseEmail);
        var patient = await _kit.NewPatientAsync();
        var admissionId = await AdmitAsync(nurse, patient);

        await AssignBedAsync(nurse, admissionId, await NewBedAsync());

        await _kit.AssertSentAsync(NotificationType.AdmissionApproved, admissionId,
            staff: [], patientAccounts: [patient.AccountId],
            actorStaffId: await _kit.StaffIdAsync(ApiApplication.NurseEmail));
    }

    [Fact]
    public async Task Assigning_a_bed_tells_the_patient_which_bed()
    {
        using var nurse = await _kit.StaffAsync(ApiApplication.NurseEmail);
        var patient = await _kit.NewPatientAsync();
        var admissionId = await AdmitAsync(nurse, patient);
        var bedId = await NewBedAsync();

        await AssignBedAsync(nurse, admissionId, bedId);

        await _kit.AssertSentAsync(NotificationType.BedAssigned, bedId,
            staff: [], patientAccounts: [patient.AccountId],
            actorStaffId: await _kit.StaffIdAsync(ApiApplication.NurseEmail));
    }

    [Fact]
    public async Task Raising_a_bill_tells_the_patient()
    {
        using var reception = await _kit.StaffAsync(ApiApplication.ReceptionEmail);
        var (patient, admissionId) = await AdmittedAsync();

        var raised = await reception.PostAsync($"/api/admissions/{admissionId}/bill", null);
        Assert.Equal(HttpStatusCode.OK, raised.StatusCode);

        await _kit.AssertSentAsync(NotificationType.BillRaised, await BillIdAsync(admissionId),
            staff: [], patientAccounts: [patient.AccountId],
            actorStaffId: await _kit.StaffIdAsync(ApiApplication.ReceptionEmail));
    }

    [Fact]
    public async Task Settling_a_bill_tells_the_patient()
    {
        using var reception = await _kit.StaffAsync(ApiApplication.ReceptionEmail);
        var (patient, admissionId) = await AdmittedAsync();
        await reception.PostAsync($"/api/admissions/{admissionId}/bill", null);

        var settled = await reception.PostAsJsonAsync(
            $"/api/admissions/{admissionId}/bill/settle", new { settlement_note = "cash" });
        Assert.Equal(HttpStatusCode.OK, settled.StatusCode);

        await _kit.AssertSentAsync(NotificationType.BillSettled, await BillIdAsync(admissionId),
            staff: [], patientAccounts: [patient.AccountId],
            actorStaffId: await _kit.StaffIdAsync(ApiApplication.ReceptionEmail));
    }

    [Fact]
    public async Task The_last_discharge_step_tells_the_patient_they_can_go_home()
    {
        using var doctor = await _kit.StaffAsync(ApiApplication.DoctorEmail);
        using var reception = await _kit.StaffAsync(ApiApplication.ReceptionEmail);
        var (patient, admissionId) = await AdmittedAsync();

        var cleared = await doctor.PatchAsJsonAsync(
            $"/api/discharges/{admissionId}/checklist", new { clinical_clearance = true });
        Assert.Equal(HttpStatusCode.OK, cleared.StatusCode);
        Assert.Equal(0, await _kit.CountAsync(NotificationType.DischargeReady, admissionId));

        var settled = await reception.PostAsJsonAsync(
            $"/api/admissions/{admissionId}/bill/settle", new { settlement_note = "cash" });
        Assert.Equal(HttpStatusCode.OK, settled.StatusCode);

        await _kit.AssertSentAsync(NotificationType.DischargeReady, admissionId,
            staff: [], patientAccounts: [patient.AccountId],
            actorStaffId: await _kit.StaffIdAsync(ApiApplication.ReceptionEmail));
    }

    [Fact]
    public async Task An_approved_care_reply_tells_the_patient()
    {
        using var nurse = await _kit.StaffAsync(ApiApplication.NurseEmail);
        var (patient, _) = await AdmittedAsync();
        var recommendationId = await SubmitCareQueryAsync(patient, "I feel a little more tired than usual today.");
        await WaitUntilDraftedAsync(recommendationId);

        var approved = await nurse.PostAsJsonAsync(
            $"/api/care-recommendations/{recommendationId}/approve", new { doctor_message = "We will check on you." });
        Assert.Equal(HttpStatusCode.OK, approved.StatusCode);

        await _kit.AssertSentAsync(NotificationType.CareReplyReady, recommendationId,
            staff: [], patientAccounts: [patient.AccountId],
            actorStaffId: await _kit.StaffIdAsync(ApiApplication.NurseEmail));
    }

    [Fact]
    public async Task A_care_question_waits_for_review_by_the_ward_nurses_and_doctors()
    {
        var (patient, _) = await AdmittedAsync();

        var recommendationId = await SubmitCareQueryAsync(patient, "I feel a little more tired than usual today.");

        await _kit.AssertSentAsync(NotificationType.CareReplyWaiting, recommendationId,
            staff: [.. await _kit.RoleAsync(StaffRole.WardNurse), .. await _kit.RoleAsync(StaffRole.Doctor)],
            patientAccounts: [], actorPatientAccountId: patient.AccountId);
    }

    [Fact]
    public async Task A_red_flag_care_question_is_flagged_to_the_ward_nurses_and_doctors()
    {
        var (patient, _) = await AdmittedAsync();

        var recommendationId = await SubmitCareQueryAsync(patient, "I have sudden chest pain and I feel dizzy.");

        await _kit.AssertSentAsync(NotificationType.CareQueryFlagged, recommendationId,
            staff: [.. await _kit.RoleAsync(StaffRole.WardNurse), .. await _kit.RoleAsync(StaffRole.Doctor)],
            patientAccounts: [], actorPatientAccountId: patient.AccountId);
    }

    [Fact]
    public async Task An_ordinary_care_question_is_not_flagged()
    {
        var (patient, _) = await AdmittedAsync();

        var recommendationId = await SubmitCareQueryAsync(patient, "I feel a little more tired than usual today.");

        Assert.Equal(0, await _kit.CountAsync(NotificationType.CareQueryFlagged, recommendationId));
    }

    private static DateTimeOffset SoonUtc()
        => DateTimeOffset.UtcNow.AddDays(Random.Shared.Next(1, 300)).AddHours(3);

    private static async Task<Guid> IdAsync(HttpResponseMessage response)
    {
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var property = body.RootElement.TryGetProperty("appointment_id", out var appointmentId)
            ? appointmentId
            : body.RootElement.GetProperty("id");
        return property.GetGuid();
    }

    private static async Task<Guid> BookAsync(HttpClient desk, NotificationTestKit.LinkedPatient patient)
    {
        var booked = await desk.PostAsJsonAsync("/api/appointments", new
        {
            patient_id = patient.PatientId,
            scheduled_at = SoonUtc()
        });
        Assert.Equal(HttpStatusCode.Created, booked.StatusCode);

        return await IdAsync(booked);
    }

    private async Task<Guid> AdmitAsync(HttpClient staff, NotificationTestKit.LinkedPatient patient)
    {
        var created = await staff.PostAsJsonAsync("/api/admissions", new
        {
            patient_id = patient.PatientId,
            source = "walk_in",
            admission_category = "general",
            category_set_by_staff_id = await _kit.StaffIdAsync(ApiApplication.NurseEmail),
            urgency = "routine",
            is_infectious = false
        });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        return await IdAsync(created);
    }

    private async Task<Guid> NewBedAsync()
    {
        using var administrator = await _kit.StaffAsync(ApiApplication.AdministratorEmail);
        var ward = await administrator.PostAsJsonAsync("/api/wards", new
        {
            name = $"Notify-Ward-{Guid.NewGuid():N}"[..24],
            ward_type = "general",
            gender_policy = "mixed",
            is_active = true
        });
        Assert.Equal(HttpStatusCode.Created, ward.StatusCode);

        using var equipment = await _kit.StaffAsync(ApiApplication.EquipmentEmail);
        var bed = await equipment.PostAsJsonAsync("/api/beds", new
        {
            ward_id = await IdAsync(ward),
            bed_number = $"N{Guid.NewGuid():N}"[..6],
            has_isolation = false
        });
        Assert.Equal(HttpStatusCode.Created, bed.StatusCode);

        return await IdAsync(bed);
    }

    private static async Task AssignBedAsync(HttpClient staff, Guid admissionId, Guid bedId)
    {
        var assigned = await staff.PostAsJsonAsync(
            $"/api/admissions/{admissionId}/assign-bed", new { bed_id = bedId });
        Assert.Equal(HttpStatusCode.OK, assigned.StatusCode);
    }

    /// <summary>A linked patient who is on a ward bed and admitted, ready for bills, discharge and care questions.</summary>
    private async Task<(NotificationTestKit.LinkedPatient Patient, Guid AdmissionId)> AdmittedAsync()
    {
        using var nurse = await _kit.StaffAsync(ApiApplication.NurseEmail);
        var patient = await _kit.NewPatientAsync();
        var admissionId = await AdmitAsync(nurse, patient);
        await AssignBedAsync(nurse, admissionId, await NewBedAsync());

        var arrived = await nurse.PostAsync($"/api/admissions/{admissionId}/arrive", null);
        Assert.Equal(HttpStatusCode.OK, arrived.StatusCode);

        return (patient, admissionId);
    }

    private async Task<Guid> BillIdAsync(Guid admissionId)
    {
        using var scope = _application.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CareLankaDbContext>();
        return await db.Bills.Where(b => b.AdmissionId == admissionId).Select(b => b.Id).SingleAsync();
    }

    private static async Task<Guid> SubmitCareQueryAsync(NotificationTestKit.LinkedPatient patient, string text)
    {
        var started = await patient.Client.PostAsJsonAsync("/api/me/care-queries", new { reported_text = text });
        Assert.Equal(HttpStatusCode.Accepted, started.StatusCode);

        using var body = JsonDocument.Parse(await started.Content.ReadAsStringAsync());
        return body.RootElement.GetProperty("recommendation_id").GetGuid();
    }

    private async Task WaitUntilDraftedAsync(Guid recommendationId)
    {
        var deadline = DateTime.UtcNow.AddSeconds(30);

        while (true)
        {
            using var scope = _application.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<CareLankaDbContext>();
            var running = await db.AgentWorkflows.AnyAsync(w =>
                w.EntityId == recommendationId && w.AgentType == AgentType.PatientCareAdvisory
                && w.CompletedAt == null);

            if (!running)
            {
                return;
            }

            Assert.True(DateTime.UtcNow < deadline, "The care run never finished.");
            await Task.Delay(50);
        }
    }
}
