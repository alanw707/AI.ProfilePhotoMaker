using AI.ProfilePhotoMaker.API.Models;
using AI.ProfilePhotoMaker.API.Models.DTOs;
using Microsoft.EntityFrameworkCore;

namespace AI.ProfilePhotoMaker.API.Data;

public class UserProfileRepository : IUserProfileRepository
{
    private readonly ApplicationDbContext _context;

    public UserProfileRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    #region Original Methods (Use Sparingly)

    /// <summary>
    /// ⚠️ PERFORMANCE WARNING: Eagerly loads ALL ProcessedImages
    /// Use GetByUserIdLightAsync() or GetProfileWithRecentImagesAsync() instead
    /// </summary>
    public async Task<UserProfile?> GetByUserIdAsync(string userId)
    {
        return await _context.UserProfiles
            .Include(p => p.ProcessedImages)
            .FirstOrDefaultAsync(p => p.UserId == userId);
    }

    #endregion

    #region Optimized Profile Loading

    /// <summary>
    /// OPTIMIZED: Loads profile metadata only (no ProcessedImages)
    /// Use this for most profile operations when images aren't needed
    /// </summary>
    public async Task<UserProfile?> GetByUserIdLightAsync(string userId)
    {
        return await _context.UserProfiles
            .FirstOrDefaultAsync(p => p.UserId == userId);
    }

    /// <summary>
    /// OPTIMIZED: Gets profile statistics without loading all images
    /// Perfect for dashboard and API responses
    /// </summary>
    public async Task<UserProfileStatsDto?> GetUserProfileStatsAsync(string userId)
    {
        var profile = await _context.UserProfiles
            .FirstOrDefaultAsync(p => p.UserId == userId);

        if (profile == null) return null;

        // Single efficient query for all statistics
        var imageStats = await _context.ProcessedImages
            .Where(i => i.UserProfileId == profile.Id)
            .GroupBy(i => 1)
            .Select(g => new
            {
                TotalCount = g.Count(),
                OriginalUploads = g.Count(i => i.IsOriginalUpload),
                GeneratedImages = g.Count(i => i.IsGenerated),
                LastOriginalUpload = g.Where(i => i.IsOriginalUpload)
                                     .Max(i => (DateTime?)i.CreatedAt),
                LastGeneration = g.Where(i => i.IsGenerated)
                                .Max(i => (DateTime?)i.CreatedAt)
            })
            .FirstOrDefaultAsync();

        return new UserProfileStatsDto
        {
            Id = profile.Id,
            UserId = profile.UserId,
            FirstName = profile.FirstName,
            LastName = profile.LastName,
            Gender = profile.Gender,
            Ethnicity = profile.Ethnicity,
            CreatedAt = profile.CreatedAt,
            UpdatedAt = profile.UpdatedAt,
            Credits = profile.Credits,
            TotalProcessedImages = imageStats?.TotalCount ?? 0,
            OriginalUploads = imageStats?.OriginalUploads ?? 0,
            GeneratedImages = imageStats?.GeneratedImages ?? 0,
            LastImageUpload = imageStats?.LastOriginalUpload,
            LastImageGeneration = imageStats?.LastGeneration
        };
    }

    /// <summary>
    /// OPTIMIZED: Gets profile with only recent images (default 10)
    /// Excellent for profile pages that show recent activity
    /// </summary>
    public async Task<UserProfileWithRecentImagesDto?> GetProfileWithRecentImagesAsync(string userId, int recentCount = 10)
    {
        var profile = await GetByUserIdLightAsync(userId);
        if (profile == null) return null;

        // Get total count efficiently
        var totalCount = await _context.ProcessedImages
            .CountAsync(i => i.UserProfileId == profile.Id);

        // Get recent images with proper indexing
        var recentImages = await _context.ProcessedImages
            .Where(i => i.UserProfileId == profile.Id)
            .OrderByDescending(i => i.CreatedAt)
            .Take(recentCount)
            .Select(i => new ProcessedImageDto
            {
                Id = i.Id,
                OriginalImageUrl = i.OriginalImageUrl,
                ProcessedImageUrl = i.ProcessedImageUrl,
                Style = i.Style,
                CreatedAt = i.CreatedAt,
                IsGenerated = i.IsGenerated,
                IsOriginalUpload = i.IsOriginalUpload
            })
            .ToListAsync();

        return new UserProfileWithRecentImagesDto
        {
            Profile = MapToUserProfileDto(profile),
            RecentImages = recentImages,
            TotalImageCount = totalCount
        };
    }

