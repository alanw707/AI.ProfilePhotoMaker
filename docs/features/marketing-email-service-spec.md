# Marketing Email Service — Feature Specification

## Overview
Build a scheduled marketing email system that supports targeted user segments, campaign management, and bulk email delivery with tracking. This enables re-engagement campaigns, feature announcements, and lifecycle marketing without building one-off blasts.

## Goals
- Send scheduled marketing emails to targeted user segments
- Prevent duplicate sends via recipient tracking
- Provide admin endpoints for campaign creation/management
- Support unsubscribe/opt-out functionality
- Use existing email design system (dark navy wrapper, white card, sky blue CTA)

## First Campaign
**"You now only need 5 selfies"** — notify users who signed up but never uploaded (or uploaded < 10 photos) that the barrier to entry is now lower.

---

## Data Models

### MarketingCampaign
```csharp
public class MarketingCampaign
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;              // Internal name
    public string Subject { get; set; } = null!;           // Email subject line
    public string HtmlBody { get; set; } = null!;          // Email body (pre-wrapped)
    public string SegmentFilter { get; set; } = null!;     // no-uploads | stuck-under-minimum | inactive-30d | has-uploads-no-model | all-verified
    public DateTime? ScheduledAt { get; set; }            // When to send (null = draft)
    public CampaignStatus Status { get; set; }              // Draft | Scheduled | Sending | Sent | Cancelled
    public int RecipientCount { get; set; }                 // Target count at send time
    public int SentCount { get; set; }                      // Actually sent
    public int FailedCount { get; set; }                    // Failed sends
    public string? CreatedBy { get; set; }                  // Admin user ID
    public DateTime CreatedAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}

public enum CampaignStatus { Draft, Scheduled, Sending, Sent, Cancelled }
```

### MarketingEmailLog
```csharp
public class MarketingEmailLog
{
    public Guid Id { get; set; }
    public Guid CampaignId { get; set; }
    public string UserId { get; set; } = null!;
    public string Email { get; set; } = null!;
    public EmailStatus Status { get; set; }              // Pending | Sent | Failed | Bounced | Unsubscribed
    public string? ErrorMessage { get; set; }
    public DateTime? SentAt { get; set; }
    public DateTime CreatedAt { get; set; }
}

public enum EmailStatus { Pending, Sent, Failed, Bounced, Unsubscribed }
```

### UserProfile Update
```csharp
public class ApplicationUser
{
    // Add to existing:
    public bool MarketingOptOut { get; set; }              // Unsubscribe flag
    public DateTime? MarketingOptOutAt { get; set; }
}
```

---

## User Segments

| Segment | Filter Logic |
|---------|-------------|
| `no-uploads` | Signed up, email verified, 0 photos uploaded |
| `stuck-under-minimum` | Uploaded 1-4 photos (previously blocked at 10, now can train) |
| `has-uploads-no-model` | Uploaded 5+ photos, never started training |
| `inactive-30d` | No login in last 30 days, has uploads |
| `all-verified` | All users with verified email, not opted out |

---

## API Endpoints

### Admin Endpoints

```
POST   /api/admin/campaigns                    → Create campaign (draft)
GET    /api/admin/campaigns                    → List all campaigns
GET    /api/admin/campaigns/{id}               → Get campaign details + stats
PATCH  /api/admin/campaigns/{id}               → Update campaign (only if Draft)
POST   /api/admin/campaigns/{id}/schedule    → Schedule send (sets ScheduledAt, validates)
POST   /api/admin/campaigns/{id}/cancel        → Cancel scheduled campaign
POST   /api/admin/campaigns/{id}/test          → Send test to admin email only
GET    /api/admin/campaigns/{id}/recipients   → Preview recipient list (paginated)
GET    /api/admin/campaigns/{id}/logs          → Email delivery logs (paginated)
POST   /api/admin/campaigns/{id}/duplicate    → Clone campaign as new draft

POST   /api/admin/segments/preview             → Preview user count for segment filter
```

### User Endpoints

```
POST /api/user/marketing/unsubscribe            → Unsubscribe from marketing (link in email)
GET  /api/user/marketing/status                → Check opt-in status
```

### Request/Response Schemas

**Create Campaign:**
```json
{
  "name": "5 Image Minimum Launch",
  "subject": "Good news: You can now create headshots with just 5 selfies",
  "htmlBody": "<p>We heard your feedback...</p>",
  "segmentFilter": "no-uploads",
  "scheduledAt": "2026-04-10T09:00:00Z"
}
```

---

## Services

### IUserSegmentService
```csharp
public interface IUserSegmentService
{
    Task<int> GetSegmentCountAsync(string segmentFilter);
    Task<IEnumerable<string>> GetSegmentUserIdsAsync(string segmentFilter, int page, int pageSize);
    Task<bool> IsUserInSegmentAsync(string userId, string segmentFilter);
}
```

### IMarketingEmailService
```csharp
public interface IMarketingEmailService
{
    Task<MarketingCampaign> CreateCampaignAsync(CreateCampaignRequest request, string createdBy);
    Task ScheduleCampaignAsync(Guid campaignId, DateTime scheduledAt);
    Task CancelCampaignAsync(Guid campaignId);
    Task SendTestAsync(Guid campaignId, string testEmail);
    Task ExecuteCampaignAsync(Guid campaignId);  // Called by background job
}
```

