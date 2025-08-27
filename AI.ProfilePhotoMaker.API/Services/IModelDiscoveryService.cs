using AI.ProfilePhotoMaker.API.Models.DTOs;

namespace AI.ProfilePhotoMaker.API.Services;

/// <summary>
/// Service for discovering and syncing user models between Replicate and database
/// </summary>
public interface IModelDiscoveryService
{
    /// <summary>
    /// Discovers user models on Replicate and syncs them with the database
    /// </summary>
    /// <param name="userId">The user ID to discover models for</param>
    /// <returns>The sync result with details about models added/removed</returns>
    Task<ModelSyncResult> DiscoverAndSyncUserModelsAsync(string userId);

    /// <summary>
    /// Gets the current model sync status for a user
    /// </summary>
    /// <param name="userId">The user ID</param>
    /// <returns>The current model status</returns>
    Task<ModelSyncStatus> GetModelSyncStatusAsync(string userId);

    /// <summary>
    /// Repairs model versions by fetching actual version IDs from Replicate API
    /// </summary>
    /// <param name="userId">The user ID</param>
    /// <returns>The repair result with details about models repaired</returns>
    Task<ModelVersionRepairResult> RepairModelVersionsAsync(string userId);

    /// <summary>
    /// Manually syncs a specific model to the database
    /// </summary>
    /// <param name="userId">The user ID</param>
    /// <param name="modelId">The model ID (owner/model-name)</param>
    /// <param name="versionId">The model version ID</param>
    /// <returns>True if sync was successful</returns>
    Task<bool> SyncSpecificModelAsync(string userId, string modelId, string versionId);

    /// <summary>
    /// Quick database-only check for user's trained model (no API calls)
    /// </summary>
    /// <param name="userId">The user ID</param>
    /// <returns>Quick check result with model info if found</returns>
    Task<QuickModelCheckResult> QuickDatabaseCheckAsync(string userId);

    /// <summary>
    /// Manual override for suspected false negative model deletion
    /// </summary>
    /// <param name="userId">The user ID</param>
    /// <param name="modelId">The model ID to restore</param>
    /// <param name="reason">Reason for the override</param>
    /// <param name="overriddenBy">Who is performing the override</param>
    /// <returns>True if override was successful</returns>
    Task<bool> OverrideModelDeletionAsync(string userId, string modelId, string reason, string overriddenBy);
}

/// <summary>
/// Result of model discovery and sync operation
/// </summary>
public class ModelSyncResult
{
    public int ModelsFound { get; set; }
    public int ModelsAdded { get; set; }
    public int ModelsRemoved { get; set; }
    public List<string> AddedModelIds { get; set; } = new();
    public List<string> RemovedModelIds { get; set; } = new();
    public string Message { get; set; } = string.Empty;
    public bool Success { get; set; }
}

/// <summary>
/// Current model sync status for a user
/// </summary>
public class ModelSyncStatus
{
    public string UserId { get; set; } = string.Empty;
    public bool HasTrainedModel { get; set; }
    public string? TrainedModelId { get; set; }
    public string? TrainedModelVersionId { get; set; }
    public DateTime? ModelTrainedAt { get; set; }
    public int ModelsFoundOnReplicate { get; set; }
    public int ModelsInDatabase { get; set; }
    public DateTime LastSyncAttempt { get; set; }
    public bool SyncNeeded { get; set; }
}

/// <summary>
/// Result of quick database-only model check
/// </summary>
public class QuickModelCheckResult
{
    public bool HasModel { get; set; }
    public string? ModelId { get; set; }
    public string? VersionId { get; set; }
    public DateTime? ModelTrainedAt { get; set; }
    public DateTime? LastSyncCheck { get; set; }
    public bool ShouldRunDiscovery { get; set; }
}