    #endregion

    #region Optimized Image Loading

    /// <summary>
    /// OPTIMIZED: Paginated image loading with efficient queries
    /// Essential for image galleries and large collections
    /// </summary>
    public async Task<PagedResult<ProcessedImageDto>> GetUserImagesPagedAsync(string userId, int page = 1, int pageSize = 20)
    {
        // Validate parameters
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100); // Prevent abuse

        // Get profile first to validate user
        var profile = await GetByUserIdLightAsync(userId);
        if (profile == null)
        {
            return new PagedResult<ProcessedImageDto> { Items = new List<ProcessedImageDto>() };
        }

        // Build efficient query with proper indexing
        var query = _context.ProcessedImages
            .Where(i => i.UserProfileId == profile.Id)
            .OrderByDescending(i => i.CreatedAt);

        // Get total count
        var totalCount = await query.CountAsync();

        // Get page of data with projection
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(i => new ProcessedImageDto
            {
                Id = i.Id,
                OriginalImageUrl = i.OriginalImageUrl,
                ProcessedImageUrl = i.ProcessedImageUrl,
                Style = i.Style,
                CreatedAt = i.CreatedAt,
                IsGenerated = i.IsGenerated,
                IsOriginalUpload = i.IsOriginalUpload
            })
            .ToListAsync();

