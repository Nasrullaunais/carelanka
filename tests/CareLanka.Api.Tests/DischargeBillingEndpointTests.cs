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
/// Step 7 — the discharge checklist and confirmation — and the billing that sits behind
/// <c>billing_settled</c>.
/// </summary>
/// <remarks>
/// The two are one test class on purpose. The whole point of the design is that settling a bill
/// and ticking that box are a single act, so a test that could pass with them apart would be
/// testing the wrong thing.
///
/// Every test makes its own ward, its own beds and its own patient. The fixture's database is
/// shared by the whole collection, so nothing here asserts on the shape of a whole list.
/// </remarks>
[Collection(ApiCollection.Name)]
public sealed class DischargeBillingEndpointTests
{
    private readonly ApiApplication _application;

    public DischargeBillingEndpointTests(ApiApplication application) => _application = application;

    // ---------- who may tick what ----------

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

        // This is the wall. Not "a senior person" and not "anyone on the ward" — the one role
        // that can say a patient is medically well enough to leave.
        Assert.Equal(HttpStatusCode.Forbidden, byNurse.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, byManager.StatusCode);
        Assert.Equal(HttpStatusCode.OK, byDoctor.StatusCode);

        // The code the nurse's refusal carries. It used to be asserted by a test about the
        // ward nurse's own boxes, and there are no ward nurse boxes any more.
        using var refusal = await ReadJsonAsync(byNurse);
        Assert.Equal("cl_pat_022", refusal.RootElement.GetProperty("code").GetString());

        using var body = await ReadJsonAsync(byDoctor);
        Assert.True(Checklist(body, "clinical_clearance").GetProperty("ticked").GetBoolean());

