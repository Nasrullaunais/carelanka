using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json;
using CareLanka.Api.Services.Patient;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace CareLanka.Api.Tests;

[Collection(ApiCollection.Name)]
public sealed class PatientAppAccountEndpointTests
{
    private const string NewPassword = "Chosen-By-Me1";

    private readonly ApiApplication _application;

    public PatientAppAccountEndpointTests(ApiApplication application) => _application = application;

    [Fact]
    public async Task The_list_shows_only_patients_with_an_app_account_and_finds_them_by_each_field()
    {
        var marker = NewMarker();
        var linked = await NewLinkedPatientAsync($"Linked {marker}");
        using var manager = await StaffClientAsync(ApiApplication.ManagerEmail);
        await CreatePatientAsync(manager, $"Unlinked {marker}", NewNic());
        using var desk = await StaffClientAsync(ApiApplication.ReceptionEmail);

        var byName = await ListAsync(desk, marker);

        var row = Assert.Single(byName.RootElement.GetProperty("items").EnumerateArray());
        Assert.Equal(linked.PatientId, row.GetProperty("patient_id").GetString());
        Assert.Equal(linked.PatientCode, row.GetProperty("patient_code").GetString());
        Assert.Equal($"Linked {marker}", row.GetProperty("full_name").GetString());
        Assert.Equal(linked.Nic, row.GetProperty("nic").GetString());
        Assert.Equal("1990-04-12", row.GetProperty("date_of_birth").GetString());
        Assert.Equal(linked.Username, row.GetProperty("username").GetString());

        foreach (var search in new[] { linked.Nic, linked.PatientCode.ToLowerInvariant(), linked.Username.ToUpperInvariant() })
        {
            using var found = await ListAsync(desk, search);

            Assert.Contains(
                linked.PatientId,
                found.RootElement.GetProperty("items").EnumerateArray()
                    .Select(item => item.GetProperty("patient_id").GetString()));
        }
    }

    [Fact]
    public async Task A_reset_replaces_the_old_password_and_flags_the_account()
    {
        var linked = await NewLinkedPatientAsync($"Reset {NewMarker()}");

        var temporary = await ResetAsync(linked.PatientId);

        Assert.Equal(HttpStatusCode.Unauthorized, (await PatientLoginAsync(linked.Username, ApiApplication.Password)).StatusCode);

        var login = await PatientLoginAsync(linked.Username, temporary);
        using var body = await ReadJsonAsync(login);

        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        Assert.True(body.RootElement.GetProperty("principal").GetProperty("must_change_password").GetBoolean());
    }

    [Fact]
    public async Task A_reset_ends_every_session_the_patient_held()
    {
        var linked = await NewLinkedPatientAsync($"Sessions {NewMarker()}");
        var second = await TokensAsync(await PatientLoginAsync(linked.Username, ApiApplication.Password));

        await ResetAsync(linked.PatientId);

        Assert.Equal(HttpStatusCode.Unauthorized, (await RefreshAsync(linked.RefreshToken)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await RefreshAsync(second.RefreshToken)).StatusCode);
    }

    [Fact]
    public async Task A_flagged_token_can_only_read_who_it_is_and_change_the_password()
    {
        var linked = await NewLinkedPatientAsync($"Flagged {NewMarker()}");
        var temporary = await ResetAsync(linked.PatientId);
        var session = await TokensAsync(await PatientLoginAsync(linked.Username, temporary));
        using var phone = BearerClient(session.AccessToken);

        var profile = await phone.GetAsync("/api/me/profile");
        using var refused = await ReadJsonAsync(profile);
        Assert.Equal(HttpStatusCode.Forbidden, profile.StatusCode);
        Assert.Equal("application/problem+json", profile.Content.Headers.ContentType?.MediaType);
        Assert.Equal("cl_err_004", refused.RootElement.GetProperty("code").GetString());

        var me = await phone.GetAsync("/api/auth/me");
        using var meBody = await ReadJsonAsync(me);
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
        Assert.True(meBody.RootElement.GetProperty("must_change_password").GetBoolean());

        var changed = await phone.PostAsJsonAsync("/api/auth/password", new
        {
            current_password = temporary,
            new_password = NewPassword
        });
        Assert.Equal(HttpStatusCode.NoContent, changed.StatusCode);
    }

