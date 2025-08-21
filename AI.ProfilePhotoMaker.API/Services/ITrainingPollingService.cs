namespace AI.ProfilePhotoMaker.API.Services;

/// <summary>
/// Service for polling training status and processing completion
/// </summary>
public interface ITrainingPollingService
{
    /// <summary>
    /// Start polling for a specific training completion
    /// </summary>
    /// <param name="trainingId">The training ID to poll</param>
    /// <param name="userId">The user ID who owns this training</param>
    Task StartPollingForTraining(string trainingId, string userId);

    /// <summary>
    /// Check if a training has completed
    /// </summary>
    /// <param name="trainingId">The training ID to check</param>
    /// <returns>True if training is complete (succeeded or failed)</returns>
    Task<bool> IsTrainingComplete(string trainingId);

    /// <summary>
    /// Process a completed training - update database and generate initial images
    /// </summary>
    /// <param name="trainingId">The training ID that completed</param>
    Task ProcessTrainingCompletion(string trainingId);
}