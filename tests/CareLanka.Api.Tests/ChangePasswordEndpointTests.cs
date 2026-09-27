using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace CareLanka.Api.Tests;

[Collection(ApiCollection.Name)]
public sealed class ChangePasswordEndpointTests
{
    private const string NewPassword = "Brand-New-Pass1";

    private readonly ApiApplication _application;

    public ChangePasswordEndpointTests(ApiApplication application) => _application = application;

    [Fact]
    public async Task The_new_password_signs_in_the_old_one_does_not_and_every_device_is_signed_out()
    {
        var username = NewUsername();
        using var phone = _application.CreateClient();
        using var tablet = _application.CreateClient();

        var phoneSession = await RegisterAsync(phone, username);
        var tabletSession = await LoginAsync(tablet, username, ApiApplication.Password);
        Authorise(phone, phoneSession.AccessToken);

        var changed = await ChangeAsync(phone, ApiApplication.Password, NewPassword);

        Assert.Equal(HttpStatusCode.NoContent, changed.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await RefreshAsync(phoneSession.RefreshToken)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await RefreshAsync(tabletSession.RefreshToken)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await LoginResponseAsync(username, ApiApplication.Password)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await LoginResponseAsync(username, NewPassword)).StatusCode);
    }

    [Fact]
    public async Task A_wrong_current_password_is_a_400_that_keeps_the_session_and_the_old_password()
    {
        var username = NewUsername();
        using var client = _application.CreateClient();
        var session = await RegisterAsync(client, username);
        Authorise(client, session.AccessToken);

        var refused = await ChangeAsync(client, "Not-My-Password1", NewPassword);
        using var body = await ReadJsonAsync(refused);

        Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);
        Assert.Equal("application/problem+json", refused.Content.Headers.ContentType?.MediaType);
        Assert.Equal("cl_err_003", body.RootElement.GetProperty("code").GetString());
        Assert.Equal(HttpStatusCode.OK, (await RefreshAsync(session.RefreshToken)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await LoginResponseAsync(username, ApiApplication.Password)).StatusCode);
    }

    [Theory]
    [InlineData("", NewPassword)]
    [InlineData(ApiApplication.Password, "short")]
    [InlineData(ApiApplication.Password, ApiApplication.Password)]
    public async Task An_invalid_body_is_refused_before_the_password_is_checked(
        string currentPassword, string newPassword)
    {
        using var client = _application.CreateClient();
        var session = await RegisterAsync(client, NewUsername());
        Authorise(client, session.AccessToken);

        var refused = await ChangeAsync(client, currentPassword, newPassword);
        using var body = await ReadJsonAsync(refused);

        Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);
        Assert.Equal("cl_err_400", body.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Without_a_token_it_is_a_401()
    {
        using var client = _application.CreateClient();

        var refused = await ChangeAsync(client, ApiApplication.Password, NewPassword);

        Assert.Equal(HttpStatusCode.Unauthorized, refused.StatusCode);
    }

    private static Task<HttpResponseMessage> ChangeAsync(
        HttpClient client, string currentPassword, string newPassword)
        => client.PostAsJsonAsync("/api/auth/password", new
        {
            current_password = currentPassword,
            new_password = newPassword
        });

    private static async Task<(string AccessToken, string RefreshToken)> RegisterAsync(
        HttpClient client, string username)
    {
        var registered = await client.PostAsJsonAsync("/api/auth/patient/register", new
        {
            username,
            password = ApiApplication.Password
        });

        Assert.Equal(HttpStatusCode.Created, registered.StatusCode);

        return await TokensAsync(registered);
    }

    private static async Task<(string AccessToken, string RefreshToken)> LoginAsync(
        HttpClient client, string username, string password)
    {
        var login = await client.PostAsJsonAsync("/api/auth/patient/login", new { username, password });

        Assert.Equal(HttpStatusCode.OK, login.StatusCode);

        return await TokensAsync(login);
    }

    private async Task<HttpResponseMessage> LoginResponseAsync(string username, string password)
    {
        using var client = _application.CreateClient();

        return await client.PostAsJsonAsync("/api/auth/patient/login", new { username, password });
    }

    private async Task<HttpResponseMessage> RefreshAsync(string refreshToken)
    {
        using var client = _application.CreateClient();

        return await client.PostAsJsonAsync("/api/auth/refresh", new { refresh_token = refreshToken });
    }

    private static async Task<(string AccessToken, string RefreshToken)> TokensAsync(HttpResponseMessage response)
    {
        using var body = await ReadJsonAsync(response);

        return (
            body.RootElement.GetProperty("access_token").GetString()!,
            body.RootElement.GetProperty("refresh_token").GetString()!);
    }

    private static void Authorise(HttpClient client, string accessToken)
        => client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

    private static async Task<JsonDocument> ReadJsonAsync(HttpResponseMessage response)
        => JsonDocument.Parse(await response.Content.ReadAsStringAsync());

    private static string NewUsername()
        => $"patient.{Random.Shared.Next(10_000_000, 99_999_999)}";
}