        // The doctor's name travels with the tick. An audit trail of bare ids is one nobody
        // reads, and this box is the one somebody will be asked to account for.
        Assert.Equal(
            "Doctor Test",
            Checklist(body, "clinical_clearance").GetProperty("ticked_by_staff_name").GetString());
    }

    [Fact]
    public async Task Nobody_ticks_billing_settled_by_hand()
    {
        var visit = await AdmittedVisitAsync();

        // Every role, including the two that may actually settle the bill. The refusal is not
        // about permission - it is that there is one way to write this fact and this is not it.
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

    // ---------- flagging ----------

    [Fact]
    public async Task Ticking_the_last_mandatory_box_is_what_flags_the_patient()
    {
        var visit = await AdmittedVisitAsync();

        using var doctor = await ClientAsync(ApiApplication.DoctorEmail);
        using var reception = await ClientAsync(ApiApplication.ReceptionEmail);

        await TickAsync(doctor, visit.AdmissionId, new { clinical_clearance = true });

        // One of two. Still admitted, because the bill is the other one.
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

        // ready_for_discharge -> admitted is a published edge for exactly this: a doctor who
        // looks again and is no longer happy to let the patient go.
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

        // A patient with one box left is the person a nurse is looking for, so they are on the
        // list with the reason attached, not filtered off it.
        Assert.Equal(
            new[] { "billing_settled" },
            row.GetProperty("outstanding_items").EnumerateArray()
                .Select(item => item.GetString())
                .ToArray());
        Assert.Equal(visit.BedNumber, row.GetProperty("bed_number").GetString());
    }

    // ---------- the bill ----------

    [Fact]
    public async Task A_bill_is_the_care_level_and_the_bed_and_nothing_else_is_invented()
    {
        var visit = await AdmittedVisitAsync();

        using var reception = await ClientAsync(ApiApplication.ReceptionEmail);
        var prepared = await reception.PostAsync($"/api/admissions/{visit.AdmissionId}/bill", null);

        Assert.Equal(HttpStatusCode.OK, prepared.StatusCode);

        using var body = await ReadJsonAsync(prepared);
        var lines = body.RootElement.GetProperty("lines").EnumerateArray().ToList();

        // Exactly two, and the test names both amounts rather than reading them back out of the
        // response: an inpatient fee is 3000 and a general bed is 6000 a day, and a stay that
        // started minutes ago is one day because part of a day counts as a day.
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
    public async Task A_visit_with_no_bed_is_billed_the_fee_alone()
    {
        // An outpatient never enters the bed board, so there is nothing to price but the visit
        // itself. Worth its own test: the alternative - a zero bed line, or no bill at all -
        // would both be wrong in a way nobody would notice until a patient queried it.
        var admissionId = await NewAdmissionAsync(category: "outpatient");

        using var reception = await ClientAsync(ApiApplication.ReceptionEmail);
        using var body = await ReadJsonAsync(
            await reception.PostAsync($"/api/admissions/{admissionId}/bill", null));

        var lines = body.RootElement.GetProperty("lines").EnumerateArray().ToList();

        Assert.Single(lines);
        Assert.Equal("admission_fee", lines[0].GetProperty("source").GetString());
        Assert.Equal(1500m, body.RootElement.GetProperty("total").GetDecimal());
    }

    [Fact]
    public async Task A_second_day_in_the_bed_is_a_second_day_on_the_bill()
    {
        var visit = await AdmittedVisitAsync();

        // Backdated rather than waited for. Part of a day counts as a day and every stay counts
        // as at least one, so 30 hours is two.
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

        // Three lines, not four: the bed line was replaced rather than duplicated, and the
        // typed charge survived. That is the whole contract of "prepare again".
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

        // Read: the discharge screen shows whether the bill is settled, so the nurse has to be
        // able to see it. Write: taking money is the front desk's job and nobody else's.
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

        // It is the piece of paper the patient was handed. Adding to it afterwards would make
        // the paper and the database disagree, with the patient holding the paper.
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

        // Straight to settle, with no prepare. Without this a visit nobody prepared a bill for
        // could never tick billing_settled and therefore could never be discharged at all -
        // a deadlock the desk would have no way out of.
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

        // No bill row, and still on the list. A list of bills would have been empty and left
        // the work invisible, which is the reason this endpoint reads admissions instead.
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

        // Gone from the work-still-to-do list, which is the whole point of that list.
        Assert.Null(await OutstandingRowAsync(reception, visit.AdmissionId));

        // Still reachable, which is the whole point of includeSettled. Without it a patient who
        // asks for another copy of their bill at the counter cannot be served at all: they have
        // paid, so they are off the outstanding list, and they have gone home, so they are not
        // even an open visit any more. Found by walking the screen, not by a test.
        var row = await OutstandingRowAsync(reception, visit.AdmissionId, includeSettled: true);

        Assert.NotNull(row);
        Assert.True(row!.Value.GetProperty("settled").GetBoolean());
        Assert.Equal("discharged", row.Value.GetProperty("status").GetString());
        Assert.Equal(9000m, row.Value.GetProperty("estimated_total").GetDecimal());
        Assert.NotEqual(JsonValueKind.Null, row.Value.GetProperty("bill_number").ValueKind);

        // And the bill itself still reads, which is what the print view renders.
        using var bill = await ReadJsonAsync(
            await reception.GetAsync($"/api/admissions/{visit.AdmissionId}/bill"));

        Assert.True(bill.RootElement.GetProperty("settled").GetBoolean());
        Assert.Equal(9000m, bill.RootElement.GetProperty("total").GetDecimal());
    }

    // ---------- confirming ----------

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

        // The bed, read the way Equipment reads it before servicing one.
        using var occupancy = await ReadJsonAsync(
            await nurse.GetAsync($"/api/beds/{visit.BedId}/occupancy"));

        Assert.False(occupancy.RootElement.GetProperty("occupied").GetBoolean());
    }

    [Fact]
    public async Task A_nurse_may_confirm_an_icu_discharge()
    {
        var icu = await ReadyToGoAsync(wardType: "icu", category: "icu");

        using var nurse = await ClientAsync(ApiApplication.NurseEmail);
        var confirmed = await nurse.PostAsJsonAsync(
            $"/api/discharges/{icu.AdmissionId}/confirm", new { });

        // Changed 2026-09-12: this used to be a 403 with cl_pat_024. The care level no longer
        // decides who may confirm, because the checklist is the gate - and getting this far
        // means a doctor has already ticked clinical_clearance.
        Assert.Equal(HttpStatusCode.OK, confirmed.StatusCode);
    }

    [Fact]
    public async Task Reception_may_confirm_a_discharge()
    {
        var visit = await ReadyToGoAsync();

        using var reception = await ClientAsync(ApiApplication.ReceptionEmail);
        var confirmed = await reception.PostAsJsonAsync(
            $"/api/discharges/{visit.AdmissionId}/confirm", new { });

        // Reception settles the bill and hands over the paperwork on this same screen. Fetching
        // a nurse for the last click was the only thing it could not do.
        Assert.Equal(HttpStatusCode.OK, confirmed.StatusCode);
    }

    [Fact]
    public async Task A_doctor_does_not_confirm_a_discharge()
    {
        var visit = await ReadyToGoAsync();

        using var doctor = await ClientAsync(ApiApplication.DoctorEmail);
        var refused = await doctor.PostAsJsonAsync(
            $"/api/discharges/{visit.AdmissionId}/confirm", new { });

        // Widening the policy did not open it to everybody. A doctor's part is the clinical
        // clearance box; sending the patient home is desk and ward work.
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

        // Off the work list, which is right: a patient who has gone home is not somebody a
        // nurse is trying to get home.
        Assert.Null(await FindCandidateAsync(nurse, visit.AdmissionId, includeDischarged: false));

        // But still findable, which is the point of includeDischarged. Without it the discharge
        // screen empties itself the moment the work is done and keeps no record of any of it -
        // you confirm a discharge, the row disappears, and there is nowhere left to look up
        // what just happened.
        var record = await FindCandidateAsync(nurse, visit.AdmissionId, includeDischarged: true);

        Assert.NotNull(record);
        Assert.True(record!.Value.GetProperty("is_discharged").GetBoolean());

        // The time they left is on the record. It is the one fact somebody looking this up
        // afterwards actually wants, and "outstanding items" is empty by definition here - it
        // could not have been confirmed otherwise.
        Assert.NotEqual(
            JsonValueKind.Null, record.Value.GetProperty("discharged_at").ValueKind);

        Assert.Empty(record.Value.GetProperty("outstanding_items").EnumerateArray());
    }

    [Fact]
    public async Task A_finished_stay_counts_days_to_when_they_left_and_not_to_now()
    {
        var visit = await AdmittedVisitAsync();

        // Admitted ten days ago, then home five days ago. A record read today must say five,
        // not ten: the stay stopped when they left.
        //
        // AdmittedAt and not the bed's OccupiedAt, because days_in_bed is counted from the
        // admission - the bill is the thing counted from the bed.
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

        // 5.5 rather than 5. The two backdates are taken from two UtcNow calls milliseconds
        // apart, so an exact five-day gap lands a hair over five days - and part of a day
        // counts as a day, which rounds it to six. Half a day of slack keeps the test about
        // the rule rather than about the clock.
        await BackdateDischargeAsync(visit.AdmissionId, TimeSpan.FromDays(5.5));

        var record = await FindCandidateAsync(nurse, visit.AdmissionId, includeDischarged: true);

        // Admitted ten days ago, left four and a half days later: five billable days. Counted
        // to now instead it would be ten, and it would grow by one every day nobody touched
        // it - a number on a record that changes while nothing happens is worse than none.
        Assert.Equal(5, record!.Value.GetProperty("days_in_bed").GetInt32());
    }

    /// <summary>Moves the start of a visit into the past, which no endpoint offers.</summary>
    private async Task BackdateAdmissionAsync(string admissionId, TimeSpan by)
    {
        using var scope = _application.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CareLankaDbContext>();

        var id = Guid.Parse(admissionId);

        var admission = await db.Admissions.FirstAsync(row => row.Id == id);
        admission.AdmittedAt = DateTimeOffset.UtcNow - by;

        await db.SaveChangesAsync();
    }

    /// <summary>
    /// Moves a confirmed discharge into the past. No endpoint offers this and no clock in a
    /// test can wait for it.
    /// </summary>
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

        // Discharging RELEASES the bed assignment, so a live-only lookup finds nothing and the
        // screen said "No bed" about somebody who had just spent three days in GEN-02. That is
        // not a missing value, it is the wrong answer to the question a record asks - "where
        // were they?", not "where are they now?".
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

        // A bill is a document handed across a counter, and "who do I ask about this charge?"
        // is the first question at the desk. Recorded when the bill is opened, not when it is
        // paid - those can be two different people and often are.
        // The seeded reception account is FirstName = the role, LastName = "Test".
        Assert.Equal(
            "GeneralStaff Test",
            body.RootElement.GetProperty("raised_by_staff_name").GetString());
    }

    // ---------- helpers ----------

    private sealed record TestVisit(string AdmissionId, Guid BedId, string BedNumber);

    /// <summary>A patient in a bed on a ward of this test's own, ready for a checklist.</summary>
    private async Task<TestVisit> AdmittedVisitAsync(
        string wardType = "general", string category = "inpatient")
    {
        var ward = await NewWardAsync(wardType);
        var (bedId, bedNumber) = await AddBedAsync(ward);

        var admissionId = await NewAdmissionAsync(category);

        // An ICU or HDU bed is the duty manager's to give, and arrival is the ward nurse's to
        // record. Two different people, which is the point of the split, so the helper uses
        // two different tokens rather than quietly picking one that works everywhere.
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

    /// <summary>Every mandatory box ticked, by the roles that actually tick them.</summary>
    private async Task<TestVisit> ReadyToGoAsync(
        string wardType = "general", string category = "inpatient")
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

        // Cloned: the JsonDocument dies at the end of this method and every JsonElement taken
        // from it dies with it, which reads as an ObjectDisposedException in the caller.
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

    /// <summary>
    /// Pushes the start of a stay backwards, which no endpoint offers and no clock in a test can
    /// wait for. The only way to bill a second day without sleeping for thirty hours.
    /// </summary>
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

    /// <summary>Registers a real bed through Equipment Management's own endpoint. Their table, their write.</summary>
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
