using AI.ProfilePhotoMaker.API.Models;

namespace AI.ProfilePhotoMaker.API.Models.DTOs;

public class CreateCampaignRequest
{
    public string Name { get; set; } = null!;
    public string Subject { get; set; } = null!;
    public string HtmlBody { get; set; } = null!;
    public string SegmentFilter { get; set; } = null!;
    public DateTime? ScheduledAt { get; set; }
}

public class UpdateCampaignRequest
{
    public string? Name { get; set; }
    public string? Subject { get; set; }
    public string? HtmlBody { get; set; }
    public string? SegmentFilter { get; set; }
}

public class ScheduleCampaignRequest
{
    public DateTime ScheduledAt { get; set; }
}

public class SendTestRequest
{
    public string TestEmail { get; set; } = null!;
}

public class SegmentPreviewRequest
{
    public string SegmentFilter { get; set; } = null!;
}

public class CampaignDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
    public string Subject { get; set; } = null!;
    public string SegmentFilter { get; set; } = null!;
    public DateTime? ScheduledAt { get; set; }
    public string Status { get; set; } = null!;
    public int RecipientCount { get; set; }
    public int SentCount { get; set; }
    public int FailedCount { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }

    public static CampaignDto From(MarketingCampaign c) => new()
    {
        Id = c.Id,
        Name = c.Name,
        Subject = c.Subject,
        SegmentFilter = c.SegmentFilter,
        ScheduledAt = c.ScheduledAt,
        Status = c.Status.ToString(),
        RecipientCount = c.RecipientCount,
        SentCount = c.SentCount,
        FailedCount = c.FailedCount,
        CreatedBy = c.CreatedBy,
        CreatedAt = c.CreatedAt,
        StartedAt = c.StartedAt,
        CompletedAt = c.CompletedAt,
    };
}

public class CampaignDetailDto : CampaignDto
{
    public string HtmlBody { get; set; } = null!;

    public new static CampaignDetailDto From(MarketingCampaign c) => new()
    {
        Id = c.Id,
        Name = c.Name,
        Subject = c.Subject,
        HtmlBody = c.HtmlBody,
        SegmentFilter = c.SegmentFilter,
        ScheduledAt = c.ScheduledAt,
        Status = c.Status.ToString(),
        RecipientCount = c.RecipientCount,
        SentCount = c.SentCount,
        FailedCount = c.FailedCount,
        CreatedBy = c.CreatedBy,
        CreatedAt = c.CreatedAt,
        StartedAt = c.StartedAt,
        CompletedAt = c.CompletedAt,
    };
}

public class EmailLogDto
{
    public Guid Id { get; set; }
    public string UserId { get; set; } = null!;
    public string Email { get; set; } = null!;
    public string Status { get; set; } = null!;
    public string? ErrorMessage { get; set; }
    public DateTime? SentAt { get; set; }
    public DateTime CreatedAt { get; set; }

    public static EmailLogDto From(MarketingEmailLog l) => new()
    {
        Id = l.Id,
        UserId = l.UserId,
        Email = l.Email,
        Status = l.Status.ToString(),
        ErrorMessage = l.ErrorMessage,
        SentAt = l.SentAt,
        CreatedAt = l.CreatedAt,
    };
}
