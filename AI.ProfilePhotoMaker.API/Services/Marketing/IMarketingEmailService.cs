using AI.ProfilePhotoMaker.API.Models;
using AI.ProfilePhotoMaker.API.Models.DTOs;

namespace AI.ProfilePhotoMaker.API.Services.Marketing;

public interface IMarketingEmailService
{
    Task<MarketingCampaign> CreateCampaignAsync(CreateCampaignRequest request, string createdBy);
    Task<MarketingCampaign?> GetCampaignAsync(Guid campaignId);
    Task<(IEnumerable<MarketingCampaign> Campaigns, int TotalCount)> GetCampaignsAsync(int page, int pageSize);
    Task<MarketingCampaign> UpdateCampaignAsync(Guid campaignId, UpdateCampaignRequest request);
    Task ScheduleCampaignAsync(Guid campaignId, DateTime scheduledAt);
    Task CancelCampaignAsync(Guid campaignId);
    Task<MarketingCampaign> DuplicateCampaignAsync(Guid campaignId, string createdBy);
    Task SendTestAsync(Guid campaignId, string testEmail);
    Task ExecuteCampaignAsync(Guid campaignId, CancellationToken cancellationToken = default);
    Task<(IEnumerable<MarketingEmailLog> Logs, int TotalCount)> GetLogsAsync(Guid campaignId, int page, int pageSize);
    Task<IEnumerable<(string UserId, string Email)>> GetRecipientsAsync(Guid campaignId, int page, int pageSize);
}
