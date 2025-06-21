using AI.ProfilePhotoMaker.API.Models.DTOs;
using AI.ProfilePhotoMaker.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace AI.ProfilePhotoMaker.API.Controllers;

[Route("api/premium-package")]
[ApiController]
public class PremiumPackageController : ControllerBase
{
    private readonly IPremiumPackageService _premiumPackageService;
    private readonly ILogger<PremiumPackageController> _logger;

    public PremiumPackageController(IPremiumPackageService premiumPackageService, ILogger<PremiumPackageController> logger)
    {
        _premiumPackageService = premiumPackageService;
        _logger = logger;
    }

    /// <summary>
    /// Get all active premium packages available for purchase
    /// </summary>
    [HttpGet("packages")]
    public async Task<IActionResult> GetActivePackages()
    {
        try
        {
            var packages = await _premiumPackageService.GetActivePackagesAsync();
            return Ok(new { success = true, data = packages, error = (object?)null });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get active packages: {Message}", ex.Message);
            return StatusCode(500, new { 
                success = false, 
                data = (object?)null, 
                error = new { code = "GetPackagesError", message = $"Failed to retrieve premium packages: {ex.Message}" } 
            });
        }
    }

    /// <summary>
    /// Get current user's package status and credits
    /// </summary>
    [HttpGet("status")]
    [Authorize]
    public async Task<IActionResult> GetUserPackageStatus()
    {
        try
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized(new { success = false, error = new { code = "Unauthorized", message = "User not authenticated." } });

            var status = await _premiumPackageService.GetUserPackageStatusAsync(userId);
            return Ok(new { success = true, data = status, error = (object?)null });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get user package status");
            return StatusCode(500, new { 
                success = false, 
                data = (object?)null, 
                error = new { code = "StatusError", message = "Failed to retrieve package status." } 
            });
        }
    }

    /// <summary>
    /// Purchase a premium package
    /// </summary>
    [HttpPost("purchase")]
    [Authorize]
    public async Task<IActionResult> PurchasePackage([FromBody] PurchasePackageRequestDto request)
    {
        if (!ModelState.IsValid)
            return BadRequest(new { success = false, error = new { code = "InvalidModel", message = "Invalid request data." } });

        try
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized(new { success = false, error = new { code = "Unauthorized", message = "User not authenticated." } });

            var purchase = await _premiumPackageService.PurchasePackageAsync(userId, request.PackageId, request.PaymentTransactionId);
            
            if (purchase == null)
            {
                return BadRequest(new { 
                    success = false, 
                    error = new { 
                        code = "PurchaseFailed", 
                        message = "Unable to purchase package. Package may not exist or you may already have an active package." 
                    } 
                });
            }

            var responseData = new
            {
                purchaseId = purchase.Id,
                packageName = purchase.Package.Name,
                credits = purchase.CreditsRemaining,
                expirationDate = purchase.ExpirationDate,
                amountPaid = purchase.AmountPaid
            };

            return Ok(new { success = true, data = responseData, error = (object?)null });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to purchase package {PackageId} for user", request.PackageId);
            return StatusCode(500, new { 
                success = false, 
                data = (object?)null, 
                error = new { code = "PurchaseError", message = "Failed to complete package purchase." } 
            });
        }
    }

    /// <summary>
    /// Check if user can select a specific number of styles
    /// </summary>
    [HttpGet("can-select-styles/{styleCount}")]
    [Authorize]
    public async Task<IActionResult> CanSelectStyles(int styleCount)
    {
        try
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized(new { success = false, error = new { code = "Unauthorized", message = "User not authenticated." } });

            var canSelect = await _premiumPackageService.CanSelectStylesAsync(userId, styleCount);
            return Ok(new { 
                success = true, 
                data = new { canSelect = canSelect, maxStyles = styleCount }, 
                error = (object?)null 
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check style selection capability for user");
            return StatusCode(500, new { 
                success = false, 
                data = (object?)null, 
                error = new { code = "ValidationError", message = "Failed to validate style selection." } 
            });
        }
    }

    /// <summary>
    /// Check if user can generate a specific number of images
    /// </summary>
    [HttpGet("can-generate-images/{imageCount}")]
    [Authorize]
    public async Task<IActionResult> CanGenerateImages(int imageCount)
    {
        try
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized(new { success = false, error = new { code = "Unauthorized", message = "User not authenticated." } });

            var canGenerate = await _premiumPackageService.CanGenerateImagesAsync(userId, imageCount);
            return Ok(new { 
                success = true, 
                data = new { canGenerate = canGenerate, imageCount = imageCount }, 
                error = (object?)null 
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check image generation capability for user");
            return StatusCode(500, new { 
                success = false, 
                data = (object?)null, 
                error = new { code = "ValidationError", message = "Failed to validate image generation." } 
            });
        }
    }
}