    [Fact]
    public async Task After_choosing_a_new_password_the_flag_is_gone_and_the_app_works_again()
    {
        var linked = await NewLinkedPatientAsync($"Changed {NewMarker()}");
        var temporary = await ResetAsync(linked.PatientId);
        var flagged = await TokensAsync(await PatientLoginAsync(linked.Username, temporary));
        using var flaggedPhone = BearerClient(flagged.AccessToken);
        Assert.Equal(HttpStatusCode.NoContent, (await flaggedPhone.PostAsJsonAsync("/api/auth/password", new
        {
            current_password = temporary,
            new_password = NewPassword
        })).StatusCode);

        var login = await PatientLoginAsync(linked.Username, NewPassword);
        using var body = await ReadJsonAsync(login);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        Assert.False(body.RootElement.GetProperty("principal").GetProperty("must_change_password").GetBoolean());

        using var phone = BearerClient(body.RootElement.GetProperty("access_token").GetString()!);
        Assert.Equal(HttpStatusCode.OK, (await phone.GetAsync("/api/me/profile")).StatusCode);
    }

    [Fact]
    public async Task A_patient_with_no_app_account_is_a_409_and_an_unknown_patient_is_a_404()
    {
        using var manager = await StaffClientAsync(ApiApplication.ManagerEmail);
        var unlinked = await CreatePatientAsync(manager, $"No App {NewMarker()}", NewNic());
        using var desk = await StaffClientAsync(ApiApplication.ReceptionEmail);

        var conflict = await desk.PostAsync($"/api/patient-accounts/{unlinked.Id}/reset-password", null);
        using var conflictBody = await ReadJsonAsync(conflict);
        var missing = await desk.PostAsync($"/api/patient-accounts/{Guid.NewGuid()}/reset-password", null);

        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
        Assert.Equal("cl_pat_053", conflictBody.RootElement.GetProperty("code").GetString());
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
    }

    [Theory]
    [InlineData(ApiApplication.NurseEmail)]
    [InlineData(ApiApplication.DoctorEmail)]
    public async Task The_ward_nurse_and_the_doctor_cannot_use_either_route(string email)
    {
        var linked = await NewLinkedPatientAsync($"Ward {NewMarker()}");
        using var client = await StaffClientAsync(email);

        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/patient-accounts")).StatusCode);
        Assert.Equal(
            HttpStatusCode.Forbidden,
            (await client.PostAsync($"/api/patient-accounts/{linked.PatientId}/reset-password", null)).StatusCode);
    }

    [Fact]
    public async Task A_patient_token_is_a_403_and_no_token_is_a_401()
    {
        var linked = await NewLinkedPatientAsync($"Self {NewMarker()}");
        using var phone = BearerClient(linked.AccessToken);
        using var anonymous = _application.CreateClient();

        Assert.Equal(HttpStatusCode.Forbidden, (await phone.GetAsync("/api/patient-accounts")).StatusCode);
        Assert.Equal(
            HttpStatusCode.Forbidden,
            (await phone.PostAsync($"/api/patient-accounts/{linked.PatientId}/reset-password", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/patient-accounts")).StatusCode);
        Assert.Equal(
            HttpStatusCode.Unauthorized,
            (await anonymous.PostAsync($"/api/patient-accounts/{linked.PatientId}/reset-password", null)).StatusCode);
    }

    [Fact]
    public async Task A_patient_locked_out_by_wrong_guesses_can_sign_in_with_the_temporary_password()
    {
        var linked = await NewLinkedPatientAsync($"Locked {NewMarker()}");

        for (var attempt = 0; attempt < 5; attempt++)
        {
            await PatientLoginAsync(linked.Username, "Wrong-Guess-99");
        }

        Assert.Equal(
            HttpStatusCode.TooManyRequests,
            (await PatientLoginAsync(linked.Username, ApiApplication.Password)).StatusCode);

        var temporary = await ResetAsync(linked.PatientId);

        Assert.Equal(HttpStatusCode.OK, (await PatientLoginAsync(linked.Username, temporary)).StatusCode);
    }

    [Fact]
    public async Task The_temporary_password_has_no_look_alike_letters_and_is_new_every_time()
    {
        var linked = await NewLinkedPatientAsync($"Shape {NewMarker()}");

        var first = await ResetAsync(linked.PatientId);
        var second = await ResetAsync(linked.PatientId);

        Assert.Matches(@"^[A-HJKMNP-Z]{4}-\d{4}$", first);
        Assert.Matches(@"^[A-HJKMNP-Z]{4}-\d{4}$", second);
        Assert.NotEqual(first, second);
    }

    [Fact]
    public async Task The_reset_response_is_never_cached()
    {
        var linked = await NewLinkedPatientAsync($"Cache {NewMarker()}");
        using var desk = await StaffClientAsync(ApiApplication.ReceptionEmail);

        var response = await desk.PostAsync($"/api/patient-accounts/{linked.PatientId}/reset-password", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.CacheControl?.NoStore);
    }

    [Theory]
    [InlineData("page=0")]
    [InlineData("pageSize=101")]
    public async Task An_invalid_list_query_is_a_400_that_never_reaches_the_service(string query)
    {
        var tracker = TrackingPatientService.Create();
        using var application = _application.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IPatientService>();
                services.AddSingleton(tracker);
            }));
        using var client = application.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", await StaffTokenAsync(client, ApiApplication.ReceptionEmail));

        var response = await client.GetAsync($"/api/patient-accounts?{query}");
        using var body = await ReadJsonAsync(response);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("cl_err_400", body.RootElement.GetProperty("code").GetString());
        Assert.Empty(((TrackingPatientService)(object)tracker).Calls);
    }

