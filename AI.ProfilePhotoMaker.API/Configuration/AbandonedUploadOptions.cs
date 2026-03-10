namespace AI.ProfilePhotoMaker.API.Configuration;

public class AbandonedUploadOptions
{
    public const string SectionName = "AbandonedUpload";

    /// <summary>How long to wait after startup before the first check runs.</summary>
    public TimeSpan InitialDelay { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>How often the background service checks for abandoned signups.</summary>
    public TimeSpan CheckInterval { get; set; } = TimeSpan.FromHours(1);

    /// <summary>Retry delay after an unexpected error.</summary>
    public TimeSpan ErrorRetryDelay { get; set; } = TimeSpan.FromMinutes(15);

    /// <summary>How long after signup before we consider the user "abandoned" (default: 4 hours).</summary>
    public TimeSpan GracePeriod { get; set; } = TimeSpan.FromHours(4);

    /// <summary>
    /// Upper bound — don't nudge accounts older than this (default: 7 days).
    /// Avoids emailing very old accounts that simply never converted.
    /// </summary>
    public TimeSpan MaxAge { get; set; } = TimeSpan.FromDays(7);

    /// <summary>Whether the nudge feature is enabled (set to false to disable without redeploying).</summary>
    public bool Enabled { get; set; } = true;
}
