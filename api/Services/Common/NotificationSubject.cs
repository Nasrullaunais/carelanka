namespace CareLanka.Api.Services.Common;

// OccurrenceId separates repeat events about one entity, e.g. each failed recommendation for the same call.
public readonly record struct NotificationSubject(string EntityType, Guid EntityId, Guid? OccurrenceId = null);