    private async Task<LinkedPatient> NewLinkedPatientAsync(string fullName)
    {
        var username = $"reset.{Random.Shared.Next(10_000_000, 99_999_999)}";
        using var anonymous = _application.CreateClient();
        var registered = await anonymous.PostAsJsonAsync("/api/auth/patient/register", new
        {
            username,
            password = ApiApplication.Password
        });
        Assert.Equal(HttpStatusCode.Created, registered.StatusCode);
        using var account = await ReadJsonAsync(registered);

        using var manager = await StaffClientAsync(ApiApplication.ManagerEmail);
        var nic = NewNic();
        var patient = await CreatePatientAsync(manager, fullName, nic);
        var linked = await manager.PostAsJsonAsync($"/api/patients/{patient.Id}/link-account", new
        {
            user_account_id = account.RootElement.GetProperty("principal").GetProperty("id").GetGuid()
        });
        Assert.Equal(HttpStatusCode.NoContent, linked.StatusCode);

        return new LinkedPatient(
            patient.Id,
            patient.PatientCode,
            nic,
            username,
            account.RootElement.GetProperty("access_token").GetString()!,
            account.RootElement.GetProperty("refresh_token").GetString()!);
    }

    private static async Task<(string Id, string PatientCode)> CreatePatientAsync(
        HttpClient client, string fullName, string nic)
    {
        var created = await client.PostAsJsonAsync("/api/patients", new
        {
            full_name = fullName,
            nic,
            gender = "female",
            date_of_birth = "1990-04-12"
        });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        using var body = await ReadJsonAsync(created);

        return (
            body.RootElement.GetProperty("id").GetString()!,
            body.RootElement.GetProperty("patient_code").GetString()!);
    }

    private async Task<string> ResetAsync(string patientId)
    {
        using var desk = await StaffClientAsync(ApiApplication.ReceptionEmail);
        var response = await desk.PostAsync($"/api/patient-accounts/{patientId}/reset-password", null);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var body = await ReadJsonAsync(response);

        return body.RootElement.GetProperty("temporary_password").GetString()!;
    }

    private static async Task<JsonDocument> ListAsync(HttpClient client, string search)
    {
        var response = await client.GetAsync($"/api/patient-accounts?search={Uri.EscapeDataString(search)}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        return await ReadJsonAsync(response);
    }

    private async Task<HttpResponseMessage> PatientLoginAsync(string username, string password)
    {
        using var client = _application.CreateClient();

        return await client.PostAsJsonAsync("/api/auth/patient/login", new { username, password });
    }

    private async Task<HttpResponseMessage> RefreshAsync(string refreshToken)
    {
        using var client = _application.CreateClient();

        return await client.PostAsJsonAsync("/api/auth/refresh", new { refresh_token = refreshToken });
    }

    private async Task<HttpClient> StaffClientAsync(string email)
    {
        var client = _application.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", await StaffTokenAsync(client, email));

        return client;
    }

    private static async Task<string> StaffTokenAsync(HttpClient client, string email)
    {
        var login = await client.PostAsJsonAsync(
            "/api/auth/login", new { email, password = ApiApplication.Password });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);

        using var body = await ReadJsonAsync(login);

        return body.RootElement.GetProperty("access_token").GetString()!;
    }

    private HttpClient BearerClient(string accessToken)
    {
        var client = _application.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        return client;
    }

    private static async Task<(string AccessToken, string RefreshToken)> TokensAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = await ReadJsonAsync(response);

        return (
            body.RootElement.GetProperty("access_token").GetString()!,
            body.RootElement.GetProperty("refresh_token").GetString()!);
    }

    private static async Task<JsonDocument> ReadJsonAsync(HttpResponseMessage response)
        => JsonDocument.Parse(await response.Content.ReadAsStringAsync());

    private static string NewMarker() => Guid.NewGuid().ToString("N")[..10];

    private static string NewNic() => $"T{Guid.NewGuid():N}"[..12];

    private sealed record LinkedPatient(
        string PatientId,
        string PatientCode,
        string Nic,
        string Username,
        string AccessToken,
        string RefreshToken);

    public class TrackingPatientService : DispatchProxy
    {
        public List<string> Calls { get; } = new();

        public static IPatientService Create() => Create<IPatientService, TrackingPatientService>();

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            Calls.Add(targetMethod?.Name ?? "?");

            throw new InvalidOperationException("The service must not be reached.");
        }
    }
}
