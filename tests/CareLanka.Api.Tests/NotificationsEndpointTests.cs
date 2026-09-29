using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using CareLanka.Api.Data;
using CareLanka.Api.Data.Entities.Common;
using CareLanka.Api.Data.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CareLanka.Api.Tests;

[Collection(ApiCollection.Name)]
public sealed class NotificationsEndpointTests
{
    private readonly ApiApplication _application;

    public NotificationsEndpointTests(ApiApplication application) => _application = application;

    [Fact]
    public async Task A_patient_sees_only_their_own_notifications()
    {
        var (me, meId) = await NewPatientAsync();
        var (_, otherId) = await NewPatientAsync();

        await SeedNotificationAsync(patientAccountId: meId, title: "Mine one");
        await SeedNotificationAsync(patientAccountId: meId, title: "Mine two");
        await SeedNotificationAsync(patientAccountId: otherId, title: "Not mine");

        using var body = await ReadJsonAsync(await me.GetAsync("/api/notifications?pageSize=100"));
        var titles = body.RootElement.GetProperty("items").EnumerateArray()
            .Select(item => item.GetProperty("title").GetString())
            .ToList();

        Assert.Equal(2, titles.Count);
        Assert.Contains("Mine one", titles);
        Assert.Contains("Mine two", titles);
        Assert.DoesNotContain("Not mine", titles);
    }

    [Fact]
    public async Task Reading_someone_elses_notification_by_id_is_a_404_not_a_403()
    {
        var (_, otherId) = await NewPatientAsync();
        var (me, _) = await NewPatientAsync();
        var notificationId = await SeedNotificationAsync(patientAccountId: otherId, title: "Someone else's");

        var response = await me.PostAsync($"/api/notifications/{notificationId}/read", null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task An_invalid_page_size_is_refused_before_it_reaches_the_service()
    {
        var (me, _) = await NewPatientAsync();

        var response = await me.GetAsync("/api/notifications?pageSize=0");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task The_unread_count_tracks_reads_one_at_a_time_and_all_at_once()
    {
        var (me, meId) = await NewPatientAsync();
        var first = await SeedNotificationAsync(patientAccountId: meId, title: "First");
        await SeedNotificationAsync(patientAccountId: meId, title: "Second");
        await SeedNotificationAsync(patientAccountId: meId, title: "Third");

        using var initialCount = await ReadJsonAsync(await me.GetAsync("/api/notifications/unread-count"));
        Assert.Equal(3, initialCount.RootElement.GetProperty("count").GetInt32());

        var marked = await me.PostAsync($"/api/notifications/{first}/read", null);
        using var markedBody = await ReadJsonAsync(marked);
        Assert.Equal(HttpStatusCode.OK, marked.StatusCode);
        Assert.NotEqual(JsonValueKind.Null, markedBody.RootElement.GetProperty("read_at").ValueKind);

        using var afterOne = await ReadJsonAsync(await me.GetAsync("/api/notifications/unread-count"));
        Assert.Equal(2, afterOne.RootElement.GetProperty("count").GetInt32());

        var markAll = await me.PostAsync("/api/notifications/read-all", null);
        using var markAllBody = await ReadJsonAsync(markAll);
        Assert.Equal(0, markAllBody.RootElement.GetProperty("count").GetInt32());

        using var afterAll = await ReadJsonAsync(await me.GetAsync("/api/notifications/unread-count"));
        Assert.Equal(0, afterAll.RootElement.GetProperty("count").GetInt32());
    }

    private async Task<(HttpClient Client, Guid PatientAccountId)> NewPatientAsync()
    {
        var client = _application.CreateClient();

        var registered = await client.PostAsJsonAsync("/api/auth/patient/register", new
        {
            username = $"inbox.patient.{Guid.NewGuid():N}",
            password = ApiApplication.Password
        });

        Assert.Equal(HttpStatusCode.Created, registered.StatusCode);

        using var body = await ReadJsonAsync(registered);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", body.RootElement.GetProperty("access_token").GetString());

        using var me = await ReadJsonAsync(await client.GetAsync("/api/auth/me"));
        var id = Guid.Parse(me.RootElement.GetProperty("id").GetString()!);

        return (client, id);
    }

    private async Task<Guid> SeedNotificationAsync(Guid patientAccountId, string title)
    {
        using var scope = _application.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CareLankaDbContext>();

        var notification = new Notification
        {
            Id = Guid.NewGuid(),
            RecipientPatientAccountId = patientAccountId,
            Type = NotificationType.DispatchAssigned,
            Title = title,
            Body = "body",
            DedupeKey = $"inbox-test:{Guid.NewGuid()}"
        };
        db.Notifications.Add(notification);
        await db.SaveChangesAsync();

        return notification.Id;
    }

    private static async Task<JsonDocument> ReadJsonAsync(HttpResponseMessage response)
        => JsonDocument.Parse(await response.Content.ReadAsStringAsync());
}
