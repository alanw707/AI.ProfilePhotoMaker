using AI.ProfilePhotoMaker.API.Models;
using AI.ProfilePhotoMaker.API.Models.DTOs;

namespace AI.ProfilePhotoMaker.API.Services;

public interface IOutcomePackageService
{
    Task<IReadOnlyList<OutcomePackageDefinitionDto>> GetActivePackageDefinitionsAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<UserPackageEntitlementDto>> GetUserEntitlementsAsync(string userId, CancellationToken cancellationToken = default);
    Task<UserPackageEntitlement?> GrantEntitlementForCreditPackageAsync(string userId, int creditPackageId, string? paymentTransactionId, CancellationToken cancellationToken = default);
    Task<UserPackageEntitlement?> GetActiveEntitlementAsync(string userId, string packageCode, CancellationToken cancellationToken = default);
    Task<bool> ConsumeCandidatesAsync(string userId, string packageCode, int candidateCount, CancellationToken cancellationToken = default);
    Task<bool> ConsumeRefinementAsync(string userId, CancellationToken cancellationToken = default);
    Task<bool> ConsumePremiumAugmentationAsync(string userId, CancellationToken cancellationToken = default);
    Task<bool> ConsumeExportKitAsync(string userId, CancellationToken cancellationToken = default);
}
