namespace CareLanka.Api.Services.Common;

// Occasion separates two different events of the same type on the same record, so the second is not swallowed as a repeat.
public readonly record struct NotificationSubject(string EntityType, Guid EntityId, string? Occasion = null);
