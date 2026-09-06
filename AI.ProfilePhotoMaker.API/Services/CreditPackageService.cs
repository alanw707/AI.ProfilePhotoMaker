using AI.ProfilePhotoMaker.API.Configuration;
using AI.ProfilePhotoMaker.API.Data;
using AI.ProfilePhotoMaker.API.Infrastructure.Logging;
using AI.ProfilePhotoMaker.API.Models;
using AI.ProfilePhotoMaker.API.Models.DTOs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Stripe;

namespace AI.ProfilePhotoMaker.API.Services;

public class CreditPackageService : ICreditPackageService
{
    private readonly ApplicationDbContext _context;
    private readonly IBasicTierService _basicTierService;
    private readonly ILogger<CreditPackageService> _logger;
    private readonly IOutcomePackageService? _outcomePackageService;
    private readonly StripeOptions _stripeOptions;
    private readonly PaymentSimulationOptions _simulationOptions;
    private readonly StripeClient? _stripeClient;

    public CreditPackageService(
        ApplicationDbContext context,
        IBasicTierService basicTierService,
        ILogger<CreditPackageService> logger,
        IOptions<StripeOptions> stripeOptions,
        IOptions<PaymentSimulationOptions> paymentSimulationOptions,
        StripeClient? stripeClient = null,
        IOutcomePackageService? outcomePackageService = null)
    {
        _context = context;
        _basicTierService = basicTierService;
        _logger = logger;
        _outcomePackageService = outcomePackageService;
        _stripeOptions = stripeOptions.Value;
        _simulationOptions = paymentSimulationOptions.Value;
        _stripeClient = stripeClient;
    }

    public async Task<IEnumerable<CreditPackageDto>> GetActiveCreditPackagesAsync()
    {
        var packages = await _context.CreditPackages
            .Where(p => p.IsActive)
            .OrderBy(p => p.DisplayOrder)
            .Select(p => new CreditPackageDto
            {
                Id = p.Id,
                Name = p.Name,
                Credits = p.Credits,
                BonusCredits = p.BonusCredits,
                TotalCredits = p.Credits + p.BonusCredits,
                Price = p.Price,
                Description = p.Description,
                DisplayOrder = p.DisplayOrder
            })
            .ToListAsync();

        return packages;
    }

