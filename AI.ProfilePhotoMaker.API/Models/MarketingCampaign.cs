namespace AI.ProfilePhotoMaker.API.Models;

public class MarketingCampaign
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = null!;
    public string Subject { get; set; } = null!;
    public string HtmlBody { get; set; } = null!;
    public string SegmentFilter { get; set; } = null!;
    public DateTime? ScheduledAt { get; set; }
    public CampaignStatus Status { get; set; } = CampaignStatus.Draft;
    public int RecipientCount { get; set; }
    public int SentCount { get; set; }
    public int FailedCount { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}

public enum CampaignStatus { Draft, Scheduled, Sending, Sent, Cancelled }
