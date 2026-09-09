using System.Globalization;
using AI.ProfilePhotoMaker.API.Configuration;
using AI.ProfilePhotoMaker.API.Data;
using AI.ProfilePhotoMaker.API.Infrastructure.Logging;
using AI.ProfilePhotoMaker.API.Models;
using AI.ProfilePhotoMaker.API.Services;
using AI.ProfilePhotoMaker.API.Services.Payments.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Stripe;

namespace AI.ProfilePhotoMaker.API.Services.Payments;

public class StripePaymentService : IStripePaymentService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly ILogger<StripePaymentService> _logger;
    private readonly StripeOptions _options;
    private readonly StripeClient _stripeClient;
    private readonly IWebHostEnvironment _environment;
    private readonly ICouponService _couponService;
    private readonly IOutcomePackageService? _outcomePackageService;
    private static string S(string? value) => LoggingSanitizer.Sanitize(value);
    private static string Sid(string? value) => LoggingSanitizer.SanitizeId(value);

    public StripePaymentService(
        ApplicationDbContext dbContext,
        ILogger<StripePaymentService> logger,
        IOptions<StripeOptions> options,
        StripeClient stripeClient,
        IWebHostEnvironment environment,
        ICouponService couponService,
        IOutcomePackageService? outcomePackageService = null)
    {
        _dbContext = dbContext;
        _logger = logger;
        _options = options.Value;
        _stripeClient = stripeClient;
        _environment = environment;
        _couponService = couponService;
        _outcomePackageService = outcomePackageService;
    }

    public async Task<PaymentIntentResponse> CreatePaymentIntentAsync(string userId, int packageId, string? couponCode = null, int? previewProcessedImageId = null, CancellationToken cancellationToken = default)
    {
        if (_environment.IsDevelopment() && !_options.AllowLiveKeysInDevelopment && _options.UsesLiveMode())
        {
            _logger.LogWarning("Stripe live keys detected in development. Refusing to create payment intent.");
            throw new InvalidOperationException(
                "Stripe live keys are not allowed in Development. Use test keys or set Stripe:AllowLiveKeysInDevelopment=true.");
        }

        if (!_options.HasApiKeys())
        {
            _logger.LogError("Stripe configuration is incomplete. Cannot create payment intent.");
            throw new InvalidOperationException("Stripe API keys are missing. Check Stripe keys in environment configuration.");
        }

        if (!_options.HasWebhookSecret())
        {
            _logger.LogWarning("Stripe webhook secret is not configured. Payment intents can be created, but webhook events will not be processed.");
        }

        var user = await _dbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);

        if (user is null)
        {
            _logger.LogWarning("Cannot create payment intent for non-existent user {UserId}", Sid(userId));
            throw new InvalidOperationException("User not found");
        }

        var package = await _dbContext.CreditPackages
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == packageId && p.IsActive, cancellationToken);

        if (package is null)
        {
            _logger.LogWarning("No active credit package found for packageId {PackageId}", packageId);
            throw new InvalidOperationException("Credit package not found or inactive");
        }

        if (previewProcessedImageId is int previewId &&
            (_outcomePackageService == null ||
             !await _outcomePackageService.ReservePreviewForPurchaseAsync(userId, previewId, cancellationToken)))
        {
            throw new InvalidOperationException("This preview expired before checkout could begin. Start over with a new photo.");
        }

        var originalPrice = package.Price;
        var discountAmount = 0m;
        string? appliedCouponCode = null;

        if (!string.IsNullOrWhiteSpace(couponCode))
        {
            var validation = await _couponService.ValidateCouponAsync(couponCode, userId, originalPrice);
            if (!validation.IsValid)
            {
                throw new InvalidOperationException(validation.Message);
            }

            discountAmount = validation.DiscountAmount;
            appliedCouponCode = couponCode.Trim().ToUpperInvariant();
        }

        var finalPrice = Math.Max(0m, originalPrice - discountAmount);
        var amountInCents = ConvertAmountToStripeLong(finalPrice);

        var paymentIntentService = new PaymentIntentService(_stripeClient);
        var metadata = new Dictionary<string, string>
        {
            ["user_id"] = userId,
            ["package_id"] = package.Id.ToString(),
            ["package_name"] = package.Name,
            ["package_total_credits"] = package.TotalCredits.ToString(),
            ["original_price"] = originalPrice.ToString("0.00", CultureInfo.InvariantCulture),
            ["discount_amount"] = discountAmount.ToString("0.00", CultureInfo.InvariantCulture)
        };
        if (!string.IsNullOrWhiteSpace(appliedCouponCode))
        {
            metadata["coupon_code"] = appliedCouponCode;
        }
        if (previewProcessedImageId is int reservedPreviewId)
        {
            metadata["preview_processed_image_id"] = reservedPreviewId.ToString();
        }

        var createOptions = new PaymentIntentCreateOptions
        {
            Amount = amountInCents,
            Currency = "usd",
            AutomaticPaymentMethods = new PaymentIntentAutomaticPaymentMethodsOptions
            {
                Enabled = true
            },
            ReceiptEmail = string.IsNullOrWhiteSpace(user.Email) ? null : user.Email,
            Metadata = metadata,
            Description = $"Credit package purchase: {package.Name} ({package.TotalCredits} credits)"
        };

        var createRequestOptions = new RequestOptions
        {
            IdempotencyKey = $"pi_create_{userId}_{packageId}_{Guid.NewGuid():N}"
        };

        PaymentIntent paymentIntent;
        try
        {
            paymentIntent = await paymentIntentService.CreateAsync(createOptions, createRequestOptions, cancellationToken);
        }
        catch (StripeException stripeException)
        {
            _logger.LogError(stripeException, "Stripe API error while creating payment intent for user {UserId}", Sid(userId));
            throw;
        }

        if (string.IsNullOrWhiteSpace(paymentIntent.ClientSecret))
        {
            _logger.LogError("Stripe did not return a client secret for payment intent {PaymentIntentId}", Sid(paymentIntent.Id));
            throw new InvalidOperationException("Stripe did not return a client secret for the payment intent");
        }

        var transaction = new PaymentTransaction
        {
            UserId = userId,
            ExternalTransactionId = paymentIntent.Id,
            Amount = finalPrice,
            Currency = paymentIntent.Currency.ToUpperInvariant(),
            PaymentProvider = "stripe",
            Status = PaymentStatus.Pending,
            Type = PaymentType.OneTime,
            Description = $"Credit package purchase: {package.Name}",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _dbContext.PaymentTransactions.Add(transaction);
        await _dbContext.SaveChangesAsync(cancellationToken);

        metadata["payment_transaction_id"] = transaction.Id.ToString();

        // Update metadata with transaction id for easier reconciliation (best-effort)
        try
        {
            var updateOptions = new PaymentIntentUpdateOptions
            {
                Metadata = metadata
            };

            var updateRequestOptions = new RequestOptions
            {
                IdempotencyKey = $"pi_update_{paymentIntent.Id}_{transaction.Id}"
            };

            await paymentIntentService.UpdateAsync(paymentIntent.Id, updateOptions, updateRequestOptions, cancellationToken);
        }
        catch (StripeException updateException)
        {
            // Log but do not fail the flow – transaction is recorded locally
            _logger.LogWarning(updateException, "Failed to update Stripe metadata for payment intent {PaymentIntentId}", Sid(paymentIntent.Id));
        }

        return new PaymentIntentResponse(paymentIntent.Id, paymentIntent.ClientSecret, _options.PublishableKey, transaction.Id.ToString());
    }

    private static long ConvertAmountToStripeLong(decimal amount)
    {
        return (long)Math.Round(amount * 100, MidpointRounding.AwayFromZero);
    }
}
