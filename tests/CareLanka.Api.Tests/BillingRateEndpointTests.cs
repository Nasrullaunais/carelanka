using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace CareLanka.Api.Tests;

[Collection(ApiCollection.Name)]
public sealed class BillingRateEndpointTests
{
    private readonly ApiApplication _application;

    public BillingRateEndpointTests(ApiApplication application) => _application = application;

    [Fact]
    public async Task The_grid_is_complete_before_anybody_has_edited_it()
    {
        using var nurse = await ClientAsync(ApiApplication.NurseEmail);

        var response = await nurse.GetAsync("/api/billing/rates");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var body = await ReadJsonAsync(response);
        var root = body.RootElement;

        Assert.Equal("LKR", root.GetProperty("currency").GetString());

        var wards = root.GetProperty("wards").EnumerateArray().ToList();

        Assert.Equal(9, wards.Count);

        foreach (var ward in wards)
        {
            var keys = ward.GetProperty("expenses").EnumerateArray()
                .Select(expense => expense.GetProperty("expense_key").GetString())
                .ToList();

            Assert.Contains("bed_day", keys);
            Assert.Equal(7, keys.Count);
        }

        Assert.Equal(5, root.GetProperty("admission_fees").EnumerateArray().Count());
    }

    [Fact]
    public async Task Only_the_administrator_may_change_a_price()
    {
        var change = new
        {
            expenses = new[] { new { ward_type = "general", expense_key = "food", amount = 1_000m } },
            admission_fees = Array.Empty<object>()
        };

        foreach (var email in new[]
        {
            ApiApplication.ReceptionEmail,
            ApiApplication.NurseEmail,
            ApiApplication.ManagerEmail,
            ApiApplication.DoctorEmail,
        })
        {
            using var client = await ClientAsync(email);
            var refused = await client.PutAsJsonAsync("/api/billing/rates", change);

            Assert.Equal(HttpStatusCode.Forbidden, refused.StatusCode);
        }

        using var administrator = await ClientAsync(ApiApplication.AdministratorEmail);
        var allowed = await administrator.PutAsJsonAsync("/api/billing/rates", change);

        Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
    }

    [Fact]
    public async Task An_unknown_expense_is_refused_rather_than_stored()
    {
        using var administrator = await ClientAsync(ApiApplication.AdministratorEmail);

        var refused = await administrator.PutAsJsonAsync("/api/billing/rates", new
        {
            expenses = new[]
            {
                new { ward_type = "general", expense_key = "helicopter", amount = 500_000m }
            },
            admission_fees = Array.Empty<object>()
        });

        Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);
    }

    [Fact]
    public async Task A_negative_price_is_refused()
    {
        using var administrator = await ClientAsync(ApiApplication.AdministratorEmail);

        var refused = await administrator.PutAsJsonAsync("/api/billing/rates", new
        {
            expenses = new[]
            {
                new { ward_type = "general", expense_key = "food", amount = -50m }
            },
            admission_fees = Array.Empty<object>()
        });

        Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);
    }

    [Fact]
    public async Task A_new_bed_rate_prices_the_next_bill_and_leaves_the_last_one_alone()
    {
        using var administrator = await ClientAsync(ApiApplication.AdministratorEmail);
        using var reception = await ClientAsync(ApiApplication.ReceptionEmail);

        const string wardType = "mental_health";

        await SetBedRateAsync(administrator, wardType, 4_000m);

        var first = await AdmittedVisitAsync(wardType);
        var beforeTotal = await PrepareAsync(reception, first);

        await SetBedRateAsync(administrator, wardType, 9_000m);

        var second = await AdmittedVisitAsync(wardType);
        var afterTotal = await PrepareAsync(reception, second);

        Assert.Equal(afterTotal - beforeTotal, 5_000m);

        var reread = await BillTotalAsync(reception, first);
        Assert.Equal(beforeTotal, reread);
    }

    private static async Task SetBedRateAsync(HttpClient administrator, string wardType, decimal amount)
    {
        var saved = await administrator.PutAsJsonAsync("/api/billing/rates", new
        {
            expenses = new[] { new { ward_type = wardType, expense_key = "bed_day", amount } },
            admission_fees = Array.Empty<object>()
        });

        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
    }

    private static async Task<decimal> PrepareAsync(HttpClient reception, string admissionId)
    {
        var prepared = await reception.PostAsync($"/api/admissions/{admissionId}/bill", null);
        Assert.Equal(HttpStatusCode.OK, prepared.StatusCode);

        using var body = await ReadJsonAsync(prepared);
        return body.RootElement.GetProperty("total").GetDecimal();
    }

    private static async Task<decimal> BillTotalAsync(HttpClient reception, string admissionId)
    {
        var response = await reception.GetAsync($"/api/admissions/{admissionId}/bill");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var body = await ReadJsonAsync(response);
        return body.RootElement.GetProperty("total").GetDecimal();
    }

    private async Task<string> AdmittedVisitAsync(string wardType)
    {
        using var administrator = await ClientAsync(ApiApplication.AdministratorEmail);
        using var equipment = await ClientAsync(ApiApplication.EquipmentEmail);
        using var nurse = await ClientAsync(ApiApplication.NurseEmail);

        var wardName = $"Rate-Ward-{Guid.NewGuid():N}"[..24];

        var ward = await administrator.PostAsJsonAsync("/api/wards", new
        {
            name = wardName,
            ward_type = wardType,
            gender_policy = "mixed",
            is_active = true
        });

        Assert.Equal(HttpStatusCode.Created, ward.StatusCode);
        using var wardBody = await ReadJsonAsync(ward);
        var wardId = wardBody.RootElement.GetProperty("id").GetString();

        var bed = await equipment.PostAsJsonAsync("/api/beds", new
        {
            ward_id = wardId,
            bed_number = $"R{Guid.NewGuid():N}"[..6],
            has_isolation = false
        });

        Assert.Equal(HttpStatusCode.Created, bed.StatusCode);
        using var bedBody = await ReadJsonAsync(bed);
        var bedId = bedBody.RootElement.GetProperty("id").GetString();

        var patient = await nurse.PostAsJsonAsync("/api/patients", new
        {
            full_name = $"Rate Patient {Guid.NewGuid():N}"[..28],
            gender = "male",
            nic = $"R{Guid.NewGuid():N}"[..12]
        });

        Assert.Equal(HttpStatusCode.Created, patient.StatusCode);
        using var patientBody = await ReadJsonAsync(patient);

        var admission = await nurse.PostAsJsonAsync("/api/admissions", new
        {
            patient_id = patientBody.RootElement.GetProperty("id").GetString(),
            source = "walk_in",
            admission_category = "general",
            category_set_by_staff_id = await NurseIdAsync(),
            urgency = "routine",
            is_infectious = false
        });

        Assert.Equal(HttpStatusCode.Created, admission.StatusCode);
        using var admissionBody = await ReadJsonAsync(admission);
        var admissionId = admissionBody.RootElement.GetProperty("id").GetString()!;

        var assigned = await nurse.PostAsJsonAsync(
            $"/api/admissions/{admissionId}/assign-bed", new { bed_id = bedId });
        Assert.Equal(HttpStatusCode.OK, assigned.StatusCode);

        var arrived = await nurse.PostAsync($"/api/admissions/{admissionId}/arrive", null);
        Assert.Equal(HttpStatusCode.OK, arrived.StatusCode);

        return admissionId;
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

            var response = await client.PostAsJsonAsync(
                "/api/auth/login", new { email, password = ApiApplication.Password });

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
