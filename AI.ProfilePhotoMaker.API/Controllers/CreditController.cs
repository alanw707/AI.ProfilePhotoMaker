using AI.ProfilePhotoMaker.API.Models.DTOs;
using AI.ProfilePhotoMaker.API.Models;
using AI.ProfilePhotoMaker.API.Services;
using AI.ProfilePhotoMaker.API.Services.Payments;
using AI.ProfilePhotoMaker.API.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Stripe;

namespace AI.ProfilePhotoMaker.API.Controllers;

[Authorize]
public class CreditController : BaseController
{
    private readonly ICreditPackageService _creditPackageService;
    private readonly IBasicTierService _basicTierService;
    private readonly IStripePaymentService _stripePaymentService;
    private readonly StripeOptions _stripeOptions;
    private readonly PaymentSimulationOptions _paymentSimulationOptions;

    public CreditController(
        ICreditPackageService creditPackageService,
        IBasicTierService basicTierService,
        IStripePaymentService stripePaymentService,
        IOptions<StripeOptions> stripeOptions,
        IOptions<PaymentSimulationOptions> paymentSimulationOptions,
        ILogger<CreditController> logger)
        : base(logger)
    {
        _creditPackageService = creditPackageService;
        _basicTierService = basicTierService;
        _stripePaymentService = stripePaymentService;
        _stripeOptions = stripeOptions.Value;
        _paymentSimulationOptions = paymentSimulationOptions.Value;
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
    [AllowAnonymous] // Allow unauthenticated access so users can see packages before login
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

        var purchaseResult = await _creditPackageService.PurchaseCreditPackageAsync(userId, dto.PackageId, dto.PaymentTransactionId);

        if (!purchaseResult.Success)
        {
            if (purchaseResult.Status == PaymentStatus.Pending)
            {
                return StatusCode(202, new
                {
                    success = false,
                    error = new
                    {
                        code = purchaseResult.ErrorCode ?? "PaymentPending",
                        message = purchaseResult.ErrorMessage ?? "Payment is still processing. Please try again shortly."
                    }
                });
            }

            var statusCode = purchaseResult.Status == PaymentStatus.Failed ? 402 : 400;
            return ErrorResponse(purchaseResult.ErrorCode ?? "PurchaseFailed", purchaseResult.ErrorMessage ?? "Credit package not found or purchase failed.", statusCode);
        }

        var purchase = purchaseResult.Purchase!;

        // Get updated credit status
        var (weeklyCredits, purchasedCredits) = await _basicTierService.GetCreditBreakdownAsync(userId);

        return SuccessResponse(new
        {
            purchase = new
            {
                purchase.Id,
                purchase.CreditsAwarded,
                purchase.AmountPaid,
                purchase.PurchaseDate
            },
            updatedCredits = new
            {
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

        var historyData = history.Select(p => new
        {
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
    public async Task<IActionResult> CreatePaymentIntent([FromBody] CreatePaymentIntentRequestDto dto, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return ErrorResponse("InvalidModel", "Invalid input.");

        var authCheck = ValidateAuthentication();
        if (authCheck != null) return authCheck;
        var userId = GetCurrentUserId()!;

        try
        {
            var intent = await _stripePaymentService.CreatePaymentIntentAsync(userId, dto.PackageId, cancellationToken);

            return SuccessResponse(new
            {
                paymentIntentId = intent.PaymentIntentId,
                clientSecret = intent.ClientSecret,
                publishableKey = intent.PublishableKey,
                paymentTransactionId = intent.PaymentTransactionId,
                packageId = dto.PackageId,
                isSimulation = false
            });
        }
        catch (StripeException stripeEx)
        {
            LogError(stripeEx, "Stripe error while creating payment intent");
            return ErrorResponse("StripeError", stripeEx.Message, 502);
        }
        catch (InvalidOperationException invalidOperationException)
        {
            Logger.LogWarning(
                "Payment intent validation failed: {Message} (PackageId: {PackageId})",
                S(invalidOperationException.Message),
                Sid(dto.PackageId.ToString()));
            return ErrorResponse("PaymentSetupError", invalidOperationException.Message, 400);
        }
        catch (Exception ex)
        {
            LogError(ex, "Unexpected error while creating payment intent");
            return ErrorResponse("InternalError", "Failed to create payment intent");
        }
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
        var stripeHasApiKeys = _stripeOptions.HasApiKeys();
        var webhookConfigured = _stripeOptions.HasWebhookSecret();
        var stripeReady = stripeHasApiKeys && webhookConfigured;
        var simulationForced = _paymentSimulationOptions.Enabled && _paymentSimulationOptions.SkipStripeIntegration;
        var simulationRequired = !stripeReady;
        var simulationActive = simulationRequired || simulationForced;

        string? simulationReason = null;
        if (simulationRequired)
        {
            simulationReason = !stripeHasApiKeys ? "StripeNotConfigured" : "WebhookNotConfigured";
        }
        else if (simulationForced)
        {
            simulationReason = "ExplicitBypass";
        }

        var config = new
        {
            PaymentSimulation = new
            {
                Enabled = simulationActive,
                SkipStripeIntegration = simulationActive,
                Reason = simulationReason
            },
            Stripe = new
            {
                Enabled = stripeReady && !simulationForced,
                PublishableKey = stripeReady ? _stripeOptions.PublishableKey : null,
                WebhookConfigured = webhookConfigured
            }
        };

        return SuccessResponse(config);
    }
}
