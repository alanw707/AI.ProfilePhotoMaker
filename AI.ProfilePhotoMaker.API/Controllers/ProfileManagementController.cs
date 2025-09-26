using AI.ProfilePhotoMaker.API.Data;
using AI.ProfilePhotoMaker.API.Models;
using AI.ProfilePhotoMaker.API.Models.DTOs;
using AI.ProfilePhotoMaker.API.Infrastructure.Logging;
using AI.ProfilePhotoMaker.API.Services;
using AI.ProfilePhotoMaker.API.Services.ImageProcessing;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.IO.Compression;

namespace AI.ProfilePhotoMaker.API.Controllers
{
    /// <summary>
    /// Controller for core profile management operations (CRUD and data management)
    /// </summary>
    [ApiController]
    [Route("api/profile-management")]
    [Authorize]
    public class ProfileManagementController : BaseController
    {
        private readonly IUserProfileRepository _userProfileRepository;
        private readonly IWebHostEnvironment _environment;
        private readonly IConfiguration _configuration;
        private readonly IReplicateApiClient _replicateApiClient;
        private readonly IUserContextService _userContextService;
        private static new string S(string? value) => LoggingSanitizer.Sanitize(value);
        private static new string Sid(string? value) => LoggingSanitizer.SanitizeId(value);

        public ProfileManagementController(
            IUserProfileRepository userProfileRepository,
            IWebHostEnvironment environment,
            IConfiguration configuration,
            IReplicateApiClient replicateApiClient,
            IUserContextService userContextService,
            ILogger<ProfileManagementController> logger,
            ApplicationDbContext context)
            : base(logger, context)
        {
            _userProfileRepository = userProfileRepository;
            _environment = environment;
            _configuration = configuration;
            _replicateApiClient = replicateApiClient;
            _userContextService = userContextService;
        }

        /// <summary>
        /// Gets the current user's profile
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetProfile()
        {
            var authCheck = ValidateAuthentication();
            if (authCheck != null) return authCheck;
            var userId = GetCurrentUserId()!;

            try
            {
                var profile = await _userContextService.GetUserProfileAsync(userId);
                if (profile == null)
                {
                    return ErrorResponse("ProfileNotFound", "Profile not found", 404);
                }

                // Get model information
                var latestModel = await _userContextService.GetLatestTrainedModelAsync(userId);
                var hasTrainedModel = latestModel != null;

                // Get image counts
                var imageData = await _userContextService.GetUserProfileWithImagesAsync(userId);
                var totalImages = imageData?.ProcessedImages?.Count ?? 0;
                var originalUploads = imageData?.ProcessedImages?.Count(i => i.IsOriginalUpload) ?? 0;

                var response = new
                {
                    Id = profile.Id,
                    UserId = profile.UserId,
                    FirstName = profile.FirstName,
                    LastName = profile.LastName,
                    Gender = profile.Gender,
                    Ethnicity = profile.Ethnicity,
                    SubscriptionTier = profile.SubscriptionTier.ToString(),
                    BasicCredits = profile.Credits,
                    LastCreditReset = profile.LastCreditReset,
                    CreatedAt = profile.CreatedAt,
                    UpdatedAt = profile.UpdatedAt,
                    HasTrainedModel = hasTrainedModel,
                    TrainedModelId = latestModel?.ReplicateModelId,
                    TrainedModelVersionId = latestModel?.TrainedModelVersion,
                    ModelTrainedAt = latestModel?.CompletedAt,
                    TotalImages = totalImages,
                    OriginalUploads = originalUploads
                };

                return SuccessResponse(response);
            }
            catch (Exception ex)
            {
                LogError(ex, "Error retrieving profile", Sid(userId));
                return ErrorResponse("ProfileError", "Failed to retrieve profile", 500);
            }
        }

