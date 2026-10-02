using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace CareLanka.Api.Tests;

[Collection(ApiCollection.Name)]
public sealed class PatientAppointmentSlotTests
{
    private readonly ApiApplication _application;

    public PatientAppointmentSlotTests(ApiApplication application) => _application = application;

    [Fact]
    [Trait("id", "PT-TC-APPT-01")]
    public async Task Two_patients_can_book_exactly_the_same_time()
    {
        using var nurse = await NurseClientAsync();
        var day = DateTimeOffset.UtcNow.AddDays(Random.Shared.Next(4500, 9000)).Date;
        var slot = new DateTimeOffset(DateTime.SpecifyKind(day.AddHours(10), DateTimeKind.Utc));
        var first = await NewPatientAsync(nurse, "QM Test Slot A");
        var second = await NewPatientAsync(nurse, "QM Test Slot B");

        var firstBooking = await BookAsync(nurse, first, slot);
        var secondBooking = await BookAsync(nurse, second, slot);

        Assert.Equal(HttpStatusCode.Created, firstBooking.StatusCode);
        Assert.Equal(HttpStatusCode.Created, secondBooking.StatusCode);

        using var list = JsonDocument.Parse(await (await nurse
            .GetAsync($"/api/appointments?date={slot:yyyy-MM-dd}")).Content.ReadAsStringAsync());
        var atSlot = list.RootElement.GetProperty("items").EnumerateArray()
            .Where(item => item.GetProperty("scheduled_at").GetDateTimeOffset() == slot)
            .ToList();

        Assert.Equal(2, atSlot.Count);
    }

    private static Task<HttpResponseMessage> BookAsync(HttpClient client, string patientId, DateTimeOffset scheduledAt)
        => client.PostAsJsonAsync("/api/appointments", new { patient_id = patientId, scheduled_at = scheduledAt });

    private static async Task<string> NewPatientAsync(HttpClient client, string fullName)
    {
        var created = await client.PostAsJsonAsync("/api/patients", new
        {
            full_name = fullName,
            gender = "male",
            nic = $"P{Guid.NewGuid():N}"[..12]
        });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        using var body = JsonDocument.Parse(await created.Content.ReadAsStringAsync());
        return body.RootElement.GetProperty("id").GetString()!;
    }

    private async Task<HttpClient> NurseClientAsync()
    {
        using var anonymous = _application.CreateClient();
        var login = await anonymous.PostAsJsonAsync(
            "/api/auth/login", new { email = ApiApplication.NurseEmail, password = ApiApplication.Password });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);

        using var body = JsonDocument.Parse(await login.Content.ReadAsStringAsync());
        var client = _application.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", body.RootElement.GetProperty("access_token").GetString());
        return client;
    }
}
