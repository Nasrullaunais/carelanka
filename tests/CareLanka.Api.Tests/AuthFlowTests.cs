using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace CareLanka.Api.Tests;

[Collection(ApiCollection.Name)]
public sealed class AuthFlowTests
{
    private readonly ApiApplication _application;

    public AuthFlowTests(ApiApplication application) => _application = application;

    [Fact]
    public async Task Seeded_staff_member_can_log_in()
    {
        using var client = _application.CreateClient();

        var response = await LoginAsync(client, ApiApplication.NurseEmail, ApiApplication.Password);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = await ReadJsonAsync(response);
        Assert.False(string.IsNullOrWhiteSpace(body.RootElement.GetProperty("access_token").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(body.RootElement.GetProperty("refresh_token").GetString()));
    }

    [Theory]
    [InlineData(ApiApplication.NurseEmail, "wrong-password")]
    [InlineData("unknown.tests@carelanka.invalid", ApiApplication.Password)]
    [InlineData(ApiApplication.InactiveEmail, ApiApplication.Password)]
    public async Task Invalid_staff_credentials_return_the_same_401(string email, string password)
    {
        using var client = _application.CreateClient();

        var response = await LoginAsync(client, email, password);
        using var body = await ReadJsonAsync(response);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("cl_err_401", body.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Duty_manager_policy_returns_401_403_and_200_for_the_three_access_cases()
    {
        using var client = _application.CreateClient();

        var withoutToken = await client.GetAsync("/api/test/policy");
        var nurseToken = await AccessTokenAsync(client, ApiApplication.NurseEmail);
        var managerToken = await AccessTokenAsync(client, ApiApplication.ManagerEmail);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", nurseToken);
        var wrongRole = await client.GetAsync("/api/test/policy");

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", managerToken);
        var rightRole = await client.GetAsync("/api/test/policy");

        Assert.Equal(HttpStatusCode.Unauthorized, withoutToken.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, wrongRole.StatusCode);
        Assert.Equal(HttpStatusCode.OK, rightRole.StatusCode);
    }

    [Fact]
    public async Task Refresh_rotates_tokens_and_reuse_kills_the_replacement()
    {
        using var client = _application.CreateClient();
        using var login = await ReadJsonAsync(await LoginAsync(
            client, ApiApplication.NurseEmail, ApiApplication.Password));
        var firstRefresh = login.RootElement.GetProperty("refresh_token").GetString()!;

        var rotatedResponse = await RefreshAsync(client, firstRefresh);
        using var rotated = await ReadJsonAsync(rotatedResponse);
        var replacementRefresh = rotated.RootElement.GetProperty("refresh_token").GetString()!;

        var reuse = await RefreshAsync(client, firstRefresh);
        var killedReplacement = await RefreshAsync(client, replacementRefresh);

        Assert.Equal(HttpStatusCode.OK, rotatedResponse.StatusCode);
        Assert.NotEqual(firstRefresh, replacementRefresh);
        Assert.Equal(HttpStatusCode.Unauthorized, reuse.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, killedReplacement.StatusCode);
    }

    [Fact]
    public async Task Logout_revokes_the_refresh_token()
    {
        using var client = _application.CreateClient();
        using var login = await ReadJsonAsync(await LoginAsync(
            client, ApiApplication.ManagerEmail, ApiApplication.Password));
        var accessToken = login.RootElement.GetProperty("access_token").GetString()!;
        var refreshToken = login.RootElement.GetProperty("refresh_token").GetString()!;

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        var logout = await client.PostAsJsonAsync("/api/auth/logout", new { refresh_token = refreshToken });
        client.DefaultRequestHeaders.Authorization = null;
        var refresh = await RefreshAsync(client, refreshToken);

        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, refresh.StatusCode);
    }

    [Fact]
    public async Task Patient_can_register_log_in_and_read_an_unlinked_principal()
    {
        using var client = _application.CreateClient();
        var phone = NewPhoneNumber("77");

        var registration = await client.PostAsJsonAsync("/api/auth/patient/register", new
        {
            phone_number = phone,
            password = ApiApplication.Password,
            full_name = "Integration Patient"
        });
        var login = await client.PostAsJsonAsync("/api/auth/patient/login", new
        {
            phone_number = phone,
            password = ApiApplication.Password
        });
        using var loginBody = await ReadJsonAsync(login);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", loginBody.RootElement.GetProperty("access_token").GetString());
        var me = await client.GetAsync("/api/auth/me");
        using var principal = await ReadJsonAsync(me);

        Assert.Equal(HttpStatusCode.Created, registration.StatusCode);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
        Assert.Equal("patient", principal.RootElement.GetProperty("principal_type").GetString());
        Assert.Equal(JsonValueKind.Null, principal.RootElement.GetProperty("patient_id").ValueKind);
    }

    [Fact]
    public async Task Concurrent_registration_of_one_phone_returns_one_201_and_one_409()
    {
        using var firstClient = _application.CreateClient();
        using var secondClient = _application.CreateClient();
        var phone = NewPhoneNumber("71");
        var body = new
        {
            phone_number = phone,
            password = ApiApplication.Password,
            full_name = "Concurrent Patient"
        };

        var responses = await Task.WhenAll(
            firstClient.PostAsJsonAsync("/api/auth/patient/register", body),
            secondClient.PostAsJsonAsync("/api/auth/patient/register", body));

        Assert.Equal(
            new[] { HttpStatusCode.Created, HttpStatusCode.Conflict },
            responses.Select(response => response.StatusCode).Order().ToArray());
        Assert.Equal("application/problem+json",
            responses.Single(response => response.StatusCode == HttpStatusCode.Conflict)
                .Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Failed_login_does_not_expose_password_or_signing_key()
    {
        using var client = _application.CreateClient();
        const string submittedPassword = "NeverLogThis#2026";

        var response = await LoginAsync(client, ApiApplication.NurseEmail, submittedPassword);
        var responseBody = await response.Content.ReadAsStringAsync();
        var logOutput = string.Join(Environment.NewLine, _application.Logs);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.DoesNotContain(submittedPassword, responseBody);
        Assert.DoesNotContain(ApiApplication.SigningKey, responseBody);
        Assert.DoesNotContain(submittedPassword, logOutput);
        Assert.DoesNotContain(ApiApplication.SigningKey, logOutput);
    }

    private static Task<HttpResponseMessage> LoginAsync(HttpClient client, string email, string password)
        => client.PostAsJsonAsync("/api/auth/login", new { email, password });

    private static Task<HttpResponseMessage> RefreshAsync(HttpClient client, string refreshToken)
        => client.PostAsJsonAsync("/api/auth/refresh", new { refresh_token = refreshToken });

    private static async Task<string> AccessTokenAsync(HttpClient client, string email)
    {
        using var body = await ReadJsonAsync(await LoginAsync(client, email, ApiApplication.Password));
        return body.RootElement.GetProperty("access_token").GetString()!;
    }

    private static async Task<JsonDocument> ReadJsonAsync(HttpResponseMessage response)
        => JsonDocument.Parse(await response.Content.ReadAsStringAsync());

    private static string NewPhoneNumber(string prefix)
        => $"+94{prefix}{Random.Shared.Next(10_000_000, 99_999_999)}";
}