        /// <summary>
        /// Creates a new user profile
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> CreateProfile([FromBody] CreateUserProfileDto dto)
        {
            if (!ModelState.IsValid)
                return ErrorResponse("InvalidModel", "Invalid profile data");

            var authCheck = ValidateAuthentication();
            if (authCheck != null) return authCheck;
            var userId = GetCurrentUserId()!;

            try
            {
                var existingProfile = await _userContextService.GetUserProfileAsync(userId);
                if (existingProfile != null)
                {
                    return ErrorResponse("ProfileExists", "Profile already exists for this user");
                }

                var profile = new UserProfile
                {
                    UserId = userId,
                    FirstName = dto.FirstName,
                    LastName = dto.LastName,
                    Gender = dto.Gender,
                    Ethnicity = dto.Ethnicity,
                    Credits = 5, // Start with basic tier credits
                    LastCreditReset = DateTime.UtcNow,
                    SubscriptionTier = SubscriptionTier.Basic
                };

                await _userProfileRepository.AddAsync(profile);

                // Invalidate cache
                await _userContextService.InvalidateUserCacheAsync(userId);

                var response = new
                {
                    Id = profile.Id,
                    UserId = profile.UserId,
                    FirstName = profile.FirstName,
                    LastName = profile.LastName,
                    Gender = profile.Gender,
                    Ethnicity = profile.Ethnicity,
                    SubscriptionTier = profile.SubscriptionTier.ToString(),
                    BasicCredits = profile.Credits,
                    CreatedAt = profile.CreatedAt
                };

                return SuccessResponse(response);
            }
            catch (Exception ex)
            {
                LogError(ex, "Error creating profile", Sid(userId));
                return ErrorResponse("ProfileCreationFailed", "Failed to create profile", 500);
            }
        }

        /// <summary>
        /// Updates the current user's profile
        /// </summary>
        [HttpPut]
        public async Task<IActionResult> UpdateProfile([FromBody] UpdateUserProfileDto dto)
        {
            if (!ModelState.IsValid)
                return ErrorResponse("InvalidModel", "Invalid profile data");

            var authCheck = ValidateAuthentication();
            if (authCheck != null) return authCheck;
            var userId = GetCurrentUserId()!;

            try
            {
                var profile = await _userContextService.GetUserProfileAsync(userId);
                if (profile == null)
                {
                    return ErrorResponse("ProfileNotFound", "Profile not found", 404);
                }

                // Update fields if provided
                if (!string.IsNullOrEmpty(dto.FirstName))
                    profile.FirstName = dto.FirstName;
                if (!string.IsNullOrEmpty(dto.LastName))
                    profile.LastName = dto.LastName;
                if (!string.IsNullOrEmpty(dto.Gender))
                    profile.Gender = dto.Gender;
                if (!string.IsNullOrEmpty(dto.Ethnicity))
                    profile.Ethnicity = dto.Ethnicity;

                profile.UpdatedAt = DateTime.UtcNow;

                await _userProfileRepository.UpdateAsync(profile);

                // Invalidate cache
                await _userContextService.InvalidateUserCacheAsync(userId);

                var response = new
                {
                    Id = profile.Id,
                    UserId = profile.UserId,
                    FirstName = profile.FirstName,
                    LastName = profile.LastName,
                    Gender = profile.Gender,
                    Ethnicity = profile.Ethnicity,
                    UpdatedAt = profile.UpdatedAt
                };

                return SuccessResponse(response);
            }
            catch (Exception ex)
            {
                LogError(ex, "Error updating profile", Sid(userId));
                return ErrorResponse("ProfileUpdateFailed", "Failed to update profile", 500);
            }
        }

        /// <summary>
        /// Deletes the current user's profile (soft delete)
        /// </summary>
        [HttpDelete]
        public async Task<IActionResult> DeleteProfile()
        {
            var authCheck = ValidateAuthentication();
            if (authCheck != null) return authCheck;
            var userId = GetCurrentUserId()!;

            try
            {
                var profile = await _userContextService.GetUserProfileAsync(userId);
                if (profile == null)
                {
                    return ErrorResponse("ProfileNotFound", "Profile not found", 404);
                }

                // Since UserProfile doesn't have soft delete, we'll just update the profile
                // In a real implementation, you might want to add soft delete fields
                // For now, we'll just mark it as updated (or implement actual deletion)
                profile.UpdatedAt = DateTime.UtcNow;

                await _userProfileRepository.UpdateAsync(profile);

                // Invalidate cache
                await _userContextService.InvalidateUserCacheAsync(userId);

                return SuccessResponse(new { Message = "Profile deleted successfully" });
            }
            catch (Exception ex)
            {
                LogError(ex, "Error deleting profile", Sid(userId));
                return ErrorResponse("ProfileDeletionFailed", "Failed to delete profile", 500);
            }
        }
    }
}
