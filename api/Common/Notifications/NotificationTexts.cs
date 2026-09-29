using System.Globalization;
using System.Resources;
using CareLanka.Api.Data.Enums;
using CareLanka.Api.Common.Persistence;

namespace CareLanka.Api.Common.Notifications;

public static class NotificationTexts
{
    private static readonly ResourceManager Resources =
        new("CareLanka.Api.Common.Notifications.NotificationTexts", typeof(NotificationTexts).Assembly);

    public static string Title(NotificationType type, params object[] args) => Render(type, "title", args);

    public static string Body(NotificationType type, params object[] args) => Render(type, "body", args);

    private static string Render(NotificationType type, string part, object[] args)
    {
        var key = $"{EnumWire.ToWire(type)}_{part}";
        var template = Resources.GetString(key, CultureInfo.CurrentUICulture)
            ?? throw new InvalidOperationException($"No notification text for '{key}'.");

        return args.Length == 0 ? template : string.Format(CultureInfo.CurrentUICulture, template, args);
    }
}
