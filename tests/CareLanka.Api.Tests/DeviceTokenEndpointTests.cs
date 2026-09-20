using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using CareLanka.Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CareLanka.Api.Tests;

[Collection(ApiCollection.Name)]
public sealed class DeviceTokenEndpointTests
{
    private readonly ApiApplication _application;

    public DeviceTokenEndpointTests(ApiApplication application) => _application = application;

    [Fact]
    public async Task Registering_the_same_phone_twice_keeps_one_row()
    {
        using var crew = await ClientAsync(ApiApplication.AmbulanceEmail);
        var token = NewToken();

        var first = await Register(crew, token);
        var second = await Register(crew, token);

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal((await ReadAsync(first)).GetProperty("id").GetGuid(), (await ReadAsync(second)).GetProperty("id").GetGuid());
        Assert.Equal(1, await CountAsync(token));
    }

    [Fact]
    public async Task A_phone_handed_to_another_person_now_belongs_to_them()
    {
        using var crew = await ClientAsync(ApiApplication.AmbulanceEmail);
        using var nurse = await ClientAsync(ApiApplication.NurseEmail);
        var token = NewToken();

        await Register(crew, token);
        await Register(nurse, token);

        using var scope = _application.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CareLankaDbContext>();
        var owner = await db.DeviceTokens.Where(x => x.Token == token).Select(x => x.StaffMember.Email).SingleAsync();
        Assert.Equal(ApiApplication.NurseEmail, owner);
    }

    [Fact]
    public async Task Signing_out_of_a_phone_stops_pushes_and_registering_again_turns_them_back_on()
    {
        using var crew = await ClientAsync(ApiApplication.AmbulanceEmail);
        var token = NewToken();
        var id = (await ReadAsync(await Register(crew, token))).GetProperty("id").GetGuid();

        Assert.Equal(HttpStatusCode.NoContent, (await crew.DeleteAsync($"/api/device-tokens/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await crew.DeleteAsync($"/api/device-tokens/{id}")).StatusCode);
        Assert.NotNull(await RevokedAtAsync(token));

        await Register(crew, token);
        Assert.Null(await RevokedAtAsync(token));
    }

    [Fact]
    public async Task Nobody_can_remove_someone_elses_phone()
    {
        using var crew = await ClientAsync(ApiApplication.AmbulanceEmail);
        using var nurse = await ClientAsync(ApiApplication.NurseEmail);
        var id = (await ReadAsync(await Register(crew, NewToken()))).GetProperty("id").GetGuid();

        Assert.Equal(HttpStatusCode.NotFound, (await nurse.DeleteAsync($"/api/device-tokens/{id}")).StatusCode);
    }

    [Fact]
    public async Task An_empty_token_or_unknown_platform_is_rejected_and_anonymous_callers_are_kept_out()
    {
        using var crew = await ClientAsync(ApiApplication.AmbulanceEmail);
        Assert.Equal(HttpStatusCode.BadRequest, (await crew.PutAsJsonAsync("/api/device-tokens", new { token = "", platform = "android" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await crew.PutAsJsonAsync("/api/device-tokens", new { token = "abc", platform = "toaster" })).StatusCode);
        using var anonymous = _application.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await Register(anonymous, "abc")).StatusCode);
    }

    private static string NewToken() => $"device-{Guid.NewGuid():N}";

    private static Task<HttpResponseMessage> Register(HttpClient client, string token)
        => client.PutAsJsonAsync("/api/device-tokens", new { token, platform = "android" });

    private async Task<int> CountAsync(string token)
    {
        using var scope = _application.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<CareLankaDbContext>().DeviceTokens.CountAsync(x => x.Token == token);
    }

    private async Task<DateTimeOffset?> RevokedAtAsync(string token)
    {
        using var scope = _application.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<CareLankaDbContext>()
            .DeviceTokens.Where(x => x.Token == token).Select(x => x.RevokedAt).SingleAsync();
    }

    private async Task<HttpClient> ClientAsync(string email)
    {
        var client = _application.CreateClient();
        using var login = await client.PostAsJsonAsync("/api/auth/login", new { email, password = ApiApplication.Password });
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", (await ReadAsync(login)).GetProperty("access_token").GetString());
        return client;
    }

    private static async Task<JsonElement> ReadAsync(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.Clone();
    }
}
