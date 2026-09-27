using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using CareLanka.Api.Data;
using CareLanka.Api.Data.Enums;
using CareLanka.Api.Data.Entities.Patient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CareLanka.Api.Tests;

[Collection(ApiCollection.Name)]
public sealed class DischargeBillingEndpointTests
{
    private readonly ApiApplication _application;

    public DischargeBillingEndpointTests(ApiApplication application) => _application = application;

    [Fact]
    public async Task Only_a_doctor_clears_a_patient_clinically()
    {
        var visit = await AdmittedVisitAsync();

        using var nurse = await ClientAsync(ApiApplication.NurseEmail);
        using var manager = await ClientAsync(ApiApplication.ManagerEmail);
        using var doctor = await ClientAsync(ApiApplication.DoctorEmail);

        var byNurse = await TickAsync(nurse, visit.AdmissionId, new { clinical_clearance = true });
        var byManager = await TickAsync(manager, visit.AdmissionId, new { clinical_clearance = true });
        var byDoctor = await TickAsync(doctor, visit.AdmissionId, new { clinical_clearance = true });

        Assert.Equal(HttpStatusCode.Forbidden, byNurse.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, byManager.StatusCode);
        Assert.Equal(HttpStatusCode.OK, byDoctor.StatusCode);

        using var refusal = await ReadJsonAsync(byNurse);
        Assert.Equal("cl_pat_022", refusal.RootElement.GetProperty("code").GetString());

        using var body = await ReadJsonAsync(byDoctor);
        Assert.True(Checklist(body, "clinical_clearance").GetProperty("ticked").GetBoolean());

        Assert.Equal(
            "Doctor Test",
            Checklist(body, "clinical_clearance").GetProperty("ticked_by_staff_name").GetString());
    }

    [Fact]
    public async Task Nobody_ticks_billing_settled_by_hand()
    {
        var visit = await AdmittedVisitAsync();

        foreach (var email in new[]
        {
            ApiApplication.NurseEmail,
            ApiApplication.DoctorEmail,
            ApiApplication.ManagerEmail,
        })
        {
            using var client = await ClientAsync(email);
            var refused = await TickAsync(client, visit.AdmissionId, new { billing_settled = true });

            using var problem = await ReadJsonAsync(refused);

            Assert.Equal(HttpStatusCode.Conflict, refused.StatusCode);
            Assert.Equal("cl_pat_025", problem.RootElement.GetProperty("code").GetString());
        }
    }

    [Fact]
    public async Task Ticking_the_last_mandatory_box_is_what_flags_the_patient()
    {
        var visit = await AdmittedVisitAsync();

        using var doctor = await ClientAsync(ApiApplication.DoctorEmail);
        using var reception = await ClientAsync(ApiApplication.ReceptionEmail);

        await TickAsync(doctor, visit.AdmissionId, new { clinical_clearance = true });

        Assert.Equal("admitted", await StatusAsync(visit.AdmissionId));

        var settled = await reception.PostAsJsonAsync(
            $"/api/admissions/{visit.AdmissionId}/bill/settle", new { settlement_note = "cash" });

        Assert.Equal(HttpStatusCode.OK, settled.StatusCode);
        Assert.Equal("ready_for_discharge", await StatusAsync(visit.AdmissionId));
    }

    [Fact]
    public async Task Unticking_a_box_takes_the_patient_back_off_the_candidate_list()
    {
        var visit = await ReadyToGoAsync();

        using var doctor = await ClientAsync(ApiApplication.DoctorEmail);
        var untick = await TickAsync(doctor, visit.AdmissionId, new { clinical_clearance = false });

        Assert.Equal(HttpStatusCode.OK, untick.StatusCode);

        Assert.Equal("admitted", await StatusAsync(visit.AdmissionId));

        using var body = await ReadJsonAsync(untick);
        Assert.False(body.RootElement.GetProperty("all_mandatory_ticked").GetBoolean());
    }