    public async Task<CreditPurchaseResult> PurchaseCreditPackageAsync(string userId, int packageId, string? paymentTransactionId = null)
    {
        var package = await _context.CreditPackages
            .FirstOrDefaultAsync(p => p.Id == packageId && p.IsActive);

        if (package == null)
        {
            _logger.LogWarning("Credit package {PackageId} not found or inactive", packageId);
            return new CreditPurchaseResult(false, PaymentStatus.Failed, null, "PackageNotFound", "Credit package not found or inactive.");
        }

        CreditPurchase? existingPurchase = null;
        if (!string.IsNullOrWhiteSpace(paymentTransactionId))
        {
            existingPurchase = await _context.CreditPurchases
                .Include(p => p.Package)
                .FirstOrDefaultAsync(p =>
                    p.PaymentTransactionId == paymentTransactionId ||
                    (!string.IsNullOrWhiteSpace(p.ExternalTransactionId) && p.ExternalTransactionId == paymentTransactionId));
        }

        if (existingPurchase != null)
        {
            if (!string.Equals(existingPurchase.UserId, userId, StringComparison.Ordinal) || existingPurchase.PackageId != packageId)
            {
                return new CreditPurchaseResult(false, PaymentStatus.Failed, null, "TransactionMismatch", "Payment transaction does not match the current user and package.");
            }

            var success = existingPurchase.Status is PaymentStatus.Completed or PaymentStatus.Succeeded;
            return new CreditPurchaseResult(success, existingPurchase.Status, existingPurchase);
        }

        var stripeHasApiKeys = _stripeOptions.HasApiKeys();
        var simulationForced = _simulationOptions.Enabled && _simulationOptions.SkipStripeIntegration;
        var simulationRequired = !stripeHasApiKeys;
        var allowSimulationBypass = simulationRequired || simulationForced;
        var requiresStripeTransaction = stripeHasApiKeys && !allowSimulationBypass;

        PaymentTransaction? transaction = null;
        if (!string.IsNullOrWhiteSpace(paymentTransactionId))
        {
            transaction = await FindTransactionAsync(paymentTransactionId);

            if (transaction == null && requiresStripeTransaction)
            {
                _logger.LogWarning(
                    "Payment transaction {TransactionId} not found for user {UserId}",
                    LoggingSanitizer.SanitizeId(paymentTransactionId),
                    LoggingSanitizer.SanitizeId(userId));
                return new CreditPurchaseResult(false, PaymentStatus.Failed, null, "TransactionNotFound", "Payment transaction not found. Please retry the payment.");
            }
        }
        else if (requiresStripeTransaction)
        {
            _logger.LogWarning(
                "Stripe configured but payment transaction id missing for user {UserId} and package {PackageId}",
                LoggingSanitizer.SanitizeId(userId),
                packageId);
            return new CreditPurchaseResult(false, PaymentStatus.Failed, null, "PaymentRequired", "Payment must be completed before credits are awarded.");
        }

        if (transaction != null)
        {
            if (!string.Equals(transaction.UserId, userId, StringComparison.Ordinal))
            {
                _logger.LogWarning(
                    "Payment transaction {TransactionId} does not belong to user {UserId}",
                    LoggingSanitizer.SanitizeId(paymentTransactionId ?? transaction.Id.ToString()),
                    LoggingSanitizer.SanitizeId(userId));
                return new CreditPurchaseResult(false, PaymentStatus.Failed, null, "TransactionMismatch", "Payment transaction does not belong to the current user.");
            }

            // Manual review must not be bypassed by refreshing a succeeded Stripe intent.
            if (transaction.Status == PaymentStatus.PendingReview)
            {
                return new CreditPurchaseResult(false, transaction.Status, null, "PaymentPendingReview", "Payment requires review before the package can be fulfilled.");
            }

            if (transaction.Status == PaymentStatus.Refunded)
            {
                return new CreditPurchaseResult(false, transaction.Status, null, "PaymentFailed", "This payment has been refunded.");
            }

            var paymentVerified = await VerifyAndRefreshStripeTransactionAsync(transaction, packageId);
            if (paymentVerified != true)
            {
                var unavailable = paymentVerified == null;
                return new CreditPurchaseResult(
                    false,
                    PaymentStatus.Failed,
                    null,
                    unavailable ? "PaymentVerificationUnavailable" : "PaymentVerificationFailed",
                    unavailable
                        ? "Payment verification is temporarily unavailable. Please retry."
                        : "Unable to verify payment for this package. Please retry or contact support.");
            }

            if (transaction.Status == PaymentStatus.Pending)
            {
                return new CreditPurchaseResult(false, PaymentStatus.Pending, null, "PaymentPending", "Payment is still processing.");
            }

            if (transaction.Status is PaymentStatus.Failed or PaymentStatus.Cancelled or PaymentStatus.Refunded)
            {
                return new CreditPurchaseResult(false, transaction.Status, null, "PaymentFailed", transaction.FailureReason ?? "Payment failed.");
            }

            var purchase = await CreatePurchaseAndApplyCreditsAsync(
                userId,
                package,
                transaction.Id.ToString(),
                transaction.Amount,
                "stripe_credit_purchase",
                transaction.ExternalTransactionId);

            var success = purchase.Status is PaymentStatus.Completed or PaymentStatus.Succeeded;
            return new CreditPurchaseResult(success, purchase.Status, purchase);
        }

        if (!allowSimulationBypass)
        {
            _logger.LogWarning(
                "Simulation fallback disabled - payment transaction required for user {UserId} and package {PackageId}",
                LoggingSanitizer.SanitizeId(userId),
                packageId);
            return new CreditPurchaseResult(false, PaymentStatus.Failed, null, "PaymentRequired", "Unable to process purchase without a valid payment transaction.");
        }

        var simulatedPurchase = await CreatePurchaseAndApplyCreditsAsync(
            userId,
            package,
            paymentTransactionId,
            package.Price,
            "credit_package_purchase");

        var simulatedSuccess = simulatedPurchase.Status is PaymentStatus.Completed or PaymentStatus.Succeeded;
        return new CreditPurchaseResult(simulatedSuccess, simulatedPurchase.Status, simulatedPurchase);
    }

    private async Task<PaymentTransaction?> FindTransactionAsync(string paymentTransactionId)
    {
        if (int.TryParse(paymentTransactionId, out var transactionId))
        {
            return await _context.PaymentTransactions
                .FirstOrDefaultAsync(t => t.Id == transactionId);
        }

        return await _context.PaymentTransactions
            .FirstOrDefaultAsync(t => t.ExternalTransactionId == paymentTransactionId);
    }

