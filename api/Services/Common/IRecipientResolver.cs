namespace CareLanka.Api.Services.Common;

public readonly record struct NotificationRecipient(Guid? StaffMemberId, Guid? PatientAccountId);

public interface IRecipientResolver
{
    Task<IReadOnlyCollection<NotificationRecipient>> ResolveAsync(Recipients recipients, CancellationToken cancellationToken = default);
}
