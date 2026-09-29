namespace CareLanka.Api.Services.Common;

public sealed class NotificationOptions
{
    public const string SectionName = "Notifications";

    public int RetentionDays { get; set; } = 90;

    public int ReminderLeadHours { get; set; } = 24;
}
