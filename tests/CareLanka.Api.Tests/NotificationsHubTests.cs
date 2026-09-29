using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using CareLanka.Api.Data;
using CareLanka.Api.Data.Entities.Common;
using CareLanka.Api.Data.Enums;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CareLanka.Api.Tests;

[Collection(ApiCollection.Name)]
public sealed class NotificationsHubTests
{
    private readonly ApiApplication _application;

    public NotificationsHubTests(ApiApplication application) => _application = application;

    [Fact]
    public async Task Creating_a_notification_announces_it_to_the_recipients_own_connection_only()
    {
        var (meToken, meId) = await NewPatientAsync();
        var (otherToken, otherId) = await NewPatientAsync();

        await using var mine = await ConnectAsync(meToken);
        await using var someoneElses = await ConnectAsync(otherToken);

        var mineReceived = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        mine.On("inboxChanged", () => mineReceived.TrySetResult(true));

        var otherReceived = 0;
        someoneElses.On("inboxChanged", () => Interlocked.Increment(ref otherReceived));

        // A notification for someone else must not reach my connection.
        await SeedNotificationAsync(otherId);
        await Task.Delay(TimeSpan.FromSeconds(1));
        Assert.False(mineReceived.Task.IsCompleted);

        // A notification for me does reach my connection - and never the other one.
        await SeedNotificationAsync(meId);
        var completed = await Task.WhenAny(mineReceived.Task, Task.Delay(TimeSpan.FromSeconds(10)));
        Assert.Same(mineReceived.Task, completed);
        Assert.True(await mineReceived.Task);
        Assert.Equal(1, otherReceived);
    }

    private async Task<HubConnection> ConnectAsync(string accessToken)
    {
        var connection = new HubConnectionBuilder()
            .WithUrl(new Uri(_application.Server.BaseAddress, "/api/hubs/notifications"), options =>
            {
                options.HttpMessageHandlerFactory = _ => _application.Server.CreateHandler();
                options.Transports = HttpTransportType.LongPolling;
                options.AccessTokenProvider = () => Task.FromResult<string?>(accessToken);
            })
            .Build();

        await connection.StartAsync();
        return connection;
    }

    private async Task<(string AccessToken, Guid PatientAccountId)> NewPatientAsync()
    {
        var client = _application.CreateClient();

        var registered = await client.PostAsJsonAsync("/api/auth/patient/register", new
        {
            username = $"hub.patient.{Guid.NewGuid():N}",
            password = ApiApplication.Password
        });

        Assert.Equal(HttpStatusCode.Created, registered.StatusCode);

        using var body = JsonDocument.Parse(await registered.Content.ReadAsStringAsync());
        var accessToken = body.RootElement.GetProperty("access_token").GetString()!;

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        using var me = JsonDocument.Parse(await (await client.GetAsync("/api/auth/me")).Content.ReadAsStringAsync());
        var id = Guid.Parse(me.RootElement.GetProperty("id").GetString()!);

        return (accessToken, id);
    }

    private async Task SeedNotificationAsync(Guid patientAccountId)
    {
        using var scope = _application.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CareLankaDbContext>();

        db.Notifications.Add(new Notification
        {
            Id = Guid.NewGuid(),
            RecipientPatientAccountId = patientAccountId,
            Type = NotificationType.DispatchAssigned,
            Title = "Hub test",
            Body = "body",
            DedupeKey = $"hub-test:{Guid.NewGuid()}"
        });
        await db.SaveChangesAsync();
    }
}
