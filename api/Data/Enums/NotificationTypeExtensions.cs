namespace CareLanka.Api.Data.Enums;

public static class AndroidChannels
{
    public const string Urgent = "urgent_alerts";
    public const string General = "general";
}

public static class NotificationTypeExtensions
{
    // Crew assignment, new emergency call and urgent care flags pop up loudly; everything else is quiet.
    public static string AndroidChannel(this NotificationType type) => type switch
    {
        NotificationType.DispatchAssigned => AndroidChannels.Urgent,
        _ => AndroidChannels.General
    };
}
