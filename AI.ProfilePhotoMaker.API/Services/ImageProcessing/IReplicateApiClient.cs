namespace AI.ProfilePhotoMaker.API.Services.ImageProcessing;

/// <summary>
/// Facade interface combining all Replicate API service capabilities.
/// Existing consumers can continue to depend on this interface unchanged.
/// New consumers should prefer the specific sub-interfaces:
/// <see cref="IReplicateModelService"/>, <see cref="IReplicateTrainingService"/>,
/// <see cref="IReplicatePredictionService"/>, <see cref="IReplicateEnhancementService"/>
/// </summary>
public interface IReplicateApiClient :
    IReplicateModelService,
    IReplicateTrainingService,
    IReplicatePredictionService,
    IReplicateEnhancementService
{
}
