namespace AI.ProfilePhotoMaker.API.Configuration;

public class RetentionNotificationOptions
{
    public const string SectionName = "RetentionNotifications";

    public int[] NotificationDays { get; set; } = { 14, 7 };
}
