namespace AI.ProfilePhotoMaker.API.Configuration;

public class RetentionPolicyBackgroundServiceOptions
{
    public const string SectionName = "RetentionPolicyBackgroundService";

    public TimeSpan InitialDelay { get; set; } = TimeSpan.FromMinutes(5);
    public TimeSpan CheckInterval { get; set; } = TimeSpan.FromHours(6);
    public TimeSpan ErrorRetryDelay { get; set; } = TimeSpan.FromMinutes(30);
}
