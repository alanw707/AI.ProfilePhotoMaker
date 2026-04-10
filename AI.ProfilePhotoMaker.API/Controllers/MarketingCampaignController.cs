using AI.ProfilePhotoMaker.API.Models.DTOs;
using AI.ProfilePhotoMaker.API.Services.Marketing;
using AI.ProfilePhotoMaker.API.Services.Notifications;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AI.ProfilePhotoMaker.API.Controllers;

[ApiController]
[Route("api/admin/campaigns")]
[Authorize(Roles = "Admin")]
public class MarketingCampaignController : BaseController
{
    private readonly IMarketingEmailService _marketingService;
    private readonly IUserSegmentService _segmentService;
    private readonly IEmailNotificationService _emailService;

    public MarketingCampaignController(
        IMarketingEmailService marketingService,
        IUserSegmentService segmentService,
        IEmailNotificationService emailService,
        ILogger<MarketingCampaignController> logger)
        : base(logger)
    {
        _marketingService = marketingService;
        _segmentService = segmentService;
        _emailService = emailService;
    }

    [HttpPost]
    public async Task<IActionResult> CreateCampaign([FromBody] CreateCampaignRequest request)
    {
        var authCheck = ValidateAuthentication();
        if (authCheck != null) return authCheck;

        return await ExecuteAsync(async () =>
        {
            var campaign = await _marketingService.CreateCampaignAsync(request, GetCurrentUserId()!);
            return CampaignDetailDto.From(campaign);
        }, "CreateCampaign");
    }

    [HttpGet]
    public async Task<IActionResult> GetCampaigns([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var authCheck = ValidateAuthentication();
        if (authCheck != null) return authCheck;

        var (campaigns, total) = await _marketingService.GetCampaignsAsync(page, pageSize);
        return SuccessResponse(new
        {
            campaigns = campaigns.Select(CampaignDto.From),
            totalCount = total,
            page,
            pageSize
        });
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetCampaign(Guid id)
    {
        var authCheck = ValidateAuthentication();
        if (authCheck != null) return authCheck;

        var campaign = await _marketingService.GetCampaignAsync(id);
        if (campaign == null) return NotFoundResponse("Campaign", id);
        return SuccessResponse(CampaignDetailDto.From(campaign));
    }

    [HttpPatch("{id:guid}")]
    public async Task<IActionResult> UpdateCampaign(Guid id, [FromBody] UpdateCampaignRequest request)
    {
        var authCheck = ValidateAuthentication();
        if (authCheck != null) return authCheck;

        return await ExecuteAsync(async () =>
        {
            var campaign = await _marketingService.UpdateCampaignAsync(id, request);
            return CampaignDetailDto.From(campaign);
        }, "UpdateCampaign");
    }

    [HttpPost("{id:guid}/schedule")]
    public async Task<IActionResult> ScheduleCampaign(Guid id, [FromBody] ScheduleCampaignRequest request)
    {
        var authCheck = ValidateAuthentication();
        if (authCheck != null) return authCheck;

        return await ExecuteAsync(async () =>
        {
            await _marketingService.ScheduleCampaignAsync(id, request.ScheduledAt);
            return new { scheduled = true };
        }, "ScheduleCampaign");
    }

    [HttpPost("{id:guid}/cancel")]
    public async Task<IActionResult> CancelCampaign(Guid id)
    {
        var authCheck = ValidateAuthentication();
        if (authCheck != null) return authCheck;

        return await ExecuteAsync(async () =>
        {
            await _marketingService.CancelCampaignAsync(id);
            return new { cancelled = true };
        }, "CancelCampaign");
    }

    [HttpPost("{id:guid}/test")]
    public async Task<IActionResult> SendTest(Guid id, [FromBody] SendTestRequest request)
    {
        var authCheck = ValidateAuthentication();
        if (authCheck != null) return authCheck;

        return await ExecuteAsync(async () =>
        {
            await _marketingService.SendTestAsync(id, request.TestEmail);
            return new { sent = true, testEmail = request.TestEmail };
        }, "SendTest");
    }

    [HttpPost("{id:guid}/duplicate")]
    public async Task<IActionResult> DuplicateCampaign(Guid id)
    {
        var authCheck = ValidateAuthentication();
        if (authCheck != null) return authCheck;

        return await ExecuteAsync(async () =>
        {
            var copy = await _marketingService.DuplicateCampaignAsync(id, GetCurrentUserId()!);
            return CampaignDetailDto.From(copy);
        }, "DuplicateCampaign");
    }

    [HttpGet("{id:guid}/recipients")]
    public async Task<IActionResult> GetRecipients(Guid id, [FromQuery] int page = 1, [FromQuery] int pageSize = 50)
    {
        var authCheck = ValidateAuthentication();
        if (authCheck != null) return authCheck;

        return await ExecuteAsync(async () =>
        {
            var recipients = await _marketingService.GetRecipientsAsync(id, page, pageSize);
            return new { recipients = recipients.Select(r => new { r.UserId, r.Email }), page, pageSize };
        }, "GetRecipients");
    }

    [HttpGet("{id:guid}/logs")]
    public async Task<IActionResult> GetLogs(Guid id, [FromQuery] int page = 1, [FromQuery] int pageSize = 50)
    {
        var authCheck = ValidateAuthentication();
        if (authCheck != null) return authCheck;

        var (logs, total) = await _marketingService.GetLogsAsync(id, page, pageSize);
        return SuccessResponse(new
        {
            logs = logs.Select(EmailLogDto.From),
            totalCount = total,
            page,
            pageSize
        });
    }

    /// <summary>
    /// Returns the fully-rendered HTML for a campaign email, for preview in a browser or iframe.
    /// </summary>
    [HttpGet("{id:guid}/preview")]
    public async Task<IActionResult> PreviewEmail(Guid id)
    {
        var authCheck = ValidateAuthentication();
        if (authCheck != null) return authCheck;

        var campaign = await _marketingService.GetCampaignAsync(id);
        if (campaign == null) return NotFoundResponse("Campaign", id);

        var html = _emailService.RenderMarketingEmailPreview(campaign.Subject, campaign.HtmlBody);
        return Content(html, "text/html");
    }

    [HttpPost("segments/preview")]
    public async Task<IActionResult> PreviewSegment([FromBody] SegmentPreviewRequest request)
    {
        var authCheck = ValidateAuthentication();
        if (authCheck != null) return authCheck;

        return await ExecuteAsync(async () =>
        {
            if (!await _segmentService.IsValidSegmentFilter(request.SegmentFilter))
                throw new ArgumentException($"Unknown segment filter: {request.SegmentFilter}");

            var count = await _segmentService.GetSegmentCountAsync(request.SegmentFilter);
            return new { segmentFilter = request.SegmentFilter, count };
        }, "PreviewSegment");
    }
}
