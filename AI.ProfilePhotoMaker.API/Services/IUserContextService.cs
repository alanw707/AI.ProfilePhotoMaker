using AI.ProfilePhotoMaker.API.Models;

namespace AI.ProfilePhotoMaker.API.Services
{
    /// <summary>
    /// Service for managing user context and user-related database operations
    /// </summary>
    public interface IUserContextService
    {
        /// <summary>
        /// Gets the current user ID from HTTP context claims
        /// </summary>
        /// <returns>User ID or null if not authenticated</returns>
        string? GetCurrentUserId();

        /// <summary>
        /// Gets the current user's profile
        /// </summary>
        /// <returns>UserProfile or null if not found</returns>
        Task<UserProfile?> GetCurrentUserProfileAsync();

        /// <summary>
        /// Gets a user profile by user ID
        /// </summary>
        /// <param name="userId">User ID</param>
        /// <returns>UserProfile or null if not found</returns>
        Task<UserProfile?> GetUserProfileAsync(string userId);

        /// <summary>
        /// Gets user profile with processed images included
        /// </summary>
        /// <param name="userId">User ID</param>
        /// <returns>UserProfile with ProcessedImages or null if not found</returns>
        Task<UserProfile?> GetUserProfileWithImagesAsync(string userId);

        /// <summary>
        /// Gets user profile with credit information
        /// </summary>
        /// <param name="userId">User ID</param>
        /// <returns>UserProfile with credit data or null if not found</returns>
        Task<UserProfile?> GetUserProfileWithCreditsAsync(string userId);

        /// <summary>
        /// Validates that a user exists in the database
        /// </summary>
        /// <param name="userId">User ID to validate</param>
        /// <returns>True if user exists, false otherwise</returns>
        Task<bool> ValidateUserExistsAsync(string userId);

        /// <summary>
        /// Checks if the provided user ID matches the current authenticated user
        /// </summary>
        /// <param name="userId">User ID to check</param>
        /// <returns>True if it's the current user, false otherwise</returns>
        bool IsCurrentUser(string userId);

        /// <summary>
        /// Gets the latest trained model for a user
        /// </summary>
        /// <param name="userId">User ID</param>
        /// <returns>Latest trained ModelCreationRequest or null if none found</returns>
        Task<ModelCreationRequest?> GetLatestTrainedModelAsync(string userId);

        /// <summary>
        /// Checks if user has any trained models
        /// </summary>
        /// <param name="userId">User ID</param>
        /// <returns>True if user has trained models, false otherwise</returns>
        Task<bool> HasTrainedModelAsync(string userId);

        /// <summary>
        /// Gets all processed images for a user
        /// </summary>
        /// <param name="userId">User ID</param>
        /// <param name="includeDeleted">Whether to include soft-deleted images</param>
        /// <returns>List of ProcessedImages</returns>
        Task<List<ProcessedImage>> GetUserImagesAsync(string userId, bool includeDeleted = false);

        /// <summary>
        /// Gets user's generated images (styled photos)
        /// </summary>
        /// <param name="userId">User ID</param>
        /// <returns>List of generated ProcessedImages</returns>
        Task<List<ProcessedImage>> GetUserGeneratedImagesAsync(string userId);

        /// <summary>
        /// Invalidates cached user data
        /// </summary>
        /// <param name="userId">User ID to invalidate cache for</param>
        Task InvalidateUserCacheAsync(string userId);

        /// <summary>
        /// Generic method for getting or setting cached data
        /// </summary>
        /// <typeparam name="T">Type of data to cache</typeparam>
        /// <param name="cacheKey">Cache key</param>
        /// <param name="factory">Function to get data if not cached</param>
        /// <param name="expiration">Cache expiration time (default: 5 minutes)</param>
        /// <returns>Cached or freshly retrieved data</returns>
        Task<T?> GetOrSetCachedAsync<T>(string cacheKey, Func<Task<T?>> factory, TimeSpan? expiration = null);
    }
}