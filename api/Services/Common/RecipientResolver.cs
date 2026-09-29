using CareLanka.Api.Data;
using CareLanka.Api.Data.Enums;
using CareLanka.Api.Services.Patient;
using CareLanka.Api.Services.Staff;
using Microsoft.EntityFrameworkCore;

namespace CareLanka.Api.Services.Common;

public sealed class RecipientResolver(
    CareLankaDbContext db,
    IPatientService patients,
    IAllocationService allocations,
    TimeProvider clock) : IRecipientResolver
{
    public async Task<IReadOnlyCollection<NotificationRecipient>> ResolveAsync(
        Recipients recipients, CancellationToken cancellationToken = default)
        => recipients switch
        {
            Recipients.StaffRecipient staff => [new NotificationRecipient(staff.StaffMemberId, null)],
            Recipients.PatientRecipient patient => await ResolvePatientAsync(patient, cancellationToken),
            Recipients.RoleRecipient role => await ResolveRoleAsync(role, cancellationToken),
            _ => throw new ArgumentOutOfRangeException(nameof(recipients))
        };

    private async Task<IReadOnlyCollection<NotificationRecipient>> ResolvePatientAsync(
        Recipients.PatientRecipient patient, CancellationToken cancellationToken)
    {
        var found = await patients.FindByIdAsync(patient.PatientId, cancellationToken);
        return found?.UserAccountId is { } accountId
            ? [new NotificationRecipient(null, accountId)]
            : [];
    }

    private async Task<IReadOnlyCollection<NotificationRecipient>> ResolveRoleAsync(
        Recipients.RoleRecipient role, CancellationToken cancellationToken)
    {
        var staffIds = role.WardId is { } wardId
            ? await OnShiftOrFallbackAsync(wardId, role.StaffRole, cancellationToken)
            : await ActiveStaffWithRoleAsync(role.StaffRole, cancellationToken);

        return staffIds.Select(id => new NotificationRecipient(id, null)).ToList();
    }

    private async Task<IReadOnlyCollection<Guid>> OnShiftOrFallbackAsync(
        Guid wardId, StaffRole role, CancellationToken cancellationToken)
    {
        var onShift = await allocations.FindOnShiftAsync(wardId, role, clock.GetUtcNow(), cancellationToken);
        return onShift.Count > 0 ? onShift : await ActiveStaffWithRoleAsync(role, cancellationToken);
    }

    private Task<List<Guid>> ActiveStaffWithRoleAsync(StaffRole role, CancellationToken cancellationToken)
        => db.StaffMembers.AsNoTracking()
            .Where(s => s.IsActive && s.Role == role)
            .Select(s => s.Id)
            .ToListAsync(cancellationToken);
}