    [Fact]
    public async Task The_candidate_list_says_what_is_outstanding_rather_than_hiding_the_patient()
    {
        var visit = await AdmittedVisitAsync();

        using var nurse = await ClientAsync(ApiApplication.NurseEmail);
        using var doctor = await ClientAsync(ApiApplication.DoctorEmail);
        await TickAsync(doctor, visit.AdmissionId, new { clinical_clearance = true });

        var row = await CandidateAsync(nurse, visit.AdmissionId);

        Assert.Equal(
            new[] { "billing_settled" },
            row.GetProperty("outstanding_items").EnumerateArray()
                .Select(item => item.GetString())
                .ToArray());
        Assert.Equal(visit.BedNumber, row.GetProperty("bed_number").GetString());
    }

    [Fact]
    public async Task A_bill_is_the_care_level_and_the_bed_and_nothing_else_is_invented()
    {
        var visit = await AdmittedVisitAsync();

        using var reception = await ClientAsync(ApiApplication.ReceptionEmail);
        var prepared = await reception.PostAsync($"/api/admissions/{visit.AdmissionId}/bill", null);

        Assert.Equal(HttpStatusCode.OK, prepared.StatusCode);

        using var body = await ReadJsonAsync(prepared);
        var lines = body.RootElement.GetProperty("lines").EnumerateArray().ToList();

        Assert.Equal(2, lines.Count);
        Assert.Equal("admission_fee", lines[0].GetProperty("source").GetString());
        Assert.Equal(3000m, lines[0].GetProperty("line_total").GetDecimal());
        Assert.Equal("bed_stay", lines[1].GetProperty("source").GetString());
        Assert.Equal(1m, lines[1].GetProperty("quantity").GetDecimal());
        Assert.Equal(6000m, lines[1].GetProperty("unit_price").GetDecimal());
        Assert.Equal(9000m, body.RootElement.GetProperty("total").GetDecimal());
        Assert.Equal("LKR", body.RootElement.GetProperty("currency").GetString());
        Assert.False(body.RootElement.GetProperty("settled").GetBoolean());
    }

    [Fact]
    public async Task A_visit_still_waiting_for_a_bed_cannot_be_billed_or_settled()
    {
        var admissionId = await NewAdmissionAsync(category: "general");

        using var reception = await ClientAsync(ApiApplication.ReceptionEmail);
        var prepared = await reception.PostAsync($"/api/admissions/{admissionId}/bill", null);
        var settled = await reception.PostAsJsonAsync(
            $"/api/admissions/{admissionId}/bill/settle", new { });

        using var body = await ReadJsonAsync(prepared);

        Assert.Equal(HttpStatusCode.Conflict, prepared.StatusCode);
        Assert.Equal("cl_pat_044", body.RootElement.GetProperty("code").GetString());
        Assert.Equal(HttpStatusCode.Conflict, settled.StatusCode);
        Assert.Equal("awaiting_bed", await StatusAsync(admissionId));
    }

    [Fact]
    public async Task Settling_counts_the_nights_up_to_now_and_not_to_when_the_bill_was_raised()
    {
        var visit = await AdmittedVisitAsync();

        using var reception = await ClientAsync(ApiApplication.ReceptionEmail);
        await reception.PostAsync($"/api/admissions/{visit.AdmissionId}/bill", null);

        await BackdateOccupancyAsync(visit.AdmissionId, TimeSpan.FromHours(30));

        using var body = await ReadJsonAsync(await reception.PostAsJsonAsync(
            $"/api/admissions/{visit.AdmissionId}/bill/settle", new { }));

        var bedLine = body.RootElement.GetProperty("lines").EnumerateArray()
            .Single(line => line.GetProperty("source").GetString() == "bed_stay");

        Assert.Equal(2m, bedLine.GetProperty("quantity").GetDecimal());
        Assert.Equal(15000m, body.RootElement.GetProperty("total").GetDecimal());
    }