    private async Task<CreditPurchase> CreatePurchaseAndApplyCreditsAsync(string userId, CreditPackage package, string? paymentTransactionId, decimal amountPaid, string creditSource, string? externalTransactionId = null)
    {
        var strategy = _context.Database.CreateExecutionStrategy();
        try
        {
            return await strategy.ExecuteAsync(async () =>
            {
                await using var transaction = _context.Database.IsRelational()
                    ? await _context.Database.BeginTransactionAsync()
                    : null;

                var purchase = new CreditPurchase
                {
                    UserId = userId,
                    PackageId = package.Id,
                    PurchaseDate = DateTime.UtcNow,
                    CreditsAwarded = package.TotalCredits,
                    AmountPaid = amountPaid,
                    PaymentTransactionId = paymentTransactionId,
                    PaymentProvider = externalTransactionId == null ? "simulation" : "stripe",
                    Status = PaymentStatus.Pending,
                    ExternalTransactionId = externalTransactionId
                };

                _context.CreditPurchases.Add(purchase);
                await _context.SaveChangesAsync();

                var creditsAdded = await _basicTierService.AddPurchasedCreditsAsync(userId, package.TotalCredits, creditSource);
                purchase.Status = creditsAdded ? PaymentStatus.Completed : PaymentStatus.Failed;
                purchase.CompletedAt = creditsAdded ? DateTime.UtcNow : null;

                if (!creditsAdded)
                {
                    _logger.LogError(
                        "Failed to add credits to user {UserId} for purchase {PurchaseId}",
                        LoggingSanitizer.SanitizeId(userId),
                        purchase.Id);
                }
                else if (_outcomePackageService != null)
                {
                    await _outcomePackageService.GrantEntitlementForCreditPackageAsync(userId, package.Id, paymentTransactionId);
                }

                await _context.SaveChangesAsync();
                if (transaction != null)
                {
                    await transaction.CommitAsync();
                }

                purchase.Package = package;
                return purchase;
            });
        }
        catch (DbUpdateException) when (!string.IsNullOrWhiteSpace(paymentTransactionId))
        {
            _context.ChangeTracker.Clear();
            var existingPurchase = await _context.CreditPurchases
                .Include(p => p.Package)
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.PaymentTransactionId == paymentTransactionId);
            if (existingPurchase != null)
            {
                return existingPurchase;
            }
            throw;
        }
    }

    private async Task<bool?> VerifyAndRefreshStripeTransactionAsync(PaymentTransaction transaction, int packageId)
    {
        if (_stripeClient == null)
        {
            return true;
        }
        if (string.IsNullOrWhiteSpace(transaction.ExternalTransactionId))
        {
            return false;
        }

        try
        {
            // Verify the first fulfillment even when a webhook already marked the local row completed.
            // The client-supplied package ID is not proof of which package the customer paid for.
            var intent = await new PaymentIntentService(_stripeClient).GetAsync(transaction.ExternalTransactionId);
            var expectedAmount = (long)Math.Round(transaction.Amount * 100, MidpointRounding.AwayFromZero);
            if (intent.Id != transaction.ExternalTransactionId ||
                intent.Metadata == null ||
                !intent.Metadata.TryGetValue("package_id", out var paidPackageId) || paidPackageId != packageId.ToString() ||
                !intent.Metadata.TryGetValue("user_id", out var paidUserId) || paidUserId != transaction.UserId ||
                intent.Amount != expectedAmount ||
                !string.Equals(intent.Currency, transaction.Currency, StringComparison.OrdinalIgnoreCase) ||
                (intent.Status == "succeeded" && intent.AmountReceived != expectedAmount))
            {
                _logger.LogWarning("Stripe payment does not match transaction {TransactionId} and package {PackageId}", transaction.Id, packageId);
                return false;
            }

            transaction.Status = intent.Status switch
            {
                "succeeded" => PaymentStatus.Completed,
                "canceled" => PaymentStatus.Cancelled,
                "requires_payment_method" when intent.LastPaymentError != null => PaymentStatus.Failed,
                _ => PaymentStatus.Pending
            };
            transaction.FailureReason = intent.LastPaymentError?.Message;
            transaction.UpdatedAt = DateTime.UtcNow;
            if (transaction.Status != PaymentStatus.Pending)
            {
                transaction.ProcessedAt ??= DateTime.UtcNow;
            }
            await _context.SaveChangesAsync();
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Unable to verify Stripe payment intent {PaymentIntentId}",
                LoggingSanitizer.SanitizeId(transaction.ExternalTransactionId));
            return null;
        }
    }

    public async Task<IEnumerable<CreditPurchase>> GetUserPurchaseHistoryAsync(string userId)
    {
        return await _context.CreditPurchases
            .Where(p => p.UserId == userId)
            .Include(p => p.Package)
            .OrderByDescending(p => p.PurchaseDate)
            .ToListAsync();
    }
}
