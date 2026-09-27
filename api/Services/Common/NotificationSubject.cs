namespace CareLanka.Api.Services.Common;

public readonly record struct NotificationSubject(string EntityType, Guid EntityId);
