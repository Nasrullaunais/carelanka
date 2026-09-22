using CareLanka.Api.Common.Exceptions;
using CareLanka.Api.Data;
using CareLanka.Api.Data.Entities.Common;
using CareLanka.Api.DTOs.Common;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace CareLanka.Api.Services.Common;

public sealed class DeviceTokenService(CareLankaDbContext db, ICurrentUser currentUser, TimeProvider clock) : IDeviceTokenService
{
    public async Task<DeviceRegistration> RegisterAsync(RegisterDeviceRequest request, CancellationToken ct = default)
    {
        var token = request.Token.Trim();
        try
        {
            return await UpsertAsync(token, request, ct);
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException
        { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            db.ChangeTracker.Clear();
            return await UpsertAsync(token, request, ct);
        }
    }

    public async Task UnregisterAsync(Guid id, CancellationToken ct = default)
    {
        var device = await db.DeviceTokens.SingleOrDefaultAsync(d => d.Id == id && d.StaffMemberId == currentUser.Id, ct)
            ?? throw new NotFoundException("Device", id);
        if (device.RevokedAt is not null) return;
        device.RevokedAt = clock.GetUtcNow();
        await db.SaveChangesAsync(ct);
    }

    private async Task<DeviceRegistration> UpsertAsync(string token, RegisterDeviceRequest request, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var device = await db.DeviceTokens.SingleOrDefaultAsync(d => d.Token == token, ct);
        if (device is null)
        {
            device = new DeviceToken { Id = Guid.NewGuid(), Token = token };
            db.DeviceTokens.Add(device);
        }

        device.StaffMemberId = currentUser.Id;
        device.Platform = request.Platform;
        device.LastSeenAt = now;
        device.RevokedAt = null;
        await db.SaveChangesAsync(ct);
        return new DeviceRegistration { Id = device.Id, Platform = device.Platform, LastSeenAt = device.LastSeenAt };
    }
}
