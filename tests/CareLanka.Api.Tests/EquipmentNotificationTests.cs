using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using CareLanka.Api.Data.Enums;
using Xunit;

namespace CareLanka.Api.Tests;

[Collection(ApiCollection.Name)]
public sealed class EquipmentNotificationTests
{
    private static readonly byte[] Jpeg = [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46];
    private static readonly byte[] Pdf = Encoding.ASCII.GetBytes("%PDF-1.4\n%%EOF\n");

    private readonly NotificationTestKit _kit;

    public EquipmentNotificationTests(ApiApplication application) => _kit = new NotificationTestKit(application);

    [Fact]
    public async Task A_prescription_marked_ready_tells_the_patient()
    {
        using var pharmacy = await _kit.StaffAsync(ApiApplication.EquipmentEmail);
        var patient = await _kit.NewPatientAsync();
        var prescriptionId = await UploadPrescriptionAsync(patient);

        var ready = await pharmacy.PostAsync($"/api/prescriptions/{prescriptionId}/ready", null);
        Assert.Equal(HttpStatusCode.OK, ready.StatusCode);

        await _kit.AssertSentAsync(NotificationType.PrescriptionReady, prescriptionId,
            staff: [], patientAccounts: [patient.AccountId],
            actorStaffId: await _kit.StaffIdAsync(ApiApplication.EquipmentEmail));
    }

    [Fact]
    public async Task A_prescription_handed_over_tells_the_patient()
    {
        using var pharmacy = await _kit.StaffAsync(ApiApplication.EquipmentEmail);
        var patient = await _kit.NewPatientAsync();
        var prescriptionId = await UploadPrescriptionAsync(patient);
        await pharmacy.PostAsync($"/api/prescriptions/{prescriptionId}/ready", null);

        var delivered = await pharmacy.PostAsync($"/api/prescriptions/{prescriptionId}/deliver", null);
        Assert.Equal(HttpStatusCode.OK, delivered.StatusCode);

        await _kit.AssertSentAsync(NotificationType.PrescriptionDelivered, prescriptionId,
            staff: [], patientAccounts: [patient.AccountId],
            actorStaffId: await _kit.StaffIdAsync(ApiApplication.EquipmentEmail));
    }

    [Fact]
    public async Task Filing_a_lab_report_tells_the_patient()
    {
        using var lab = await _kit.StaffAsync(ApiApplication.EquipmentEmail);
        var patient = await _kit.NewPatientAsync();

        using var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(Pdf);
        file.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        form.Add(new StringContent(patient.PatientId.ToString()), "PatientId");
        form.Add(new StringContent("Full blood count"), "TestName");
        form.Add(file, "File", "report.pdf");

        var filed = await lab.PostAsync("/api/lab-reports", form);
        Assert.Equal(HttpStatusCode.Created, filed.StatusCode);
        using var body = JsonDocument.Parse(await filed.Content.ReadAsStringAsync());

        await _kit.AssertSentAsync(NotificationType.LabReportReady, body.RootElement.GetProperty("id").GetGuid(),
            staff: [], patientAccounts: [patient.AccountId],
            actorStaffId: await _kit.StaffIdAsync(ApiApplication.EquipmentEmail));
    }

    [Fact]
    public async Task A_stock_sweep_that_raises_warnings_sends_one_alert_to_the_other_equipment_managers()
    {
        using var sweeper = await _kit.StaffAsync(ApiApplication.EquipmentEmail);
        var sweeperId = await _kit.StaffIdAsync(ApiApplication.EquipmentEmail);
        await _kit.SeedStaffAsync(StaffRole.EquipmentManager);
        await NewLowStockMedicineAsync(sweeper);
        var startedAt = DateTimeOffset.UtcNow;

        var swept = await sweeper.PostAsync("/api/warnings/sweep", null);
        Assert.Equal(HttpStatusCode.OK, swept.StatusCode);

        await _kit.AssertSentAsync(
            n => n.EntityType == "warning_sweep" && n.CreatedAt >= startedAt,
            NotificationType.EquipmentWarningRaised,
            staff: await _kit.RoleAsync(StaffRole.EquipmentManager, sweeperId), patientAccounts: [],
            actorStaffId: sweeperId);
    }

    [Fact]
    public async Task A_stock_sweep_that_raises_nothing_new_sends_nothing()
    {
        using var sweeper = await _kit.StaffAsync(ApiApplication.EquipmentEmail);
        await NewLowStockMedicineAsync(sweeper);
        await sweeper.PostAsync("/api/warnings/sweep", null);
        var startedAt = DateTimeOffset.UtcNow;

        var second = await sweeper.PostAsync("/api/warnings/sweep", null);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);

        await _kit.AssertSentAsync(
            n => n.EntityType == "warning_sweep" && n.CreatedAt >= startedAt,
            NotificationType.EquipmentWarningRaised, staff: [], patientAccounts: []);
    }

    private static async Task<Guid> UploadPrescriptionAsync(NotificationTestKit.LinkedPatient patient)
    {
        var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(Jpeg);
        file.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
        form.Add(file, "File", "rx.jpg");

        var uploaded = await patient.Client.PostAsync("/api/me/prescriptions", form);
        Assert.Equal(HttpStatusCode.Created, uploaded.StatusCode);
        using var body = JsonDocument.Parse(await uploaded.Content.ReadAsStringAsync());

        return body.RootElement.GetProperty("id").GetGuid();
    }

    private static async Task NewLowStockMedicineAsync(HttpClient client)
    {
        var category = await client.PostAsJsonAsync("/api/pharmacy-categories", new
        {
            name = $"Category {Guid.NewGuid():N}"[..20],
            requires_prescription = false
        });
        using var categoryBody = JsonDocument.Parse(await category.Content.ReadAsStringAsync());

        var item = await client.PostAsJsonAsync("/api/pharmacy-items", new
        {
            name = $"Notify {Guid.NewGuid():N}"[..20],
            category_id = categoryBody.RootElement.GetProperty("id").GetGuid(),
            unit = "box",
            quantity_on_hand = 2,
            reorder_threshold = 10
        });
        Assert.Equal(HttpStatusCode.Created, item.StatusCode);
    }
}
