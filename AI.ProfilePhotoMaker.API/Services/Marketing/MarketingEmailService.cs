using AI.ProfilePhotoMaker.API.Configuration;
using AI.ProfilePhotoMaker.API.Data;
using AI.ProfilePhotoMaker.API.Models;
using AI.ProfilePhotoMaker.API.Models.DTOs;
using AI.ProfilePhotoMaker.API.Services.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AI.ProfilePhotoMaker.API.Services.Marketing;

public class MarketingEmailService : IMarketingEmailService
{
    private readonly ApplicationDbContext _db;
    private readonly IEmailNotificationService _emailService;
    private readonly IUserSegmentService _segmentService;
    private readonly EmailOptions _emailOptions;
    private readonly MarketingEmailOptions _marketingOptions;
    private readonly ILogger<MarketingEmailService> _logger;

    public MarketingEmailService(
        ApplicationDbContext db,
        IEmailNotificationService emailService,
        IUserSegmentService segmentService,
        IOptions<EmailOptions> emailOptions,
        IOptions<MarketingEmailOptions> marketingOptions,
        ILogger<MarketingEmailService> logger)
    {
        _db = db;
        _emailService = emailService;
        _segmentService = segmentService;
        _emailOptions = emailOptions.Value;
        _marketingOptions = marketingOptions.Value;
        _logger = logger;
    }

    public async Task<MarketingCampaign> CreateCampaignAsync(CreateCampaignRequest request, string createdBy)
    {
        if (!await _segmentService.IsValidSegmentFilter(request.SegmentFilter))
            throw new ArgumentException($"Unknown segment filter: {request.SegmentFilter}");

        var campaign = new MarketingCampaign
        {
            Name = request.Name,
            Subject = request.Subject,
            HtmlBody = request.HtmlBody,
            SegmentFilter = request.SegmentFilter,
            ScheduledAt = request.ScheduledAt,
            Status = request.ScheduledAt.HasValue ? CampaignStatus.Scheduled : CampaignStatus.Draft,
            CreatedBy = createdBy,
            CreatedAt = DateTime.UtcNow,
        };

        if (campaign.Status == CampaignStatus.Scheduled)
        {
            campaign.RecipientCount = await _segmentService.GetSegmentCountAsync(request.SegmentFilter);
        }

        _db.MarketingCampaigns.Add(campaign);
        await _db.SaveChangesAsync();
        return campaign;
    }

    public Task<MarketingCampaign?> GetCampaignAsync(Guid campaignId)
        => _db.MarketingCampaigns.FindAsync(campaignId).AsTask();

    public async Task<(IEnumerable<MarketingCampaign> Campaigns, int TotalCount)> GetCampaignsAsync(int page, int pageSize)
    {
        var query = _db.MarketingCampaigns.OrderByDescending(c => c.CreatedAt);
        var total = await query.CountAsync();
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
        return (items, total);
    }

    public async Task<MarketingCampaign> UpdateCampaignAsync(Guid campaignId, UpdateCampaignRequest request)
    {
        var campaign = await _db.MarketingCampaigns.FindAsync(campaignId)
            ?? throw new KeyNotFoundException($"Campaign {campaignId} not found");

        if (campaign.Status != CampaignStatus.Draft)
            throw new InvalidOperationException("Only Draft campaigns can be updated");

        if (request.Name != null) campaign.Name = request.Name;
        if (request.Subject != null) campaign.Subject = request.Subject;
        if (request.HtmlBody != null) campaign.HtmlBody = request.HtmlBody;
        if (request.SegmentFilter != null)
        {
            if (!await _segmentService.IsValidSegmentFilter(request.SegmentFilter))
                throw new ArgumentException($"Unknown segment filter: {request.SegmentFilter}");
            campaign.SegmentFilter = request.SegmentFilter;
        }

        await _db.SaveChangesAsync();
        return campaign;
    }

