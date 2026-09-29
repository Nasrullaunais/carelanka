using System.Linq.Expressions;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using CareLanka.Api.Data;
using CareLanka.Api.Data.Entities.Common;
using CareLanka.Api.Data.Enums;
using CareLanka.Api.Services.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CareLanka.Api.Tests;

/// <summary>
/// Shared set-up and the one assertion every notification test uses: exactly one notification
/// per intended person, nobody else, and never the person who did the action.
/// </summary>
internal sealed class NotificationTestKit
{
    private static readonly SemaphoreSlim TokenLock = new(1, 1);
    private static readonly Dictionary<string, string> Tokens = new();

    private readonly ApiApplication _application;

    public NotificationTestKit(ApiApplication application) => _application = application;

    public sealed record LinkedPatient(HttpClient Client, Guid PatientId, Guid AccountId, string Code);

    public async Task<HttpClient> StaffAsync(string email)
    {
        var client = _application.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await TokenAsync(email));
        return client;
    }

    public async Task<Guid> StaffIdAsync(string email)
    {
        using var scope = _application.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CareLankaDbContext>();
        return await db.StaffMembers.Where(s => s.Email == email).Select(s => s.Id).SingleAsync();
    }

    public async Task<(Guid Id, string Email)> SeedStaffAsync(StaffRole role)
    {
        using var scope = _application.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CareLankaDbContext>();
        var passwords = scope.ServiceProvider.GetRequiredService<IPasswordService>();

        var staff = new StaffMember
        {
            Id = Guid.NewGuid(),
            Email = $"notify-{Guid.NewGuid():N}@carelanka.invalid",
            PasswordHash = passwords.Hash(ApiApplication.Password),
            FirstName = role.ToString(),
            LastName = "Notify",
            Role = role,
            IsActive = true
        };
        db.StaffMembers.Add(staff);
        await db.SaveChangesAsync();

        return (staff.Id, staff.Email);
    }

    /// <summary>Everyone active in a role right now, minus whoever did the action.</summary>
    public async Task<Guid[]> RoleAsync(StaffRole role, params Guid[] except)
    {
        using var scope = _application.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CareLankaDbContext>();
        var ids = await db.StaffMembers.Where(s => s.IsActive && s.Role == role).Select(s => s.Id).ToListAsync();
        return ids.Except(except).ToArray();
    }

    /// <summary>A signed-in patient app user whose record is linked to their account.</summary>
    public async Task<LinkedPatient> NewPatientAsync()
    {
        var client = _application.CreateClient();

        var registered = await client.PostAsJsonAsync("/api/auth/patient/register", new
        {
            username = $"notify.patient.{Guid.NewGuid():N}",
            password = ApiApplication.Password
        });
        Assert.Equal(HttpStatusCode.Created, registered.StatusCode);

        using var registeredBody = JsonDocument.Parse(await registered.Content.ReadAsStringAsync());
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", registeredBody.RootElement.GetProperty("access_token").GetString());
        var accountId = registeredBody.RootElement.GetProperty("principal").GetProperty("id").GetGuid();

        var saved = await client.PostAsJsonAsync("/api/me/pre-register", new
        {
            nic = $"N{Guid.NewGuid():N}"[..12],
            full_name = $"Notify Patient {Guid.NewGuid():N}"[..24],
            gender = "female"
        });
        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);

        using var savedBody = JsonDocument.Parse(await saved.Content.ReadAsStringAsync());
        var code = savedBody.RootElement.GetProperty("patient_code").GetString()!;

        using var scope = _application.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CareLankaDbContext>();
        var patientId = await db.Patients.Where(p => p.PatientCode == code).Select(p => p.Id).SingleAsync();

        return new LinkedPatient(client, patientId, accountId, code);
    }

    public Task AssertSentAsync(
        NotificationType type,
        Guid entityId,
        IEnumerable<Guid> staff,
        IEnumerable<Guid> patientAccounts,
        Guid? actorStaffId = null,
        Guid? actorPatientAccountId = null)
        => AssertSentAsync(n => n.Type == type && n.EntityId == entityId, type, staff, patientAccounts, actorStaffId, actorPatientAccountId);

    public async Task AssertSentAsync(
        Expression<Func<Notification, bool>> which,
        NotificationType type,
        IEnumerable<Guid> staff,
        IEnumerable<Guid> patientAccounts,
        Guid? actorStaffId = null,
        Guid? actorPatientAccountId = null)
    {
        using var scope = _application.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CareLankaDbContext>();
        var sent = await db.Notifications.AsNoTracking().Where(which).Where(n => n.Type == type).ToListAsync();

        var expectedStaff = staff.OrderBy(id => id).ToList();
        var expectedPatients = patientAccounts.OrderBy(id => id).ToList();

        Assert.Equal(expectedStaff.Count + expectedPatients.Count, sent.Count);
        Assert.Equal(
            expectedStaff, sent.Where(n => n.RecipientStaffMemberId is not null)
                .Select(n => n.RecipientStaffMemberId!.Value).OrderBy(id => id).ToList());
        Assert.Equal(
            expectedPatients, sent.Where(n => n.RecipientPatientAccountId is not null)
                .Select(n => n.RecipientPatientAccountId!.Value).OrderBy(id => id).ToList());

        if (actorStaffId is { } actor)
        {
            Assert.DoesNotContain(sent, n => n.RecipientStaffMemberId == actor);
        }

        if (actorPatientAccountId is { } actorAccount)
        {
            Assert.DoesNotContain(sent, n => n.RecipientPatientAccountId == actorAccount);
        }
    }

    public async Task<int> UnreadCountAsync(NotificationType type, Guid entityId)
    {
        using var scope = _application.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CareLankaDbContext>();
        return await db.Notifications.CountAsync(n => n.Type == type && n.EntityId == entityId && n.ReadAt == null);
    }

    public async Task<int> CountAsync(NotificationType type, Guid entityId)
    {
        using var scope = _application.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CareLankaDbContext>();
        return await db.Notifications.CountAsync(n => n.Type == type && n.EntityId == entityId);
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

            using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            return Tokens[email] = body.RootElement.GetProperty("access_token").GetString()!;
        }
        finally
        {
            TokenLock.Release();
        }
    }
}
