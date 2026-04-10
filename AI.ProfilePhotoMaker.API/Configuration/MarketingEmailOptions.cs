namespace AI.ProfilePhotoMaker.API.Configuration;

public class MarketingEmailOptions
{
    public const string SectionName = "MarketingEmail";

    public bool Enabled { get; set; } = true;
    public TimeSpan InitialDelay { get; set; } = TimeSpan.FromMinutes(2);
    public TimeSpan CheckInterval { get; set; } = TimeSpan.FromMinutes(1);
    public TimeSpan ErrorRetryDelay { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>Number of recipients to process per iteration to respect Postmark rate limits.</summary>
    public int BatchSize { get; set; } = 50;

    /// <summary>Delay between batches in milliseconds.</summary>
    public int BatchDelayMs { get; set; } = 1000;
}
