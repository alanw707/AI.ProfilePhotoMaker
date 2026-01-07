using AI.ProfilePhotoMaker.API.Configuration;
using AI.ProfilePhotoMaker.API.Data;
using AI.ProfilePhotoMaker.API.Infrastructure.Logging;
using AI.ProfilePhotoMaker.API.Models;
using AI.ProfilePhotoMaker.API.Models.DTOs;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Stripe;

namespace AI.ProfilePhotoMaker.API.Services;

public class CreditPackageService : ICreditPackageService
{
    private readonly ApplicationDbContext _context;
    private readonly IBasicTierService _basicTierService;
    private readonly ILogger<CreditPackageService> _logger;
    private readonly StripeOptions _stripeOptions;
    private readonly PaymentSimulationOptions _simulationOptions;
    private readonly StripeClient? _stripeClient;

    public CreditPackageService(
        ApplicationDbContext context,
        IBasicTierService basicTierService,
        ILogger<CreditPackageService> logger,
        IOptions<StripeOptions> stripeOptions,
        IOptions<PaymentSimulationOptions> paymentSimulationOptions,
        StripeClient? stripeClient = null)
    {
        _context = context;
        _basicTierService = basicTierService;
        _logger = logger;
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
            await RefreshStripeTransactionStatusAsync(transaction);

            if (!string.Equals(transaction.UserId, userId, StringComparison.Ordinal))
            {
                _logger.LogWarning(
                    "Payment transaction {TransactionId} does not belong to user {UserId}",
                    LoggingSanitizer.SanitizeId(paymentTransactionId ?? transaction.Id.ToString()),
                    LoggingSanitizer.SanitizeId(userId));
                return new CreditPurchaseResult(false, PaymentStatus.Failed, null, "TransactionMismatch", "Payment transaction does not belong to the current user.");
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

    private static bool IsUniqueConstraintViolation(DbUpdateException exception)
    {
        if (exception.InnerException is SqlException sqlException)
        {
            return sqlException.Number is 2601 or 2627;
        }

        return false;
    }

    private async Task<CreditPurchase> CreatePurchaseAndApplyCreditsAsync(string userId, CreditPackage package, string? paymentTransactionId, decimal amountPaid, string creditSource, string? externalTransactionId = null)
    {
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

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex) && !string.IsNullOrWhiteSpace(paymentTransactionId))
        {
            _context.Entry(purchase).State = EntityState.Detached;

            var existingPurchase = await _context.CreditPurchases
                .Include(p => p.Package)
                .AsNoTracking()
                .FirstAsync(p => p.PaymentTransactionId == paymentTransactionId);

            return existingPurchase;
        }

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

        await _context.SaveChangesAsync();

        purchase.Package = package;
        return purchase;
    }

    private async Task RefreshStripeTransactionStatusAsync(PaymentTransaction transaction)
    {
        if (_stripeClient == null || string.IsNullOrWhiteSpace(transaction.ExternalTransactionId))
        {
            return;
        }

        if (transaction.Status is PaymentStatus.Completed or PaymentStatus.Succeeded)
        {
            return;
        }

        var paymentIntentService = new PaymentIntentService(_stripeClient);

        try
        {
            var intent = await paymentIntentService.GetAsync(transaction.ExternalTransactionId);

            switch (intent.Status)
            {
                case "succeeded":
                    transaction.Status = PaymentStatus.Completed;
                    transaction.FailureReason = null;
                    transaction.ProcessedAt ??= DateTime.UtcNow;
                    transaction.UpdatedAt = DateTime.UtcNow;
                    await _context.SaveChangesAsync();
                    break;
                case "processing":
                case "requires_capture":
                    transaction.Status = PaymentStatus.Pending;
                    transaction.UpdatedAt = DateTime.UtcNow;
                    await _context.SaveChangesAsync();
                    break;
                case "requires_payment_method":
                case "requires_confirmation":
                    // Payment is waiting for a new confirmation; leave status as-is so UI can retry.
                    break;
                default:
                    break;
            }
        }
        catch (StripeException stripeException)
        {
            _logger.LogWarning(stripeException,
                "Unable to refresh Stripe payment intent {PaymentIntentId}",
                LoggingSanitizer.SanitizeId(transaction.ExternalTransactionId));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Unexpected error while refreshing Stripe payment intent {PaymentIntentId}",
                LoggingSanitizer.SanitizeId(transaction.ExternalTransactionId));
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
