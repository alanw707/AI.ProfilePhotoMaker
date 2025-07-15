using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using AI.ProfilePhotoMaker.API.Data;
using AI.ProfilePhotoMaker.API.Models;

namespace AI.ProfilePhotoMaker.API.Services
{
    /// <summary>
    /// Service for managing user context and user-related database operations
    /// </summary>
    public class UserContextService : IUserContextService
    {
        private readonly ApplicationDbContext _context;
        private readonly IMemoryCache _cache;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ILogger<UserContextService> _logger;
        private readonly TimeSpan _defaultCacheExpiration = TimeSpan.FromMinutes(5);

        public UserContextService(
            ApplicationDbContext context,
            IMemoryCache cache,
            IHttpContextAccessor httpContextAccessor,
            ILogger<UserContextService> logger)
        {
            _context = context;
            _cache = cache;
            _httpContextAccessor = httpContextAccessor;
            _logger = logger;
        }

        /// <summary>
        /// Gets the current user ID from HTTP context claims
        /// </summary>
        public string? GetCurrentUserId()
        {
            return _httpContextAccessor.HttpContext?.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        }

        /// <summary>
        /// Gets the current user's profile
        /// </summary>
        public async Task<UserProfile?> GetCurrentUserProfileAsync()
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrEmpty(userId))
                return null;

            return await GetUserProfileAsync(userId);
        }

        /// <summary>
        /// Gets a user profile by user ID with caching
        /// </summary>
        public async Task<UserProfile?> GetUserProfileAsync(string userId)
        {
            if (string.IsNullOrEmpty(userId))
                return null;

            var cacheKey = $"user_profile_{userId}";
            return await GetOrSetCachedAsync(cacheKey, async () =>
            {
                return await _context.UserProfiles
                    .FirstOrDefaultAsync(p => p.UserId == userId);
            });
        }

        /// <summary>
        /// Gets user profile with processed images included
        /// </summary>
        public async Task<UserProfile?> GetUserProfileWithImagesAsync(string userId)
        {
            if (string.IsNullOrEmpty(userId))
                return null;

            // Don't cache this as images change frequently
            return await _context.UserProfiles
                .Include(up => up.ProcessedImages)
                .FirstOrDefaultAsync(up => up.UserId == userId);
        }

        /// <summary>
        /// Gets user profile with credit information (delegate to BasicTierService if needed)
        /// </summary>
        public async Task<UserProfile?> GetUserProfileWithCreditsAsync(string userId)
        {
            if (string.IsNullOrEmpty(userId))
                return null;

            // For now, just return regular profile - can be enhanced with credit-specific includes
            return await GetUserProfileAsync(userId);
        }

        /// <summary>
        /// Validates that a user exists in the database
        /// </summary>
        public async Task<bool> ValidateUserExistsAsync(string userId)
        {
            if (string.IsNullOrEmpty(userId))
                return false;

            // For bool values, handle caching manually to avoid nullable issues
            var cacheKey = $"user_exists_{userId}";
            if (_cache.TryGetValue(cacheKey, out bool cached))
            {
                return cached;
            }

            var exists = await _context.UserProfiles.AnyAsync(p => p.UserId == userId);

            var cacheOptions = new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10),
                SlidingExpiration = TimeSpan.FromMinutes(2)
            };
            _cache.Set(cacheKey, exists, cacheOptions);

            return exists;
        }

        /// <summary>
        /// Checks if the provided user ID matches the current authenticated user
        /// </summary>
        public bool IsCurrentUser(string userId)
        {
            var currentUserId = GetCurrentUserId();
            return !string.IsNullOrEmpty(currentUserId) && currentUserId == userId;
        }

        /// <summary>
        /// Gets the latest trained model for a user
        /// </summary>
        public async Task<ModelCreationRequest?> GetLatestTrainedModelAsync(string userId)
        {
            if (string.IsNullOrEmpty(userId))
                return null;

            var cacheKey = $"latest_model_{userId}";
            return await GetOrSetCachedAsync(cacheKey, async () =>
            {
                return await _context.ModelCreationRequests
                    .Where(m => m.UserId == userId && m.Status == ModelCreationStatus.Ready)
                    .OrderByDescending(m => m.CompletedAt)
                    .FirstOrDefaultAsync();
            }, TimeSpan.FromMinutes(15)); // Cache model status longer as it changes less frequently
        }

        /// <summary>
        /// Checks if user has any trained models
        /// </summary>
        public async Task<bool> HasTrainedModelAsync(string userId)
        {
            if (string.IsNullOrEmpty(userId))
                return false;

            // For bool values, handle caching manually to avoid nullable issues
            var cacheKey = $"has_models_{userId}";
            if (_cache.TryGetValue(cacheKey, out bool cached))
            {
                return cached;
            }

            var hasModels = await _context.ModelCreationRequests
                .AnyAsync(m => m.UserId == userId && m.Status == ModelCreationStatus.Ready);

            var cacheOptions = new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10),
                SlidingExpiration = TimeSpan.FromMinutes(2)
            };
            _cache.Set(cacheKey, hasModels, cacheOptions);

            return hasModels;
        }

        /// <summary>
        /// Gets all processed images for a user
        /// </summary>
        public async Task<List<ProcessedImage>> GetUserImagesAsync(string userId, bool includeDeleted = false)
        {
            if (string.IsNullOrEmpty(userId))
                return new List<ProcessedImage>();

            var query = _context.ProcessedImages
                .Include(pi => pi.UserProfile)
                .Where(pi => pi.UserProfile.UserId == userId);

            // Note: IsDeleted field removed - all images are now considered active

            return await query
                .OrderByDescending(pi => pi.CreatedAt)
                .ToListAsync();
        }

        /// <summary>
        /// Gets user's generated images (styled photos)
        /// </summary>
        public async Task<List<ProcessedImage>> GetUserGeneratedImagesAsync(string userId)
        {
            if (string.IsNullOrEmpty(userId))
                return new List<ProcessedImage>();

            return await _context.ProcessedImages
                .Include(pi => pi.UserProfile)
                .Where(pi => pi.UserProfile.UserId == userId)
                .Where(pi => !string.IsNullOrEmpty(pi.Style)) // Only styled/generated images
                .OrderByDescending(pi => pi.CreatedAt)
                .ToListAsync();
        }

        /// <summary>
        /// Invalidates cached user data
        /// </summary>
        public async Task InvalidateUserCacheAsync(string userId)
        {
            if (string.IsNullOrEmpty(userId))
                return;

            var cacheKeys = new[]
            {
                $"user_profile_{userId}",
                $"user_exists_{userId}",
                $"latest_model_{userId}",
                $"has_models_{userId}"
            };

            foreach (var key in cacheKeys)
            {
                _cache.Remove(key);
            }

            _logger.LogInformation("Invalidated cache for user {UserId}", userId);
            await Task.CompletedTask;
        }

        /// <summary>
        /// Generic method for getting or setting cached data
        /// </summary>
        public async Task<T?> GetOrSetCachedAsync<T>(string cacheKey, Func<Task<T?>> factory, TimeSpan? expiration = null)
        {
            if (_cache.TryGetValue(cacheKey, out T? cached))
            {
                return cached;
            }

            var value = await factory();
            if (value != null)
            {
                var cacheOptions = new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = expiration ?? _defaultCacheExpiration,
                    SlidingExpiration = TimeSpan.FromMinutes(2) // Refresh if accessed within 2 minutes
                };

                _cache.Set(cacheKey, value, cacheOptions);
            }

            return value;
        }
    }
}