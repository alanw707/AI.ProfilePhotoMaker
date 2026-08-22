using AI.ProfilePhotoMaker.API.Models;
using AI.ProfilePhotoMaker.API.Models.DTOs;

namespace AI.ProfilePhotoMaker.API.Services;

public sealed record PromotedPreviewDownload(Stream Content, int ImageId);

public interface IOutcomePackageService
{
    Task<IReadOnlyList<OutcomePackageDefinitionDto>> GetActivePackageDefinitionsAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<UserPackageEntitlementDto>> GetUserEntitlementsAsync(string userId, CancellationToken cancellationToken = default);
    Task<bool> CanPromotePreviewAsync(string userId, int previewProcessedImageId, CancellationToken cancellationToken = default);
    Task<UserPackageEntitlement?> GrantEntitlementForCreditPackageAsync(string userId, int creditPackageId, string? paymentTransactionId, int? previewProcessedImageId = null, CancellationToken cancellationToken = default);
    Task<ResumableHeadshotPreviewDto?> GetResumablePreviewAsync(string userId, int? previewId = null, CancellationToken cancellationToken = default);
    Task<PromotedPreviewDownload?> GetPromotedPreviewDownloadAsync(string userId, int imageId, CancellationToken cancellationToken = default);
    Task<UserPackageEntitlement?> GetActiveEntitlementAsync(string userId, string packageCode, CancellationToken cancellationToken = default);
    Task<bool> ConsumeCandidatesAsync(string userId, string packageCode, int candidateCount, CancellationToken cancellationToken = default);
    Task<bool> ConsumeRefinementAsync(string userId, string? packageCode = null, CancellationToken cancellationToken = default);
    Task<bool> ConsumePremiumAugmentationAsync(string userId, CancellationToken cancellationToken = default);
    Task<bool> ConsumeExportKitAsync(string userId, CancellationToken cancellationToken = default);
}