    public async Task ScheduleCampaignAsync(Guid campaignId, DateTime scheduledAt)
    {
        var campaign = await _db.MarketingCampaigns.FindAsync(campaignId)
            ?? throw new KeyNotFoundException($"Campaign {campaignId} not found");

        if (campaign.Status is not (CampaignStatus.Draft or CampaignStatus.Cancelled))
            throw new InvalidOperationException($"Cannot schedule a campaign with status {campaign.Status}");

        if (scheduledAt <= DateTime.UtcNow)
            throw new ArgumentException("ScheduledAt must be in the future");

        campaign.ScheduledAt = scheduledAt;
        campaign.Status = CampaignStatus.Scheduled;
        campaign.RecipientCount = await _segmentService.GetSegmentCountAsync(campaign.SegmentFilter);
        await _db.SaveChangesAsync();
    }

    public async Task CancelCampaignAsync(Guid campaignId)
    {
        var campaign = await _db.MarketingCampaigns.FindAsync(campaignId)
            ?? throw new KeyNotFoundException($"Campaign {campaignId} not found");

        if (campaign.Status is not (CampaignStatus.Draft or CampaignStatus.Scheduled))
            throw new InvalidOperationException($"Cannot cancel a campaign with status {campaign.Status}");

        campaign.Status = CampaignStatus.Cancelled;
        await _db.SaveChangesAsync();
    }

    public async Task<MarketingCampaign> DuplicateCampaignAsync(Guid campaignId, string createdBy)
    {
        var source = await _db.MarketingCampaigns.FindAsync(campaignId)
            ?? throw new KeyNotFoundException($"Campaign {campaignId} not found");

        var copy = new MarketingCampaign
        {
            Name = $"Copy of {source.Name}",
            Subject = source.Subject,
            HtmlBody = source.HtmlBody,
            SegmentFilter = source.SegmentFilter,
            Status = CampaignStatus.Draft,
            CreatedBy = createdBy,
            CreatedAt = DateTime.UtcNow,
        };

        _db.MarketingCampaigns.Add(copy);
        await _db.SaveChangesAsync();
        return copy;
    }

    public async Task SendTestAsync(Guid campaignId, string testEmail)
    {
        var campaign = await _db.MarketingCampaigns.FindAsync(campaignId)
            ?? throw new KeyNotFoundException($"Campaign {campaignId} not found");

        var result = await _emailService.SendMarketingEmailAsync(
            userId: "test",
            email: testEmail,
            subject: $"[TEST] {campaign.Subject}",
            htmlBody: campaign.HtmlBody,
            unsubscribeUrl: BuildUnsubscribeUrl("test", campaignId));

        if (!result.Success)
        {
            throw new InvalidOperationException("Test email send failed");
        }
    }

