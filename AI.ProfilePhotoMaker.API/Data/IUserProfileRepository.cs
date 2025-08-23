using AI.ProfilePhotoMaker.API.Models;
using AI.ProfilePhotoMaker.API.Models.DTOs;

namespace AI.ProfilePhotoMaker.API.Data;

public interface IUserProfileRepository
{
    // Original method - use sparingly, only when all data truly needed
    Task<UserProfile?> GetByUserIdAsync(string userId);

    // OPTIMIZED: Lightweight profile loading without ProcessedImages
    Task<UserProfile?> GetByUserIdLightAsync(string userId);

    // OPTIMIZED: Paginated image loading with efficient queries
    Task<PagedResult<ProcessedImageDto>> GetUserImagesPagedAsync(string userId, int page = 1, int pageSize = 20);

    // OPTIMIZED: Statistics without loading all images
    Task<UserProfileStatsDto?> GetUserProfileStatsAsync(string userId);

    // OPTIMIZED: Profile with recent images only
    Task<UserProfileWithRecentImagesDto?> GetProfileWithRecentImagesAsync(string userId, int recentCount = 10);

    // OPTIMIZED: Count operations without loading data
    Task<int> GetUserImageCountAsync(string userId);
    Task<int> GetUserOriginalUploadCountAsync(string userId);
    Task<int> GetUserGeneratedImageCountAsync(string userId);

    // OPTIMIZED: Check if user has specific data without loading
    Task<bool> HasProcessedImagesAsync(string userId);
    Task<bool> HasOriginalUploadsAsync(string userId, int minimumCount = 1);

    // OPTIMIZED: Get images by style with pagination
    Task<PagedResult<ProcessedImageDto>> GetUserImagesByStyleAsync(string userId, string style, int page = 1, int pageSize = 20);

    // Standard CRUD operations
    Task AddAsync(UserProfile profile);
    Task UpdateAsync(UserProfile profile);
    Task DeleteAsync(UserProfile profile);

    // OPTIMIZED: Bulk operations for data management
    Task<List<ProcessedImage>> GetUserProcessedImagesAsync(string userId, bool includeGenerated = true, bool includeOriginal = true);
    Task DeleteUserImageAsync(string userId, int imageId);
    Task DeleteUserImagesAsync(string userId, List<int> imageIds);
}

// New DTOs for optimized data transfer
public class PagedResult<T>
{
    public List<T> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
    public bool HasNextPage => Page < TotalPages;
    public bool HasPreviousPage => Page > 1;
}

public class ProcessedImageDto
{
    public int Id { get; set; }
    public string OriginalImageUrl { get; set; } = string.Empty;
    public string ProcessedImageUrl { get; set; } = string.Empty;
    public string Style { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public bool IsGenerated { get; set; }
    public bool IsOriginalUpload { get; set; }
}

public class UserProfileStatsDto
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? Gender { get; set; }
    public string? Ethnicity { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public int Credits { get; set; }
    public int PurchasedCredits { get; set; }

    // Efficient statistics
    public int TotalProcessedImages { get; set; }
    public int OriginalUploads { get; set; }
    public int GeneratedImages { get; set; }
    public DateTime? LastImageUpload { get; set; }
    public DateTime? LastImageGeneration { get; set; }
}

public class UserProfileWithRecentImagesDto
{
    public UserProfileDto Profile { get; set; } = null!;
    public List<ProcessedImageDto> RecentImages { get; set; } = new();
    public int TotalImageCount { get; set; }
}