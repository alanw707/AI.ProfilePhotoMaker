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
                        var style = string.Join("_", parts.Take(parts.Length - 1)); // Everything except the last part (number)

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

                        _context.ProcessedImages.Add(processedImage);
                        
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
            await _context.SaveChangesAsync();
            _logger.LogInformation("Successfully saved {AddedCount} images to database", addedImages.Count);

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

}