    [Fact]
    public async Task The_checklist_is_refused_for_a_patient_who_is_not_on_the_ward()
    {
        var admissionId = await NewAdmissionAsync(category: "general");

        using var doctor = await ClientAsync(ApiApplication.DoctorEmail);
        var ticked = await TickAsync(doctor, admissionId, new { clinical_clearance = true });

        using var body = await ReadJsonAsync(ticked);

        Assert.Equal(HttpStatusCode.Conflict, ticked.StatusCode);
        Assert.Equal("cl_pat_046", body.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task A_second_day_in_the_bed_is_a_second_day_on_the_bill()
    {
        var visit = await AdmittedVisitAsync();

        await BackdateOccupancyAsync(visit.AdmissionId, TimeSpan.FromHours(30));

        using var reception = await ClientAsync(ApiApplication.ReceptionEmail);
        using var body = await ReadJsonAsync(
            await reception.PostAsync($"/api/admissions/{visit.AdmissionId}/bill", null));

        var bedLine = body.RootElement.GetProperty("lines").EnumerateArray()
            .Single(line => line.GetProperty("source").GetString() == "bed_stay");

        Assert.Equal(2m, bedLine.GetProperty("quantity").GetDecimal());
        Assert.Equal(12000m, bedLine.GetProperty("line_total").GetDecimal());
        Assert.Equal(15000m, body.RootElement.GetProperty("total").GetDecimal());
    }

    [Fact]
    public async Task Preparing_again_updates_the_bed_days_and_keeps_what_reception_typed()
    {
        var visit = await AdmittedVisitAsync();

        using var reception = await ClientAsync(ApiApplication.ReceptionEmail);

        await reception.PostAsJsonAsync(
            $"/api/admissions/{visit.AdmissionId}/bill/charges",
            new { description = "Chest X-ray", quantity = 1, unit_price = 3500 });

        await BackdateOccupancyAsync(visit.AdmissionId, TimeSpan.FromHours(30));

        using var body = await ReadJsonAsync(
            await reception.PostAsync($"/api/admissions/{visit.AdmissionId}/bill", null));

        var lines = body.RootElement.GetProperty("lines").EnumerateArray().ToList();

        Assert.Equal(3, lines.Count);
        Assert.Single(lines, line => line.GetProperty("source").GetString() == "manual");
        Assert.Equal(18500m, body.RootElement.GetProperty("total").GetDecimal());
    }

    [Fact]
    public async Task A_typed_charge_can_be_removed_and_a_worked_out_one_cannot()
    {
        var visit = await AdmittedVisitAsync();

        using var reception = await ClientAsync(ApiApplication.ReceptionEmail);

        using var added = await ReadJsonAsync(await reception.PostAsJsonAsync(
            $"/api/admissions/{visit.AdmissionId}/bill/charges",
            new { description = "Typed by mistake", quantity = 2, unit_price = 500 }));

        var lines = added.RootElement.GetProperty("lines").EnumerateArray().ToList();
        var typed = lines.Single(line => line.GetProperty("source").GetString() == "manual");
        var bed = lines.Single(line => line.GetProperty("source").GetString() == "bed_stay");

        var bedRefused = await reception.DeleteAsync(
            $"/api/admissions/{visit.AdmissionId}/bill/charges/{bed.GetProperty("id").GetString()}");
        var typedRemoved = await reception.DeleteAsync(
            $"/api/admissions/{visit.AdmissionId}/bill/charges/{typed.GetProperty("id").GetString()}");

        using var problem = await ReadJsonAsync(bedRefused);

        Assert.Equal(HttpStatusCode.Conflict, bedRefused.StatusCode);
        Assert.Equal("cl_pat_027", problem.RootElement.GetProperty("code").GetString());
        Assert.Equal(HttpStatusCode.OK, typedRemoved.StatusCode);

        using var after = await ReadJsonAsync(typedRemoved);
        Assert.Equal(9000m, after.RootElement.GetProperty("total").GetDecimal());
    }

    [Fact]
    public async Task A_ward_nurse_may_read_a_bill_and_may_not_touch_the_money()
    {
        var visit = await AdmittedVisitAsync();

        using var reception = await ClientAsync(ApiApplication.ReceptionEmail);
        await reception.PostAsync($"/api/admissions/{visit.AdmissionId}/bill", null);

        using var nurse = await ClientAsync(ApiApplication.NurseEmail);

        var read = await nurse.GetAsync($"/api/admissions/{visit.AdmissionId}/bill");
        var settle = await nurse.PostAsJsonAsync(
            $"/api/admissions/{visit.AdmissionId}/bill/settle", new { });
        var worklist = await nurse.GetAsync("/api/billing/outstanding");

        Assert.Equal(HttpStatusCode.OK, read.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, settle.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, worklist.StatusCode);
    }

    [Fact]
    public async Task A_settled_bill_is_frozen()
    {
        var visit = await AdmittedVisitAsync();

        using var reception = await ClientAsync(ApiApplication.ReceptionEmail);
        await reception.PostAsJsonAsync(
            $"/api/admissions/{visit.AdmissionId}/bill/settle", new { settlement_note = "cash" });

        var addAgain = await reception.PostAsJsonAsync(
            $"/api/admissions/{visit.AdmissionId}/bill/charges",
            new { description = "Too late", quantity = 1, unit_price = 100 });
        var prepareAgain = await reception.PostAsync(
            $"/api/admissions/{visit.AdmissionId}/bill", null);
        var settleAgain = await reception.PostAsJsonAsync(
            $"/api/admissions/{visit.AdmissionId}/bill/settle", new { });

        using var problem = await ReadJsonAsync(addAgain);

        Assert.Equal(HttpStatusCode.Conflict, addAgain.StatusCode);
        Assert.Equal("cl_pat_026", problem.RootElement.GetProperty("code").GetString());
        Assert.Equal(HttpStatusCode.Conflict, prepareAgain.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, settleAgain.StatusCode);
    }

    [Fact]
    public async Task Settling_a_visit_nobody_billed_works_the_bill_out_first()
    {
        var visit = await AdmittedVisitAsync();

        using var reception = await ClientAsync(ApiApplication.ReceptionEmail);

        using var body = await ReadJsonAsync(await reception.PostAsJsonAsync(
            $"/api/admissions/{visit.AdmissionId}/bill/settle", new { }));

        Assert.Equal(9000m, body.RootElement.GetProperty("total").GetDecimal());
        Assert.True(body.RootElement.GetProperty("settled").GetBoolean());
        Assert.Equal(2, body.RootElement.GetProperty("lines").GetArrayLength());
    }

    [Fact]
    public async Task Settling_the_bill_is_what_ticks_the_box()
    {
        var visit = await AdmittedVisitAsync();

        using var reception = await ClientAsync(ApiApplication.ReceptionEmail);
        await reception.PostAsJsonAsync(
            $"/api/admissions/{visit.AdmissionId}/bill/settle", new { settlement_note = "card" });

        using var nurse = await ClientAsync(ApiApplication.NurseEmail);
        using var body = await ReadJsonAsync(
            await nurse.GetAsync($"/api/admissions/{visit.AdmissionId}"));

        var box = body.RootElement.GetProperty("discharge").GetProperty("checklist")
            .GetProperty("billing_settled");

        Assert.True(box.GetProperty("ticked").GetBoolean());
        Assert.True(body.RootElement.GetProperty("bill").GetProperty("settled").GetBoolean());
    }

    [Fact]
    public async Task The_outstanding_list_carries_visits_nobody_has_billed_yet()
    {
        var visit = await AdmittedVisitAsync();

        using var reception = await ClientAsync(ApiApplication.ReceptionEmail);
        var before = await OutstandingRowAsync(reception, visit.AdmissionId);

        Assert.Equal(JsonValueKind.Null, before!.Value.GetProperty("bill_number").ValueKind);
        Assert.Equal(9000m, before.Value.GetProperty("estimated_total").GetDecimal());

        await reception.PostAsJsonAsync(
            $"/api/admissions/{visit.AdmissionId}/bill/settle", new { });

        Assert.Null(await OutstandingRowAsync(reception, visit.AdmissionId));
    }

    [Fact]
    public async Task A_settled_bill_can_still_be_found_and_reprinted_after_the_patient_has_gone_home()
    {
        var visit = await ReadyToGoAsync();

        using var nurse = await ClientAsync(ApiApplication.NurseEmail);
        await nurse.PostAsJsonAsync($"/api/discharges/{visit.AdmissionId}/confirm", new { });

        using var reception = await ClientAsync(ApiApplication.ReceptionEmail);

        Assert.Null(await OutstandingRowAsync(reception, visit.AdmissionId));

        var row = await OutstandingRowAsync(reception, visit.AdmissionId, includeSettled: true);

        Assert.NotNull(row);
        Assert.True(row!.Value.GetProperty("settled").GetBoolean());
        Assert.Equal("discharged", row.Value.GetProperty("status").GetString());
        Assert.Equal(9000m, row.Value.GetProperty("estimated_total").GetDecimal());
        Assert.NotEqual(JsonValueKind.Null, row.Value.GetProperty("bill_number").ValueKind);

        using var bill = await ReadJsonAsync(
            await reception.GetAsync($"/api/admissions/{visit.AdmissionId}/bill"));

        Assert.True(bill.RootElement.GetProperty("settled").GetBoolean());
        Assert.Equal(9000m, bill.RootElement.GetProperty("total").GetDecimal());
    }

    [Fact]
    public async Task A_discharge_with_a_box_outstanding_is_refused()
    {
        var visit = await AdmittedVisitAsync();

        using var nurse = await ClientAsync(ApiApplication.NurseEmail);
        using var doctor = await ClientAsync(ApiApplication.DoctorEmail);

        await TickAsync(doctor, visit.AdmissionId, new { clinical_clearance = true });

        var refused = await nurse.PostAsJsonAsync(
            $"/api/discharges/{visit.AdmissionId}/confirm", new { summary_note = "too early" });

        using var problem = await ReadJsonAsync(refused);

        Assert.Equal(HttpStatusCode.Conflict, refused.StatusCode);
        Assert.Equal("cl_pat_023", problem.RootElement.GetProperty("code").GetString());
        Assert.Contains("billing_settled", problem.RootElement.GetProperty("detail").GetString()!);
    }

    [Fact]
    public async Task Confirming_ends_the_visit_and_gives_the_bed_back()
    {
        var visit = await ReadyToGoAsync();

        using var nurse = await ClientAsync(ApiApplication.NurseEmail);
        var confirmed = await nurse.PostAsJsonAsync(
            $"/api/discharges/{visit.AdmissionId}/confirm",
            new { summary_note = "Rest at home for three days." });

        Assert.Equal(HttpStatusCode.OK, confirmed.StatusCode);

        using var discharge = await ReadJsonAsync(confirmed);
        Assert.Equal("Rest at home for three days.", discharge.RootElement.GetProperty("summary_note").GetString());
        Assert.NotEqual(JsonValueKind.Null, discharge.RootElement.GetProperty("confirmed_at").ValueKind);

        using var after = await ReadJsonAsync(
            await nurse.GetAsync($"/api/admissions/{visit.AdmissionId}"));

        Assert.Equal("discharged", after.RootElement.GetProperty("status").GetString());
        Assert.NotEqual(JsonValueKind.Null, after.RootElement.GetProperty("discharged_at").ValueKind);

        var assignment = after.RootElement.GetProperty("bed_assignments").EnumerateArray().First();
        Assert.Equal("released", assignment.GetProperty("status").GetString());
        Assert.Equal("discharged", assignment.GetProperty("release_reason").GetString());

        using var occupancy = await ReadJsonAsync(
            await nurse.GetAsync($"/api/beds/{visit.BedId}/occupancy"));

        Assert.False(occupancy.RootElement.GetProperty("occupied").GetBoolean());
    }

    [Fact]
    public async Task Confirming_a_discharge_closes_unanswered_care_recommendations()
    {
        var visit = await ReadyToGoAsync();

        using (var scope = _application.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CareLankaDbContext>();
            var admissionId = Guid.Parse(visit.AdmissionId);
            var patientId = await db.Admissions
                .Where(row => row.Id == admissionId)
                .Select(row => row.PatientId)
                .SingleAsync();

            db.CareRecommendations.Add(new CareRecommendation
            {
                Id = Guid.NewGuid(),
                PatientId = patientId,
                AdmissionId = admissionId,
                ReportedText = "I need help.",
                ReportedAt = DateTimeOffset.UtcNow,
                AgentMessage = "A nurse will come soon.",
                Status = CareRecommendationStatus.PendingReview,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            });
            await db.SaveChangesAsync();
        }

        using var nurse = await ClientAsync(ApiApplication.NurseEmail);
        var confirmed = await nurse.PostAsJsonAsync($"/api/discharges/{visit.AdmissionId}/confirm", new { });
        Assert.Equal(HttpStatusCode.OK, confirmed.StatusCode);

        using var verificationScope = _application.Services.CreateScope();
        var verificationDb = verificationScope.ServiceProvider.GetRequiredService<CareLankaDbContext>();
        var recommendation = await verificationDb.CareRecommendations
            .AsNoTracking()
            .SingleAsync(row => row.AdmissionId == Guid.Parse(visit.AdmissionId));

        Assert.Equal(CareRecommendationStatus.Rejected, recommendation.Status);
        Assert.Equal(
            "Closed automatically: the patient was discharged before this was answered.",
            recommendation.RejectionReason);
    }

    [Fact]
    public async Task A_nurse_may_confirm_an_icu_discharge()
    {
        var icu = await ReadyToGoAsync(wardType: "icu", category: "icu");

        using var nurse = await ClientAsync(ApiApplication.NurseEmail);
        var confirmed = await nurse.PostAsJsonAsync(
            $"/api/discharges/{icu.AdmissionId}/confirm", new { });

        Assert.Equal(HttpStatusCode.OK, confirmed.StatusCode);
    }

    [Fact]
    public async Task Reception_may_confirm_a_discharge()
    {
        var visit = await ReadyToGoAsync();

        using var reception = await ClientAsync(ApiApplication.ReceptionEmail);
        var confirmed = await reception.PostAsJsonAsync(
            $"/api/discharges/{visit.AdmissionId}/confirm", new { });

        Assert.Equal(HttpStatusCode.OK, confirmed.StatusCode);
    }

    [Fact]
    public async Task A_doctor_does_not_confirm_a_discharge()
    {
        var visit = await ReadyToGoAsync();

        using var doctor = await ClientAsync(ApiApplication.DoctorEmail);
        var refused = await doctor.PostAsJsonAsync(
            $"/api/discharges/{visit.AdmissionId}/confirm", new { });

        Assert.Equal(HttpStatusCode.Forbidden, refused.StatusCode);
    }

    [Fact]
    public async Task A_finished_discharge_is_a_record_rather_than_a_row_that_vanishes()
    {
        var visit = await ReadyToGoAsync();

        using var nurse = await ClientAsync(ApiApplication.NurseEmail);

        var confirmed = await nurse.PostAsJsonAsync(
            $"/api/discharges/{visit.AdmissionId}/confirm", new { summary_note = "Home." });

        Assert.Equal(HttpStatusCode.OK, confirmed.StatusCode);

        Assert.Null(await FindCandidateAsync(nurse, visit.AdmissionId, includeDischarged: false));

        var record = await FindCandidateAsync(nurse, visit.AdmissionId, includeDischarged: true);

        Assert.NotNull(record);
        Assert.True(record!.Value.GetProperty("is_discharged").GetBoolean());

        Assert.NotEqual(
            JsonValueKind.Null, record.Value.GetProperty("discharged_at").ValueKind);

        Assert.Empty(record.Value.GetProperty("outstanding_items").EnumerateArray());
    }

    [Fact]
    public async Task A_finished_stay_counts_days_to_when_they_left_and_not_to_now()
    {
        var visit = await AdmittedVisitAsync();

        await BackdateAdmissionAsync(visit.AdmissionId, TimeSpan.FromDays(10));

        using var doctor = await ClientAsync(ApiApplication.DoctorEmail);
        using var reception = await ClientAsync(ApiApplication.ReceptionEmail);
        using var nurse = await ClientAsync(ApiApplication.NurseEmail);

        await TickAsync(doctor, visit.AdmissionId, new { clinical_clearance = true });
        await reception.PostAsJsonAsync(
            $"/api/admissions/{visit.AdmissionId}/bill/settle", new { settlement_note = "cash" });

        var confirmed = await nurse.PostAsJsonAsync(
            $"/api/discharges/{visit.AdmissionId}/confirm", new { });

        Assert.Equal(HttpStatusCode.OK, confirmed.StatusCode);

        await BackdateDischargeAsync(visit.AdmissionId, TimeSpan.FromDays(5.5));

        var record = await FindCandidateAsync(nurse, visit.AdmissionId, includeDischarged: true);

        Assert.Equal(5, record!.Value.GetProperty("days_in_bed").GetInt32());
    }

    private async Task BackdateAdmissionAsync(string admissionId, TimeSpan by)
    {
        using var scope = _application.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CareLankaDbContext>();

        var id = Guid.Parse(admissionId);

        var admission = await db.Admissions.FirstAsync(row => row.Id == id);
        admission.AdmittedAt = DateTimeOffset.UtcNow - by;

        await db.SaveChangesAsync();
    }

    private async Task BackdateDischargeAsync(string admissionId, TimeSpan by)
    {
        using var scope = _application.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CareLankaDbContext>();

        var id = Guid.Parse(admissionId);

        var discharge = await db.Discharges.FirstAsync(row => row.AdmissionId == id);
        discharge.ConfirmedAt = DateTimeOffset.UtcNow - by;

        await db.SaveChangesAsync();
    }

    private static async Task<JsonElement?> FindCandidateAsync(
        HttpClient client, string admissionId, bool includeDischarged)
    {
        var response = await client.GetAsync(
            $"/api/discharges/candidates?pageSize=100&includeDischarged={includeDischarged}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var body = await ReadJsonAsync(response);

        foreach (var row in body.RootElement.GetProperty("items").EnumerateArray())
        {
            if (row.GetProperty("admission_id").GetString() == admissionId)
            {
                return row.Clone();
            }
        }

        return null;
    }

    [Fact]
    public async Task A_discharged_visit_still_says_which_bed_they_were_in()
    {
        var visit = await ReadyToGoAsync();

        using var nurse = await ClientAsync(ApiApplication.NurseEmail);

        var before = await FindCandidateAsync(nurse, visit.AdmissionId, includeDischarged: false);
        Assert.Equal(visit.BedNumber, before!.Value.GetProperty("bed_number").GetString());

        await nurse.PostAsJsonAsync($"/api/discharges/{visit.AdmissionId}/confirm", new { });

        var after = await FindCandidateAsync(nurse, visit.AdmissionId, includeDischarged: true);

        Assert.Equal(visit.BedNumber, after!.Value.GetProperty("bed_number").GetString());
        Assert.NotEqual(string.Empty, after.Value.GetProperty("ward_name").GetString());
    }

    [Fact]
    public async Task A_bill_names_the_person_who_raised_it()
    {
        var visit = await AdmittedVisitAsync();

        using var reception = await ClientAsync(ApiApplication.ReceptionEmail);

        var prepared = await reception.PostAsync($"/api/admissions/{visit.AdmissionId}/bill", null);
        Assert.Equal(HttpStatusCode.OK, prepared.StatusCode);

        using var body = await ReadJsonAsync(prepared);

        Assert.Equal(
            "GeneralStaff Test",
            body.RootElement.GetProperty("raised_by_staff_name").GetString());
    }

    private sealed record TestVisit(string AdmissionId, Guid BedId, string BedNumber);

    private async Task<TestVisit> AdmittedVisitAsync(
        string wardType = "general", string category = "general")
    {
        var ward = await NewWardAsync(wardType);
        var (bedId, bedNumber) = await AddBedAsync(ward);

        var admissionId = await NewAdmissionAsync(category);

        using var placer = await ClientAsync(
            wardType is "icu" or "hdu" ? ApiApplication.ManagerEmail : ApiApplication.NurseEmail);

        var assigned = await placer.PostAsJsonAsync(
            $"/api/admissions/{admissionId}/assign-bed", new { bed_id = bedId });
        Assert.Equal(HttpStatusCode.OK, assigned.StatusCode);

        using var nurse = await ClientAsync(ApiApplication.NurseEmail);
        var arrived = await nurse.PostAsync($"/api/admissions/{admissionId}/arrive", null);
        Assert.Equal(HttpStatusCode.OK, arrived.StatusCode);

        return new TestVisit(admissionId, bedId, bedNumber);
    }

    private async Task<TestVisit> ReadyToGoAsync(
        string wardType = "general", string category = "general")
    {
        var visit = await AdmittedVisitAsync(wardType, category);

        using var doctor = await ClientAsync(ApiApplication.DoctorEmail);
        using var reception = await ClientAsync(ApiApplication.ReceptionEmail);

        await TickAsync(doctor, visit.AdmissionId, new { clinical_clearance = true });

        var settled = await reception.PostAsJsonAsync(
            $"/api/admissions/{visit.AdmissionId}/bill/settle", new { settlement_note = "cash" });
        Assert.Equal(HttpStatusCode.OK, settled.StatusCode);

        return visit;
    }

    private static Task<HttpResponseMessage> TickAsync(
        HttpClient client, string admissionId, object body)
        => client.PatchAsJsonAsync($"/api/discharges/{admissionId}/checklist", body);

    private static JsonElement Checklist(JsonDocument discharge, string item)
        => discharge.RootElement.GetProperty("checklist").GetProperty(item);

    private async Task<string> StatusAsync(string admissionId)
    {
        using var nurse = await ClientAsync(ApiApplication.NurseEmail);
        using var body = await ReadJsonAsync(await nurse.GetAsync($"/api/admissions/{admissionId}"));

        return body.RootElement.GetProperty("status").GetString()!;
    }

    private static async Task<JsonElement> CandidateAsync(HttpClient client, string admissionId)
    {
        var response = await client.GetAsync("/api/discharges/candidates?pageSize=100");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var body = await ReadJsonAsync(response);

        return body.RootElement.GetProperty("items").EnumerateArray()
            .Single(row => row.GetProperty("admission_id").GetString() == admissionId)
            .Clone();
    }

    private static async Task<JsonElement?> OutstandingRowAsync(
        HttpClient client, string admissionId, bool includeSettled = false)
    {
        var response = await client.GetAsync(
            $"/api/billing/outstanding?pageSize=100&includeSettled={includeSettled}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var body = await ReadJsonAsync(response);

        foreach (var row in body.RootElement.GetProperty("items").EnumerateArray())
        {
            if (row.GetProperty("admission_id").GetString() == admissionId)
            {
                return row.Clone();
            }
        }

        return null;
    }

    private async Task BackdateOccupancyAsync(string admissionId, TimeSpan by)
    {
        using var scope = _application.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CareLankaDbContext>();

        var id = Guid.Parse(admissionId);

        var assignment = await db.BedAssignments
            .Where(a => a.AdmissionId == id && a.Status == AssignmentStatus.Occupied)
            .FirstAsync();

        assignment.OccupiedAt = DateTimeOffset.UtcNow - by;

        await db.SaveChangesAsync();
    }

    private sealed record TestWard(Guid Id, string Name);

    private async Task<TestWard> NewWardAsync(string wardType)
    {
        using var administrator = await ClientAsync(ApiApplication.AdministratorEmail);
        var name = $"Bill-Ward-{Guid.NewGuid():N}"[..24];

        var created = await administrator.PostAsJsonAsync("/api/wards", new
        {
            name,
            ward_type = wardType,
            gender_policy = "mixed",
            is_active = true
        });

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        using var body = await ReadJsonAsync(created);
        return new TestWard(Guid.Parse(body.RootElement.GetProperty("id").GetString()!), name);
    }

    private async Task<(Guid Id, string Number)> AddBedAsync(TestWard ward)
    {
        using var equipment = await ClientAsync(ApiApplication.EquipmentEmail);
        var number = $"D{Guid.NewGuid():N}"[..6];

        var created = await equipment.PostAsJsonAsync("/api/beds", new
        {
            ward_id = ward.Id,
            bed_number = number,
            has_isolation = false
        });

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        using var body = await ReadJsonAsync(created);
        return (Guid.Parse(body.RootElement.GetProperty("id").GetString()!), number);
    }

    private async Task<string> NewAdmissionAsync(string category)
    {
        using var nurse = await ClientAsync(ApiApplication.NurseEmail);

        var patient = await nurse.PostAsJsonAsync("/api/patients", new
        {
            full_name = $"Bill Patient {Guid.NewGuid():N}"[..28],
            gender = "male",
            nic = $"G{Guid.NewGuid():N}"[..12]
        });

        Assert.Equal(HttpStatusCode.Created, patient.StatusCode);

        using var patientBody = await ReadJsonAsync(patient);

        var created = await nurse.PostAsJsonAsync("/api/admissions", new
        {
            patient_id = patientBody.RootElement.GetProperty("id").GetString(),
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