        return new PagedResult<ProcessedImageDto>
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    /// <summary>
    /// OPTIMIZED: Get user images filtered by style with pagination
    /// </summary>
    public async Task<PagedResult<ProcessedImageDto>> GetUserImagesByStyleAsync(string userId, string style, int page = 1, int pageSize = 20)
    {
        // Validate parameters
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var profile = await GetByUserIdLightAsync(userId);
        if (profile == null)
        {
            return new PagedResult<ProcessedImageDto> { Items = new List<ProcessedImageDto>() };
        }

        var query = _context.ProcessedImages
            .Where(i => i.UserProfileId == profile.Id && i.Style == style)
            .OrderByDescending(i => i.CreatedAt);

        var totalCount = await query.CountAsync();

        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(i => new ProcessedImageDto
            {
                Id = i.Id,
                OriginalImageUrl = i.OriginalImageUrl,
                ProcessedImageUrl = i.ProcessedImageUrl,
                Style = i.Style,
                CreatedAt = i.CreatedAt,
                IsGenerated = i.IsGenerated,
                IsOriginalUpload = i.IsOriginalUpload
            })
            .ToListAsync();

        return new PagedResult<ProcessedImageDto>
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    #endregion

    #region Optimized Count Operations

    /// <summary>
    /// OPTIMIZED: Get total image count without loading data
    /// </summary>
    public async Task<int> GetUserImageCountAsync(string userId)
    {
        var profile = await GetByUserIdLightAsync(userId);
        if (profile == null) return 0;

        return await _context.ProcessedImages
            .CountAsync(i => i.UserProfileId == profile.Id);
    }

    /// <summary>
    /// OPTIMIZED: Get original upload count without loading data
    /// </summary>
    public async Task<int> GetUserOriginalUploadCountAsync(string userId)
    {
        var profile = await GetByUserIdLightAsync(userId);
        if (profile == null) return 0;

        return await _context.ProcessedImages
            .CountAsync(i => i.UserProfileId == profile.Id && i.IsOriginalUpload);
    }

    /// <summary>
    /// OPTIMIZED: Get generated image count without loading data
    /// </summary>
    public async Task<int> GetUserGeneratedImageCountAsync(string userId)
    {
        var profile = await GetByUserIdLightAsync(userId);
        if (profile == null) return 0;

        return await _context.ProcessedImages
            .CountAsync(i => i.UserProfileId == profile.Id && i.IsGenerated);
    }

    #endregion

    #region Optimized Existence Checks

    /// <summary>
    /// OPTIMIZED: Check if user has processed images without loading them
    /// </summary>
    public async Task<bool> HasProcessedImagesAsync(string userId)
    {
        var profile = await GetByUserIdLightAsync(userId);
        if (profile == null) return false;

        return await _context.ProcessedImages
            .AnyAsync(i => i.UserProfileId == profile.Id);
    }

    /// <summary>
    /// OPTIMIZED: Check if user has minimum number of original uploads
    /// Perfect for training requirements (e.g., minimum 5 images)
    /// </summary>
    public async Task<bool> HasOriginalUploadsAsync(string userId, int minimumCount = 1)
    {
        var profile = await GetByUserIdLightAsync(userId);
        if (profile == null) return false;

        var count = await _context.ProcessedImages
            .CountAsync(i => i.UserProfileId == profile.Id && i.IsOriginalUpload);

        return count >= minimumCount;
    }

    #endregion

    #region Bulk Operations

    /// <summary>
    /// OPTIMIZED: Get user's processed images with filtering options
    /// Use when you actually need the full ProcessedImage entities
    /// </summary>
    public async Task<List<ProcessedImage>> GetUserProcessedImagesAsync(string userId, bool includeGenerated = true, bool includeOriginal = true)
    {
        var profile = await GetByUserIdLightAsync(userId);
        if (profile == null) return new List<ProcessedImage>();

        var query = _context.ProcessedImages
            .Where(i => i.UserProfileId == profile.Id);

        if (!includeGenerated)
        {
            query = query.Where(i => !i.IsGenerated);
        }

        if (!includeOriginal)
        {
            query = query.Where(i => !i.IsOriginalUpload);
        }

        return await query
            .OrderByDescending(i => i.CreatedAt)
            .ToListAsync();
    }

    /// <summary>
    /// OPTIMIZED: Delete a single user image efficiently
    /// </summary>
    public async Task DeleteUserImageAsync(string userId, int imageId)
    {
        var profile = await GetByUserIdLightAsync(userId);
        if (profile == null) return;

        var image = await _context.ProcessedImages
            .FirstOrDefaultAsync(i => i.Id == imageId && i.UserProfileId == profile.Id);

        if (image != null)
        {
            _context.ProcessedImages.Remove(image);
            await _context.SaveChangesAsync();
        }
    }

    /// <summary>
    /// OPTIMIZED: Delete multiple user images in a single operation
    /// </summary>
    public async Task DeleteUserImagesAsync(string userId, List<int> imageIds)
    {
        var profile = await GetByUserIdLightAsync(userId);
        if (profile == null || !imageIds.Any()) return;

        var imagesToDelete = await _context.ProcessedImages
            .Where(i => imageIds.Contains(i.Id) && i.UserProfileId == profile.Id)
            .ToListAsync();

        if (imagesToDelete.Any())
        {
            _context.ProcessedImages.RemoveRange(imagesToDelete);
            await _context.SaveChangesAsync();
        }
    }

    #endregion

    #region Standard CRUD Operations

    public async Task AddAsync(UserProfile profile)
    {
        _context.UserProfiles.Add(profile);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateAsync(UserProfile profile)
    {
        // Check if entity is already being tracked
        var existingEntry = _context.Entry(profile);
        if (existingEntry.State == EntityState.Detached)
        {
            _context.UserProfiles.Update(profile);
        }
        else
        {
            // Entity is already tracked, just save changes
            existingEntry.State = EntityState.Modified;
        }
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(UserProfile profile)
    {
        _context.UserProfiles.Remove(profile);
        await _context.SaveChangesAsync();
    }

    #endregion

    #region Helper Methods

    private UserProfileDto MapToUserProfileDto(UserProfile profile)
    {
        return new UserProfileDto
        {
            Id = profile.Id,
            FirstName = profile.FirstName,
            LastName = profile.LastName,
            Gender = profile.Gender,
            Ethnicity = profile.Ethnicity,
            CreatedAt = profile.CreatedAt,
            UpdatedAt = profile.UpdatedAt,
            TotalProcessedImages = 0 // Will be populated by calling method if needed
        };
    }

    #endregion
}
