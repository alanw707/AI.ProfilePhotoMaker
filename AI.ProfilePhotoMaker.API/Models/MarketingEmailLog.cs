namespace AI.ProfilePhotoMaker.API.Models;

public class MarketingEmailLog
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CampaignId { get; set; }
    public MarketingCampaign Campaign { get; set; } = null!;
    public string UserId { get; set; } = null!;
    public string Email { get; set; } = null!;
    public string? PostmarkMessageId { get; set; }
    public MarketingEmailStatus Status { get; set; } = MarketingEmailStatus.Pending;
    public string? ErrorMessage { get; set; }
    public DateTime? SentAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public enum MarketingEmailStatus { Pending, Sent, Failed, Bounced, Unsubscribed }