### MarketingEmailBackgroundService
```csharp
public class MarketingEmailBackgroundService : BackgroundService
{
    // Runs every minute
    // Finds campaigns with Status=Scheduled and ScheduledAt <= Now
    // Updates status to Sending, processes in batches
    // Respects Postmark rate limits (e.g., 100/sec for burst, 10/sec sustained)
}
```

---

## Email Template

Uses existing `WrapEmail()` layout. Adds unsubscribe footer:

```html
<p style="margin:24px 0 0; font-size:13px; color:#64748b;">
  You're receiving this because you signed up for AI Profile Photo Maker.
  <a href="{unsubscribeUrl}" style="color:#64748b; text-decoration:underline;">Unsubscribe</a> from marketing emails.
</p>
```

---

## Database Migrations

```sql
-- MarketingCampaigns table
CREATE TABLE MarketingCampaigns (
    Id UNIQUEIDENTIFIER PRIMARY KEY,
    Name NVARCHAR(200) NOT NULL,
    Subject NVARCHAR(300) NOT NULL,
    HtmlBody NVARCHAR(MAX) NOT NULL,
    SegmentFilter NVARCHAR(50) NOT NULL,
    ScheduledAt DATETIME2 NULL,
    Status INT NOT NULL,
    RecipientCount INT NOT NULL DEFAULT 0,
    SentCount INT NOT NULL DEFAULT 0,
    FailedCount INT NOT NULL DEFAULT 0,
    CreatedBy NVARCHAR(450) NULL,
    CreatedAt DATETIME2 NOT NULL,
    StartedAt DATETIME2 NULL,
    CompletedAt DATETIME2 NULL
);

-- MarketingEmailLogs table
CREATE TABLE MarketingEmailLogs (
    Id UNIQUEIDENTIFIER PRIMARY KEY,
    CampaignId UNIQUEIDENTIFIER NOT NULL,
    UserId NVARCHAR(450) NOT NULL,
    Email NVARCHAR(256) NOT NULL,
    Status INT NOT NULL,
    ErrorMessage NVARCHAR(MAX) NULL,
    SentAt DATETIME2 NULL,
    CreatedAt DATETIME2 NOT NULL,
    CONSTRAINT FK_Logs_Campaign FOREIGN KEY (CampaignId) REFERENCES MarketingCampaigns(Id)
);
CREATE INDEX IX_MarketingEmailLogs_CampaignId ON MarketingEmailLogs(CampaignId);
CREATE INDEX IX_MarketingEmailLogs_UserId ON MarketingEmailLogs(UserId);
CREATE INDEX IX_MarketingEmailLogs_Status ON MarketingEmailLogs(Status);

-- Add columns to AspNetUsers
ALTER TABLE AspNetUsers ADD MarketingOptOut BIT NOT NULL DEFAULT 0;
ALTER TABLE AspNetUsers ADD MarketingOptOutAt DATETIME2 NULL;
```

---

## Implementation Plan

| Step | Action | Est | PR |
|------|--------|-----|-----|
| 1 | Create spec document (this file) | 30 min | — |
| 2 | Add EF entities + migrations | 45 min | #351 |
| 3 | Build `IUserSegmentService` + implementations | 1 hr | #352 |
| 4 | Build `IMarketingEmailService` + Postmark integration | 1.5 hr | #353 |
| 5 | Add admin API endpoints (CRUD + schedule/test) | 1.5 hr | #354 |
| 6 | Build `MarketingEmailBackgroundService` | 1 hr | #355 |
| 7 | Add unsubscribe endpoint + email footer | 30 min | #356 |
| 8 | Create first campaign content + send test | 30 min | — |
| 9 | End-to-end testing + deploy | 1 hr | — |

**Total estimate:** ~8 hours across ~6 PRs

---

## Open Questions

1. **Rate limiting:** Postmark allows 100/sec burst, 10/sec sustained. Should we configure per-campaign batch sizes, or hardcode sensible defaults?
2. **Admin auth:** Should campaigns be restricted to a specific admin role, or any authenticated admin user?
3. **Email preview:** Do we want a "preview" endpoint that returns the fully-rendered email HTML for review before sending?
4. **Bounce handling:** Should we auto-unsubscribe users after N bounces, or just log and continue?
5. **Throttling:** Should we spread large campaigns over multiple hours to avoid spam filters, or send in one batch?

---

## Success Criteria

- [ ] Admin can create a campaign and preview recipient count
- [ ] Admin can schedule a campaign for future delivery
- [ ] Admin can send a test email to verify content
- [ ] Background service picks up scheduled campaigns and sends them
- [ ] Each recipient receives exactly one email (no duplicates)
- [ ] Users can unsubscribe via footer link
- [ ] Unsubscribed users are excluded from future campaigns
- [ ] Delivery stats (sent/failed) are visible in admin UI
- [ ] First campaign sent to `no-uploads` + `stuck-under-minimum` segments

---

## Related Documents

- [min-images-5-spec.md](./min-images-5-spec.md) — Previous feature that motivated this campaign
- [EmailNotificationService.cs](../../AI.ProfilePhotoMaker.API/Services/Notifications/EmailNotificationService.cs) — Existing email template