    public async Task ExecuteCampaignAsync(Guid campaignId, CancellationToken cancellationToken = default)
    {
        var campaign = await _db.MarketingCampaigns.FindAsync(campaignId)
            ?? throw new KeyNotFoundException($"Campaign {campaignId} not found");

        if (campaign.Status != CampaignStatus.Scheduled)
            throw new InvalidOperationException($"Campaign {campaignId} is not in Scheduled state");

        campaign.Status = CampaignStatus.Sending;
        campaign.StartedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        _logger.LogInformation("Starting campaign {CampaignId} ({Name})", campaignId, campaign.Name);

        try
        {
            int page = 1;
            int sent = 0, failed = 0;

            while (!cancellationToken.IsCancellationRequested)
            {
                var recipients = (await _segmentService.GetSegmentUsersAsync(
                    campaign.SegmentFilter, page, _marketingOptions.BatchSize)).ToList();

                if (recipients.Count == 0) break;

                foreach (var (userId, email) in recipients)
                {
                    if (cancellationToken.IsCancellationRequested) break;

                    var log = await _db.MarketingEmailLogs.SingleOrDefaultAsync(
                        l => l.CampaignId == campaignId && l.UserId == userId, cancellationToken);

                    if (log is { Status: MarketingEmailStatus.Sent or MarketingEmailStatus.Bounced or MarketingEmailStatus.Unsubscribed })
                    {
                        continue;
                    }

                    if (log == null)
                    {
                        log = new MarketingEmailLog
                        {
                            CampaignId = campaignId,
                            UserId = userId,
                            Email = email,
                            Status = MarketingEmailStatus.Pending,
                            CreatedAt = DateTime.UtcNow,
                        };
                        _db.MarketingEmailLogs.Add(log);
                    }
                    else
                    {
                        log.Email = email;
                        log.PostmarkMessageId = null;
                        log.Status = MarketingEmailStatus.Pending;
                        log.ErrorMessage = null;
                        log.SentAt = null;
                    }

                    await _db.SaveChangesAsync(cancellationToken);

                    var unsubscribeUrl = BuildUnsubscribeUrl(userId, campaignId);

                    var result = await _emailService.SendMarketingEmailAsync(
                        userId, email, campaign.Subject, campaign.HtmlBody, unsubscribeUrl);

                    log.PostmarkMessageId = result.Success ? result.ProviderMessageId : null;
                    log.Status = result.Success ? MarketingEmailStatus.Sent : MarketingEmailStatus.Failed;
                    log.SentAt = result.Success ? DateTime.UtcNow : null;
                    if (!result.Success) log.ErrorMessage = "Send failed";
                    await _db.SaveChangesAsync(cancellationToken);

                    if (result.Success) sent++; else failed++;
                }

                cancellationToken.ThrowIfCancellationRequested();

                if (recipients.Count < _marketingOptions.BatchSize) break;
                page++;

                if (_marketingOptions.BatchDelayMs > 0)
                    await Task.Delay(_marketingOptions.BatchDelayMs, cancellationToken);
            }

            campaign.SentCount = sent;
            campaign.FailedCount = failed;
            campaign.Status = CampaignStatus.Sent;
            campaign.CompletedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            _logger.LogInformation(
                "Campaign {CampaignId} completed: {Sent} sent, {Failed} failed",
                campaignId, sent, failed);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Campaign {CampaignId} execution was cancelled", campaignId);
            campaign.Status = CampaignStatus.Scheduled;
            await _db.SaveChangesAsync();
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Campaign {CampaignId} failed unexpectedly", campaignId);
            campaign.Status = CampaignStatus.Scheduled; // Reset so it will retry
            await _db.SaveChangesAsync();
            throw;
        }
    }

    public async Task<(IEnumerable<MarketingEmailLog> Logs, int TotalCount)> GetLogsAsync(
        Guid campaignId, int page, int pageSize)
    {
        var query = _db.MarketingEmailLogs
            .Where(l => l.CampaignId == campaignId)
            .OrderByDescending(l => l.CreatedAt);
        var total = await query.CountAsync();
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
        return (items, total);
    }

    public async Task<IEnumerable<(string UserId, string Email)>> GetRecipientsAsync(
        Guid campaignId, int page, int pageSize)
    {
        var campaign = await _db.MarketingCampaigns.FindAsync(campaignId)
            ?? throw new KeyNotFoundException($"Campaign {campaignId} not found");

        return await _segmentService.GetSegmentUsersAsync(campaign.SegmentFilter, page, pageSize);
    }

    private string BuildUnsubscribeUrl(string userId, Guid campaignId)
    {
        var baseUrl = _emailOptions.FrontendBaseUrl?.TrimEnd('/');
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            throw new InvalidOperationException("Email.FrontendBaseUrl is not configured");
        }

        var token = userId == "test"
            ? "test"
            : MarketingUnsubscribeTokenService.CreateToken(
                userId,
                campaignId,
                MarketingUnsubscribeTokenService.ResolveSigningSecret(_emailOptions)
                    ?? throw new InvalidOperationException("Marketing unsubscribe signing secret is not configured"));

        return $"{baseUrl}/unsubscribe?token={Uri.EscapeDataString(token)}";
    }
}
