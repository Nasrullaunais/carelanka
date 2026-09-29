using CareLanka.Api.Data.Enums;

namespace CareLanka.Api.Hubs.Common;

public static class NotificationGroups
{
    public static string For(PrincipalType principalType, Guid id) => principalType switch
    {
        PrincipalType.Staff => $"staff:{id}",
        PrincipalType.Patient => $"patient:{id}",
        _ => throw new ArgumentOutOfRangeException(nameof(principalType), principalType, null)
    };
}
