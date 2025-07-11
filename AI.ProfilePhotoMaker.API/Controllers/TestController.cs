using AI.ProfilePhotoMaker.API.Data;
using AI.ProfilePhotoMaker.API.Services.ImageProcessing;
using AI.ProfilePhotoMaker.API.Services;
using AI.ProfilePhotoMaker.API.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace AI.ProfilePhotoMaker.API.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize]
public class TestController : ControllerBase
{
    private readonly IReplicateApiClient _replicateApiClient;
    private readonly IBasicTierService _basicTierService;
    private readonly ILogger<TestController> _logger;
    private readonly ApplicationDbContext _context;

    public TestController(IReplicateApiClient replicateApiClient, IBasicTierService basicTierService, ILogger<TestController> logger, ApplicationDbContext context)
    {
        _replicateApiClient = replicateApiClient;
        _basicTierService = basicTierService;
        _logger = logger;
        _context = context;
    }

    /// <summary>
    /// Test Replicate API connection by attempting to list available models
    /// </summary>
    [HttpPost("fix-generated-images")]
    public async Task<IActionResult> FixGeneratedImages()
    {
        try
        {
            _logger.LogInformation("Starting fix-generated-images for user");
            
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (userId == null)
            {
                _logger.LogWarning("No user ID found in claims");
                return Unauthorized(new { success = false, error = "No user ID found" });
            }

            _logger.LogInformation("Processing fix-generated-images for user: {UserId}", userId);

            var userProfile = await _context.UserProfiles
                .Include(up => up.ProcessedImages)
                .FirstOrDefaultAsync(up => up.UserId == userId);

            if (userProfile == null)
            {
                _logger.LogWarning("User profile not found for user: {UserId}", userId);
                return NotFound(new { success = false, error = "Profile not found" });
            }

            _logger.LogInformation("Found user profile: {ProfileId}", userProfile.Id);

            // Get existing generated images for duplicate checking
            var existingGenerated = userProfile.ProcessedImages.Where(img => img.IsGenerated).ToList();
            var existingGeneratedPaths = existingGenerated.Select(img => img.ProcessedImageUrl).ToHashSet();
            _logger.LogInformation("User has {ExistingCount} existing generated images in database", existingGenerated.Count);

            // Look for generated images in the file system
            var generatedPath = Path.Combine(Directory.GetCurrentDirectory(), "generated", userId);
            _logger.LogInformation("Checking generated images folder: {GeneratedPath}", generatedPath);
            
            if (!Directory.Exists(generatedPath))
            {
                _logger.LogWarning("Generated images folder not found: {GeneratedPath}", generatedPath);
                return NotFound(new { success = false, error = "No generated images folder found", path = generatedPath });
            }

            // Support multiple image formats
            var imageExtensions = new[] { "*.png", "*.jpg", "*.jpeg", "*.webp" };
            var imageFiles = new List<string>();
            
            foreach (var extension in imageExtensions)
            {
                imageFiles.AddRange(Directory.GetFiles(generatedPath, extension));
            }
            
            _logger.LogInformation("Found {FileCount} image files in generated folder (PNG, JPG, JPEG, WebP)", imageFiles.Count);

            if (imageFiles.Count == 0)
            {
                return Ok(new { 
                    success = true, 
                    message = "No image files found in generated folder",
                    data = new { addedCount = 0, folderPath = generatedPath }
                });
            }

            var addedImages = new List<object>();
            var errorFiles = new List<string>();

            foreach (var filePath in imageFiles)
            {
                try
                {
                    var fileName = Path.GetFileName(filePath);
                    var relativePath = $"/generated/{userId}/{fileName}";
                    
                    // Skip if this image already exists in database
                    if (existingGeneratedPaths.Contains(relativePath))
                    {
                        _logger.LogDebug("Skipping existing image: {FileName}", fileName);
                        continue;
                    }
                    
                    // Remove file extension to parse style
                    var fileNameWithoutExt = Path.GetFileNameWithoutExtension(fileName);
                    var parts = fileNameWithoutExt.Split('_');
                    
                    _logger.LogDebug("Processing file: {FileName}, parts: {Parts}", fileName, string.Join(", ", parts));
                    
                    if (parts.Length >= 2)
                    {
                        string style;
                        
                        // Handle both naming patterns:
                        // Old: {style}_{number} (e.g., fitness_2)
                        // New: {style}_{timestamp}_{guid} (e.g., fitness_20250702123456_a1b2c3d4e7f8)
                        if (parts.Length >= 3 && parts[parts.Length - 1].Length == 32 && IsHexString(parts[parts.Length - 1]))
                        {
                            // New pattern: style is everything except the last two parts (timestamp and GUID)
                            style = string.Join("_", parts.Take(parts.Length - 2));
                            _logger.LogDebug("Detected new naming pattern for {FileName}, extracted style: {Style}", fileName, style);
                        }
                        else if (parts.Length >= 2 && int.TryParse(parts[parts.Length - 1], out _))
                        {
                            // Old pattern: style is everything except the last part (number)
                            style = string.Join("_", parts.Take(parts.Length - 1));
                            _logger.LogDebug("Detected old naming pattern for {FileName}, extracted style: {Style}", fileName, style);
                        }
                        else
                        {
                            // Fallback: treat the whole filename as style (minus extension)
                            style = fileNameWithoutExt;
                            _logger.LogDebug("Using fallback naming for {FileName}, style: {Style}", fileName, style);
                        }

                        // Create new ProcessedImage record with duplicate protection
                        var processedImage = new ProcessedImage
                        {
                            UserProfileId = userProfile.Id,
                            OriginalImageUrl = relativePath, // Store relative path for local images
                            ProcessedImageUrl = relativePath,
                            Style = style,
                            IsGenerated = true,
                            IsOriginalUpload = false,
                            CreatedAt = DateTime.UtcNow
                        };

                        // Set scheduled deletion date
                        processedImage.SetScheduledDeletionDate();

                        try
                        {
                            _context.ProcessedImages.Add(processedImage);
                            // Don't save here - batch save at the end for better performance
                        }
                        catch (Exception addEx)
                        {
                            _logger.LogWarning(addEx, "Failed to add ProcessedImage for {FileName} - likely duplicate", fileName);
                            continue; // Skip this file and continue with others
                        }
                        
                        addedImages.Add(new
                        {
                            fileName = fileName,
                            style = style,
                            path = relativePath
                        });
                        
                        _logger.LogDebug("Added image to database: {FileName} -> {Style}", fileName, style);
                    }
                    else
                    {
                        errorFiles.Add($"{fileName} (invalid filename format)");
                        _logger.LogWarning("Skipping file with invalid format: {FileName}", fileName);
                    }
                }
                catch (Exception fileEx)
                {
                    errorFiles.Add(Path.GetFileName(filePath));
                    _logger.LogError(fileEx, "Error processing file: {FilePath}", filePath);
                }
            }

            _logger.LogInformation("Attempting to save {AddedCount} images to database", addedImages.Count);
            
            try
            {
                await _context.SaveChangesAsync();
                _logger.LogInformation("Successfully saved {AddedCount} images to database", addedImages.Count);
            }
            catch (Microsoft.EntityFrameworkCore.DbUpdateException ex) when (ex.InnerException?.Message?.Contains("UNIQUE constraint failed") == true)
            {
                // Handle unique constraint violations gracefully
                _logger.LogWarning(ex, "Some images were already in database due to unique constraint - this is expected");
                
                // Count how many records failed due to constraint violations
                var failedEntries = _context.ChangeTracker.Entries<ProcessedImage>().Where(e => e.State == EntityState.Added).Count();
                _logger.LogInformation("Batch save had constraint violations: {FailedCount} images were likely duplicates", failedEntries);
                
                // Clear the context and continue without these duplicates
                _context.ChangeTracker.Clear();
            }

            return Ok(new
            {
                success = true,
                message = $"Successfully added {addedImages.Count} generated images to database. {existingGenerated.Count} images already existed.",
                data = new
                {
                    addedCount = addedImages.Count,
                    existingCount = existingGenerated.Count,
                    totalFilesFound = imageFiles.Count,
                    skippedExisting = imageFiles.Count - addedImages.Count - errorFiles.Count,
                    errorFiles = errorFiles,
                    addedImages = addedImages.Take(5).ToList(), // Show first 5 for brevity
                    folderPath = generatedPath
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fixing generated images for user");
            return StatusCode(500, new { 
                success = false, 
                error = ex.Message,
                details = ex.StackTrace?.Split('\n').Take(3).ToArray() // First 3 lines of stack trace
            });
        }
    }

    [HttpGet("ping")]
    public IActionResult Ping()
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Ok(new { 
            success = true, 
            message = "Test controller is working",
            userId = userId,
            timestamp = DateTime.UtcNow
        });
    }

    [HttpGet("check-generated-images")]
    public async Task<IActionResult> CheckGeneratedImages()
    {
        try
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (userId == null)
                return Unauthorized();

            var userProfile = await _context.UserProfiles
                .Include(up => up.ProcessedImages)
                .FirstOrDefaultAsync(up => up.UserId == userId);

            if (userProfile == null)
                return NotFound("Profile not found");

            var allImages = userProfile.ProcessedImages.Select(img => new
            {
                img.Id,
                img.Style,
                img.IsGenerated,
                img.IsOriginalUpload,
                img.CreatedAt,
                img.OriginalImageUrl,
                img.ProcessedImageUrl
            }).ToList();

            var generatedImages = userProfile.ProcessedImages.Where(img => img.IsGenerated).ToList();

            return Ok(new
            {
                success = true,
                data = new
                {
                    totalImages = allImages.Count,
                    generatedImagesCount = generatedImages.Count,
                    originalUploadsCount = userProfile.ProcessedImages.Count(img => img.Style == "Original"),
                    allImages = allImages,
                    generatedImages = generatedImages.Select(img => new
                    {
                        img.Id,
                        img.Style,
                        img.CreatedAt,
                        img.OriginalImageUrl,
                        img.ProcessedImageUrl
                    })
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking generated images");
            return StatusCode(500, new { success = false, error = ex.Message });
        }
    }

    [HttpGet("replicate-connection")]
    public async Task<IActionResult> TestReplicateConnection()
    {
        try
        {
            // Test API connection by making a simple request
            // We'll test with a known model version to see if our credentials work
            var testResult = await _replicateApiClient.GetPredictionStatusAsync("dummy-id");
            
            // If we get here without an auth error, connection is working
            return Ok(new { 
                success = true, 
                message = "Replicate API connection successful", 
                data = new { connectionStatus = "Connected" },
                error = (object?)null 
            });
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogError(ex, "Replicate API authentication failed");
            return Ok(new { 
                success = false, 
                message = "Replicate API authentication failed", 
                data = (object?)null,
                error = new { code = "AuthError", message = ex.Message }
            });
        }
        catch (Exception ex) when (ex.Message.Contains("not found") || ex.Message.Contains("404"))
        {
            // Expected error for dummy ID - means auth worked but resource doesn't exist
            return Ok(new { 
                success = true, 
                message = "Replicate API connection successful (test prediction not found as expected)", 
                data = new { connectionStatus = "Connected", authStatus = "Valid" },
                error = (object?)null 
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Replicate API connection test failed");
            return Ok(new { 
                success = false, 
                message = "Replicate API connection failed", 
                data = (object?)null,
                error = new { code = "ConnectionError", message = ex.Message }
            });
        }
    }



    /// <summary>
    /// Test endpoint to check current user's basic tier status
    /// </summary>
    [HttpGet("basic-tier-status")]
    public async Task<IActionResult> GetBasicTierStatus()
    {
        try
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                return BadRequest(new { 
                    success = false, 
                    error = new { code = "InvalidUser", message = "User not authenticated" } 
                });
            }

            var profile = await _basicTierService.GetUserProfileWithCreditsAsync(userId);
            if (profile == null)
            {
                return Ok(new {
                    success = false,
                    message = "User profile not found",
                    data = (object?)null,
                    error = new { code = "ProfileNotFound", message = "User profile does not exist" }
                });
            }

            var availableCredits = await _basicTierService.GetAvailableCreditsAsync(userId);

            return Ok(new {
                success = true,
                message = "Basic tier status retrieved successfully",
                data = new {
                    userId = userId,
                    subscriptionTier = profile.SubscriptionTier.ToString(),
                    availableCredits = availableCredits,
                    totalCredits = profile.Credits,
                    lastCreditReset = profile.LastCreditReset,
                    nextResetDate = profile.LastCreditReset.AddDays(7),
                    daysUntilReset = Math.Max(0, 7 - (DateTime.UtcNow - profile.LastCreditReset).Days),
                    canGenerate = availableCredits > 0
                },
                error = (object?)null
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get basic tier status");
            return StatusCode(500, new {
                success = false,
                message = "Failed to get basic tier status",
                data = (object?)null,
                error = new { code = "StatusError", message = ex.Message }
            });
        }
    }

    /// <summary>
    /// Test endpoint to manually reset user's weekly credits (for testing purposes)
    /// </summary>
    [HttpPost("reset-credits")]
    public async Task<IActionResult> ResetCredits()
    {
        try
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                return BadRequest(new { 
                    success = false, 
                    error = new { code = "InvalidUser", message = "User not authenticated" } 
                });
            }

            await _basicTierService.ResetWeeklyCreditsAsync(userId);
            var newCredits = await _basicTierService.GetAvailableCreditsAsync(userId);

            return Ok(new {
                success = true,
                message = "Credits reset successfully",
                data = new {
                    userId = userId,
                    newCredits = newCredits,
                    resetAt = DateTime.UtcNow
                },
                error = (object?)null
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to reset credits");
            return StatusCode(500, new {
                success = false,
                message = "Failed to reset credits",
                data = (object?)null,
                error = new { code = "ResetError", message = ex.Message }
            });
        }
    }

    [HttpPost("cleanup-and-sync-images")]
    public async Task<IActionResult> CleanupAndSyncImages()
    {
        try
        {
            _logger.LogInformation("Starting database cleanup and filesystem sync");
            
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (userId == null)
            {
                return Unauthorized(new { success = false, error = "No user ID found" });
            }

            var userProfile = await _context.UserProfiles
                .Include(up => up.ProcessedImages)
                .FirstOrDefaultAsync(up => up.UserId == userId);

            if (userProfile == null)
            {
                return NotFound(new { success = false, error = "Profile not found" });
            }

            // Phase 1: Backup existing generated images (for safety)
            var existingGenerated = userProfile.ProcessedImages.Where(img => img.IsGenerated).ToList();
            var backupData = existingGenerated.Select(img => new
            {
                img.Id,
                img.Style,
                img.ProcessedImageUrl,
                img.CreatedAt,
                img.IsGenerated
            }).ToList();

            _logger.LogInformation("Backing up {Count} existing generated images", existingGenerated.Count);

            // Phase 2: Delete all generated images from database
            _context.ProcessedImages.RemoveRange(existingGenerated);
            await _context.SaveChangesAsync();
            
            _logger.LogInformation("Deleted {Count} generated images from database", existingGenerated.Count);

            // Phase 3: Scan filesystem and create new records
            var generatedPath = Path.Combine(Directory.GetCurrentDirectory(), "generated", userId);
            if (!Directory.Exists(generatedPath))
            {
                return Ok(new { 
                    success = true, 
                    message = "No generated images folder found - cleanup complete",
                    backupCount = existingGenerated.Count,
                    newCount = 0
                });
            }

            // Support multiple image formats
            var imageExtensions = new[] { "*.png", "*.jpg", "*.jpeg", "*.webp" };
            var imageFiles = new List<string>();
            
            foreach (var extension in imageExtensions)
            {
                imageFiles.AddRange(Directory.GetFiles(generatedPath, extension));
            }

            _logger.LogInformation("Found {FileCount} image files in generated folder", imageFiles.Count);

            var newImages = new List<object>();

            foreach (var filePath in imageFiles)
            {
                try
                {
                    var fileName = Path.GetFileName(filePath);
                    var fileNameWithoutExt = Path.GetFileNameWithoutExtension(fileName);
                    
                    // Extract style from filename (everything before first underscore)
                    var style = fileNameWithoutExt.Split('_')[0];
                    
                    // Get file creation time
                    var fileInfo = new FileInfo(filePath);
                    var createdAt = fileInfo.CreationTimeUtc;

                    var processedImage = new ProcessedImage
                    {
                        UserProfileId = userProfile.Id,
                        ProcessedImageUrl = $"/generated/{userId}/{fileName}",
                        OriginalImageUrl = $"/generated/{userId}/{fileName}", // Same for generated images
                        Style = style,
                        IsGenerated = true,
                        IsOriginalUpload = false,
                        CreatedAt = createdAt
                    };

                    // Set scheduled deletion date
                    processedImage.SetScheduledDeletionDate();

                    _context.ProcessedImages.Add(processedImage);
                    
                    newImages.Add(new
                    {
                        fileName = fileName,
                        style = style,
                        createdAt = createdAt,
                        path = processedImage.ProcessedImageUrl
                    });
                    
                    _logger.LogDebug("Added image to database: {FileName} -> {Style}", fileName, style);
                }
                catch (Exception fileEx)
                {
                    _logger.LogError(fileEx, "Error processing file: {FilePath}", filePath);
                }
            }

            await _context.SaveChangesAsync();
            _logger.LogInformation("Successfully synced {NewCount} images from filesystem", newImages.Count);

            return Ok(new
            {
                success = true,
                message = $"Database cleanup and sync completed. Removed {existingGenerated.Count} old records, added {newImages.Count} new records from filesystem.",
                data = new
                {
                    removedCount = existingGenerated.Count,
                    newCount = newImages.Count,
                    totalFilesFound = imageFiles.Count,
                    backupData = backupData.Take(5).ToList(), // Show first 5 for verification
                    newImages = newImages.Take(5).ToList(), // Show first 5 for verification
                    folderPath = generatedPath
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during database cleanup and sync");
            return StatusCode(500, new { 
                success = false, 
                error = ex.Message,
                details = ex.StackTrace?.Split('\n').Take(3).ToArray()
            });
        }
    }

    /// <summary>
    /// Check for duplicate ProcessedImageUrl entries in the database
    /// </summary>
    [HttpGet("check-duplicate-images")]
    public async Task<IActionResult> CheckDuplicateImages()
    {
        try
        {
            _logger.LogInformation("Checking for duplicate ProcessedImageUrl entries");
            
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (userId == null)
            {
                return Unauthorized(new { success = false, error = "No user ID found" });
            }

            // Get all generated images for this user
            var userProfile = await _context.UserProfiles
                .Include(up => up.ProcessedImages)
                .FirstOrDefaultAsync(up => up.UserId == userId);

            if (userProfile == null)
            {
                return NotFound(new { success = false, error = "User profile not found" });
            }

            var generatedImages = userProfile.ProcessedImages
                .Where(img => img.IsGenerated && !img.IsDeleted)
                .ToList();

            // Group by ProcessedImageUrl to find duplicates
            var duplicateGroups = generatedImages
                .GroupBy(img => img.ProcessedImageUrl)
                .Where(group => group.Count() > 1)
                .ToList();

            // Get duplicate statistics
            var duplicateUrls = duplicateGroups.Select(group => new {
                ProcessedImageUrl = group.Key,
                Count = group.Count(),
                Images = group.Select(img => new {
                    Id = img.Id,
                    Style = img.Style,
                    CreatedAt = img.CreatedAt,
                    OriginalImageUrl = img.OriginalImageUrl
                }).ToList()
            }).ToList();

            // Get overall statistics
            var totalGeneratedImages = generatedImages.Count;
            var uniqueUrls = generatedImages.Select(img => img.ProcessedImageUrl).Distinct().Count();
            var duplicateCount = duplicateGroups.Sum(group => group.Count() - 1); // Extra images (not counting one from each group)

            return Ok(new {
                success = true,
                data = new {
                    userId = userId,
                    totalGeneratedImages = totalGeneratedImages,
                    uniqueUrls = uniqueUrls,
                    duplicateCount = duplicateCount,
                    duplicateGroups = duplicateUrls.Count,
                    duplicates = duplicateUrls
                },
                error = (object?)null
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking for duplicate images");
            return StatusCode(500, new {
                success = false,
                error = ex.Message,
                details = ex.StackTrace?.Split('\n').Take(3).ToArray()
            });
        }
    }

    /// <summary>
    /// Get global duplicate statistics across all users (admin function)
    /// </summary>
    [HttpGet("check-all-duplicates")]
    public async Task<IActionResult> CheckAllDuplicates()
    {
        try
        {
            _logger.LogInformation("Checking for duplicate ProcessedImageUrl entries across all users");

            // Get all generated images across all users
            var allGeneratedImages = await _context.ProcessedImages
                .Where(img => img.IsGenerated && !img.IsDeleted)
                .Select(img => new {
                    img.Id,
                    img.ProcessedImageUrl,
                    img.Style,
                    img.CreatedAt,
                    img.UserProfileId
                })
                .ToListAsync();

            // Group by ProcessedImageUrl to find duplicates
            var duplicateGroups = allGeneratedImages
                .GroupBy(img => img.ProcessedImageUrl)
                .Where(group => group.Count() > 1)
                .ToList();

            // Get duplicate statistics
            var duplicateUrls = duplicateGroups.Select(group => new {
                ProcessedImageUrl = group.Key,
                Count = group.Count(),
                Images = group.Select(img => new {
                    Id = img.Id,
                    Style = img.Style,
                    CreatedAt = img.CreatedAt,
                    UserProfileId = img.UserProfileId
                }).ToList()
            }).ToList();

            // Get overall statistics
            var totalGeneratedImages = allGeneratedImages.Count;
            var uniqueUrls = allGeneratedImages.Select(img => img.ProcessedImageUrl).Distinct().Count();
            var duplicateCount = duplicateGroups.Sum(group => group.Count() - 1); // Extra images

            return Ok(new {
                success = true,
                data = new {
                    totalGeneratedImages = totalGeneratedImages,
                    uniqueUrls = uniqueUrls,
                    duplicateCount = duplicateCount,
                    duplicateGroups = duplicateUrls.Count,
                    duplicates = duplicateUrls
                },
                error = (object?)null
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking for all duplicate images");
            return StatusCode(500, new {
                success = false,
                error = ex.Message,
                details = ex.StackTrace?.Split('\n').Take(3).ToArray()
            });
        }
    }

    /// <summary>
    /// Simple duplicate analysis using Entity Framework
    /// </summary>
    [HttpGet("simple-duplicate-analysis")]
    public async Task<IActionResult> SimpleDuplicateAnalysis()
    {
        try
        {
            _logger.LogInformation("Running simple duplicate analysis for ProcessedImageUrl entries");

            // Get all generated images that aren't deleted
            var allGeneratedImages = await _context.ProcessedImages
                .Where(img => img.IsGenerated && !img.IsDeleted)
                .Select(img => new {
                    img.Id,
                    img.ProcessedImageUrl,
                    img.Style,
                    img.CreatedAt,
                    img.UserProfileId
                })
                .ToListAsync();

            // Group by ProcessedImageUrl to find duplicates
            var duplicateGroups = allGeneratedImages
                .GroupBy(img => img.ProcessedImageUrl)
                .Where(group => group.Count() > 1)
                .OrderByDescending(group => group.Count())
                .Take(20) // Limit to top 20 duplicate groups
                .Select(group => new {
                    ProcessedImageUrl = group.Key,
                    Count = group.Count(),
                    Images = group.OrderBy(img => img.CreatedAt).Select(img => new {
                        img.Id,
                        img.Style,
                        img.CreatedAt,
                        img.UserProfileId
                    }).ToList(),
                    FirstCreated = group.Min(img => img.CreatedAt),
                    LastCreated = group.Max(img => img.CreatedAt)
                })
                .ToList();

            // Calculate statistics
            var totalGeneratedImages = allGeneratedImages.Count;
            var uniqueUrls = allGeneratedImages.Select(img => img.ProcessedImageUrl).Distinct().Count();
            var totalDuplicates = allGeneratedImages.Count - uniqueUrls;

            return Ok(new {
                success = true,
                data = new {
                    summary = new {
                        totalGeneratedImages = totalGeneratedImages,
                        uniqueUrls = uniqueUrls,
                        duplicateCount = totalDuplicates,
                        duplicateGroupsFound = duplicateGroups.Count,
                        duplicateGroupsShown = Math.Min(duplicateGroups.Count, 20)
                    },
                    duplicateGroups = duplicateGroups
                },
                error = (object?)null
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error running simple duplicate analysis");
            return StatusCode(500, new {
                success = false,
                error = ex.Message,
                details = ex.StackTrace?.Split('\n').Take(3).ToArray()
            });
        }
    }

    /// <summary>
    /// Fix uploaded selfies by syncing files from uploads directory to database
    /// </summary>
    [HttpPost("fix-uploaded-selfies")]
    public async Task<IActionResult> FixUploadedSelfies()
    {
        try
        {
            _logger.LogInformation("Starting fix-uploaded-selfies for user");
            
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (userId == null)
            {
                _logger.LogWarning("No user ID found in claims");
                return Unauthorized(new { success = false, error = "No user ID found" });
            }

            _logger.LogInformation("Processing fix-uploaded-selfies for user: {UserId}", userId);

            var userProfile = await _context.UserProfiles
                .Include(up => up.ProcessedImages)
                .FirstOrDefaultAsync(up => up.UserId == userId);

            if (userProfile == null)
            {
                _logger.LogWarning("User profile not found for user: {UserId}", userId);
                return NotFound(new { success = false, error = "Profile not found" });
            }

            _logger.LogInformation("Found user profile: {ProfileId}", userProfile.Id);

            // Get existing uploaded images for duplicate checking
            var existingUploaded = userProfile.ProcessedImages.Where(img => img.IsOriginalUpload && !img.IsDeleted).ToList();
            var existingUploadedPaths = existingUploaded.Select(img => img.OriginalImageUrl).ToHashSet();
            _logger.LogInformation("User has {ExistingCount} existing uploaded images in database", existingUploaded.Count);

            // Look for uploaded images in the file system
            var uploadsPath = Path.Combine(Directory.GetCurrentDirectory(), "uploads", userId);
            _logger.LogInformation("Checking uploads folder: {UploadsPath}", uploadsPath);
            
            if (!Directory.Exists(uploadsPath))
            {
                _logger.LogWarning("Uploads folder not found: {UploadsPath}", uploadsPath);
                return NotFound(new { success = false, error = "No uploads folder found", path = uploadsPath });
            }

            // Support multiple image formats
            var imageExtensions = new[] { "*.png", "*.jpg", "*.jpeg", "*.webp" };
            var imageFiles = new List<string>();
            
            foreach (var extension in imageExtensions)
            {
                imageFiles.AddRange(Directory.GetFiles(uploadsPath, extension));
            }
            
            _logger.LogInformation("Found {FileCount} image files in uploads folder", imageFiles.Count);

            if (imageFiles.Count == 0)
            {
                return Ok(new { 
                    success = true, 
                    message = "No image files found in uploads folder",
                    data = new { addedCount = 0, folderPath = uploadsPath }
                });
            }

            var addedImages = new List<object>();
            var errorFiles = new List<string>();

            foreach (var filePath in imageFiles)
            {
                try
                {
                    var fileName = Path.GetFileName(filePath);
                    var relativePath = $"/uploads/{userId}/{fileName}";
                    
                    // Skip if this image already exists in database
                    if (existingUploadedPaths.Contains(relativePath))
                    {
                        _logger.LogDebug("Skipping existing uploaded image: {FileName}", fileName);
                        continue;
                    }
                    
                    // Get file creation time for proper retention dating
                    var fileInfo = new FileInfo(filePath);
                    var fileCreatedAt = fileInfo.CreationTimeUtc;

                    // Create new ProcessedImage record for uploaded selfie
                    var processedImage = new ProcessedImage
                    {
                        UserProfileId = userProfile.Id,
                        OriginalImageUrl = relativePath,
                        ProcessedImageUrl = relativePath, // Same as original for uploaded images
                        Style = "Original", // Mark as original upload
                        IsGenerated = false,
                        IsOriginalUpload = true, // This is the key flag
                        CreatedAt = fileCreatedAt // Use file creation time
                    };

                    // Set scheduled deletion date (7 days for uploads)
                    processedImage.SetScheduledDeletionDate();

                    try
                    {
                        _context.ProcessedImages.Add(processedImage);
                    }
                    catch (Exception addEx)
                    {
                        _logger.LogWarning(addEx, "Failed to add ProcessedImage for uploaded file {FileName}", fileName);
                        continue;
                    }
                    
                    addedImages.Add(new
                    {
                        fileName = fileName,
                        style = "Original",
                        path = relativePath,
                        createdAt = fileCreatedAt
                    });
                    
                    _logger.LogDebug("Added uploaded image to database: {FileName}", fileName);
                }
                catch (Exception fileEx)
                {
                    errorFiles.Add(Path.GetFileName(filePath));
                    _logger.LogError(fileEx, "Error processing uploaded file: {FilePath}", filePath);
                }
            }

            _logger.LogInformation("Attempting to save {AddedCount} uploaded images to database", addedImages.Count);
            
            try
            {
                await _context.SaveChangesAsync();
                _logger.LogInformation("Successfully saved {AddedCount} uploaded images to database", addedImages.Count);
            }
            catch (Microsoft.EntityFrameworkCore.DbUpdateException ex) when (ex.InnerException?.Message?.Contains("UNIQUE constraint failed") == true)
            {
                // Handle unique constraint violations gracefully
                _logger.LogWarning(ex, "Some uploaded images were already in database due to unique constraint");
                
                var failedEntries = _context.ChangeTracker.Entries<ProcessedImage>().Where(e => e.State == EntityState.Added).Count();
                _logger.LogInformation("Batch save had constraint violations: {FailedCount} uploaded images were likely duplicates", failedEntries);
                
                _context.ChangeTracker.Clear();
            }

            return Ok(new
            {
                success = true,
                message = $"Successfully added {addedImages.Count} uploaded selfies to database. {existingUploaded.Count} images already existed.",
                data = new
                {
                    addedCount = addedImages.Count,
                    existingCount = existingUploaded.Count,
                    totalFilesFound = imageFiles.Count,
                    errorFiles = errorFiles,
                    addedImages = addedImages.Take(5).ToList(), // Show first 5 for brevity
                    folderPath = uploadsPath
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fixing uploaded selfies for user");
            return StatusCode(500, new { 
                success = false, 
                error = ex.Message,
                details = ex.StackTrace?.Split('\n').Take(3).ToArray()
            });
        }
    }


    /// <summary>
    /// Helper method to check if a string is a valid hexadecimal string (for GUID detection)
    /// </summary>
    private static bool IsHexString(string input)
    {
        if (string.IsNullOrEmpty(input))
            return false;
            
        return input.All(c => char.IsDigit(c) || (char.ToLowerInvariant(c) >= 'a' && char.ToLowerInvariant(c) <= 'f'));
    }

}

