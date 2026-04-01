using AI.ProfilePhotoMaker.API.Models.Replicate;

namespace AI.ProfilePhotoMaker.API.Services.ImageProcessing;

/// <summary>
/// Photo enhancement operations using Replicate models (Flux Kontext Pro)
/// </summary>
public interface IReplicateEnhancementService
{
    Task<ReplicatePredictionResult> EnhancePhotoAsync(string userId, string imageUrl, string enhancementType = "professional");
}
