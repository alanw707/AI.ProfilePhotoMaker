using AI.ProfilePhotoMaker.API.Models.DTOs;
using AI.ProfilePhotoMaker.API.Models;
using AI.ProfilePhotoMaker.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AI.ProfilePhotoMaker.API.Controllers;

[Authorize]
public class CreditController : BaseController
{
    private readonly ICreditPackageService _creditPackageService;
    private readonly IBasicTierService _basicTierService;
    private readonly IConfiguration _configuration;

    public CreditController(ICreditPackageService creditPackageService, IBasicTierService basicTierService, IConfiguration configuration, ILogger<CreditController> logger) 
        : base(logger)
    {
        _creditPackageService = creditPackageService;
        _basicTierService = basicTierService;
        _configuration = configuration;
    }

    /// <summary>
    /// Gets current user's credit status including weekly and purchased credits
    /// </summary>
    [HttpGet("status")]
    public async Task<IActionResult> GetCreditStatus()
    {
        var authCheck = ValidateAuthentication();
        if (authCheck != null) return authCheck;
        var userId = GetCurrentUserId()!;

        var (weeklyCredits, purchasedCredits) = await _basicTierService.GetCreditBreakdownAsync(userId);
        var profile = await _basicTierService.GetUserProfileWithCreditsAsync(userId);
        
        if (profile == null)
            return ErrorResponse("ProfileNotFound", "User profile not found.", 404);

        var status = new UserCreditStatusDto
        {
            TotalCredits = weeklyCredits + purchasedCredits,
            WeeklyCredits = weeklyCredits,
            PurchasedCredits = purchasedCredits,
            LastCreditReset = profile.LastCreditReset,
            NextResetDate = profile.LastCreditReset.AddDays(7)
        };

        return SuccessResponse(status);
    }

    /// <summary>
    /// Gets all available credit packages for purchase
    /// </summary>
    [HttpGet("packages")]
    public async Task<IActionResult> GetCreditPackages()
    {
        try
        {
            var packages = await _creditPackageService.GetActiveCreditPackagesAsync();
            return SuccessResponse(packages);
        }
        catch (Exception ex)
        {
            LogError(ex, "Failed to get credit packages");
            return ErrorResponse("InternalError", ex.Message, 500);
        }
    }


    /// <summary>
    /// Purchases a credit package
    /// </summary>
    [HttpPost("purchase")]
    public async Task<IActionResult> PurchaseCreditPackage([FromBody] PurchaseCreditPackageRequestDto dto)
    {
        if (!ModelState.IsValid)
            return ErrorResponse("InvalidModel", "Invalid input.");

        var authCheck = ValidateAuthentication();
        if (authCheck != null) return authCheck;
        var userId = GetCurrentUserId()!;

        var purchase = await _creditPackageService.PurchaseCreditPackageAsync(userId, dto.PackageId, dto.PaymentTransactionId);
        
        if (purchase == null)
        {
            return ErrorResponse("PurchaseFailed", "Credit package not found or purchase failed.");
        }

        // Get updated credit status
        var (weeklyCredits, purchasedCredits) = await _basicTierService.GetCreditBreakdownAsync(userId);

        return SuccessResponse(new {
            purchase = new {
                purchase.Id,
                purchase.CreditsAwarded,
                purchase.AmountPaid,
                purchase.PurchaseDate
            },
            updatedCredits = new {
                totalCredits = weeklyCredits + purchasedCredits,
                weeklyCredits = weeklyCredits,
                purchasedCredits = purchasedCredits
            }
        });
    }

    /// <summary>
    /// Gets the user's credit purchase history
    /// </summary>
    [HttpGet("history")]
    public async Task<IActionResult> GetPurchaseHistory()
    {
        var authCheck = ValidateAuthentication();
        if (authCheck != null) return authCheck;
        var userId = GetCurrentUserId()!;

        var history = await _creditPackageService.GetUserPurchaseHistoryAsync(userId);
        
        var historyData = history.Select(p => new {
            p.Id,
            p.PurchaseDate,
            p.CreditsAwarded,
            p.AmountPaid,
            p.Status,
            PackageName = p.Package.Name
        });

        return SuccessResponse(historyData);
    }

    /// <summary>
    /// Creates a payment intent for Stripe (placeholder for development)
    /// </summary>
    [HttpPost("create-payment-intent")]
    public async Task<IActionResult> CreatePaymentIntent([FromBody] CreatePaymentIntentRequestDto dto)
    {
        if (!ModelState.IsValid)
            return ErrorResponse("InvalidModel", "Invalid input.");

        var authCheck = ValidateAuthentication();
        if (authCheck != null) return authCheck;
        var userId = GetCurrentUserId()!;

        // Get the package to validate it exists
        var package = await _creditPackageService.GetActiveCreditPackagesAsync();
        var selectedPackage = package.FirstOrDefault(p => p.Id == dto.PackageId);
        
        if (selectedPackage == null)
        {
            return ErrorResponse("PackageNotFound", "Credit package not found or inactive.");
        }

        // Return mock payment intent for development
        var mockClientSecret = $"pi_mock_{Guid.NewGuid():N}_secret_{Guid.NewGuid():N}";
        
        return SuccessResponse(new {
            clientSecret = mockClientSecret,
            packageId = dto.PackageId,
            amount = selectedPackage.Price,
            packageName = selectedPackage.Name,
            isSimulation = true
        });
    }

    /// <summary>
    /// Gets credit cost configuration for different operations
    /// </summary>
    [HttpGet("costs")]
    [AllowAnonymous] // Allow unauthenticated access so users can see pricing before login
    public IActionResult GetCreditCosts()
    {
        var costs = new
        {
            PhotoEnhancement = new
            {
                Cost = CreditCostConfig.PhotoEnhancement,
                CanUseWeeklyCredits = CreditCostConfig.CanUseWeeklyCredits("photo_enhancement"),
                Description = "Enhance photos using AI professional editing"
            },
            ModelTraining = new
            {
                Cost = CreditCostConfig.ModelTraining,
                CanUseWeeklyCredits = CreditCostConfig.CanUseWeeklyCredits("model_training"),
                Description = "Train custom AI model with your photos"
            },
            StyledGeneration = new
            {
                Cost = CreditCostConfig.StyledGeneration,
                CanUseWeeklyCredits = CreditCostConfig.CanUseWeeklyCredits("styled_generation"),
                Description = "Generate styled photos using trained model"
            }
        };

        return SuccessResponse(costs);
    }

    /// <summary>
    /// Gets payment configuration including simulation settings
    /// </summary>
    [HttpGet("payment-config")]
    [AllowAnonymous]
    public IActionResult GetPaymentConfig()
    {
        var config = new
        {
            PaymentSimulation = new
            {
                Enabled = _configuration.GetValue<bool>("PaymentSimulation:Enabled", false),
                SkipStripeIntegration = _configuration.GetValue<bool>("PaymentSimulation:SkipStripeIntegration", false)
            }
        };

        return SuccessResponse(config);
    }
}