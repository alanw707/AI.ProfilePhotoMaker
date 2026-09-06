using System.Collections.Generic;
using System.Globalization;
using AI.ProfilePhotoMaker.API.Data;
using AI.ProfilePhotoMaker.API.Infrastructure.Logging;
using AI.ProfilePhotoMaker.API.Models;
using AI.ProfilePhotoMaker.API.Services.Notifications;
using AI.ProfilePhotoMaker.API.Services;
using Microsoft.EntityFrameworkCore;
using Stripe;

namespace AI.ProfilePhotoMaker.API.Services.Payments;

public class StripeWebhookService : IStripeWebhookService
{
    private static readonly TimeSpan WebhookLeaseDuration = TimeSpan.FromMinutes(10);
    private readonly ApplicationDbContext _dbContext;
    private readonly ICreditPackageService _creditPackageService;
    private readonly ILogger<StripeWebhookService> _logger;
    private readonly IEmailNotificationService _emailNotificationService;
    private readonly ICouponService _couponService;

    public StripeWebhookService(
        ApplicationDbContext dbContext,
        ICreditPackageService creditPackageService,
        ILogger<StripeWebhookService> logger,
        IEmailNotificationService emailNotificationService,
        ICouponService couponService)
    {
        _dbContext = dbContext;
        _creditPackageService = creditPackageService;
        _logger = logger;
        _emailNotificationService = emailNotificationService;
        _couponService = couponService;
    }

    public async Task HandleEventAsync(Event stripeEvent, CancellationToken cancellationToken = default)
    {
        var paymentIntentId = (stripeEvent.Data.Object as PaymentIntent)?.Id;
        var operationKey = string.IsNullOrWhiteSpace(paymentIntentId)
            ? stripeEvent.Id
            : $"{stripeEvent.Type}:{paymentIntentId}";
        var operationToken = await AcquireWebhookOperationAsync(stripeEvent, operationKey, paymentIntentId, cancellationToken);
        if (operationToken == null)
        {
            return;
        }

        try
        {
            await EnsureWebhookOperationOwnershipAsync(operationKey, operationToken, cancellationToken);
            switch (stripeEvent.Type)
            {
                case "payment_intent.succeeded":
                    if (stripeEvent.Data.Object is PaymentIntent succeededIntent)
                    {
                        await HandlePaymentIntentSucceededAsync(
                            stripeEvent.Id,
                            succeededIntent,
                            operationKey,
                            operationToken,
                            cancellationToken);
                    }
                    break;
                case "payment_intent.payment_failed":
                    if (stripeEvent.Data.Object is PaymentIntent failedIntent)
                    {
                        await HandlePaymentIntentFailedAsync(stripeEvent.Id, failedIntent, cancellationToken);
                    }
                    break;
                case "payment_intent.canceled":
                    if (stripeEvent.Data.Object is PaymentIntent canceledIntent)
                    {
                        await HandlePaymentIntentCanceledAsync(stripeEvent.Id, canceledIntent, cancellationToken);
                    }
                    break;
                default:
                    _logger.LogDebug("Ignoring Stripe event type {EventType}", stripeEvent.Type);
                    break;
            }

            await EnsureWebhookOperationOwnershipAsync(operationKey, operationToken, cancellationToken);
            await CompleteWebhookOperationAsync(operationKey, operationToken, cancellationToken);
        }
        catch (Exception ex)
        {
            try
            {
                await FailWebhookOperationAsync(operationKey, operationToken, ex.GetType().Name, CancellationToken.None);
            }
            catch (Exception receiptException)
            {
                _logger.LogError(receiptException, "Unable to persist failed Stripe webhook operation {OperationKey}", LoggingSanitizer.SanitizeId(operationKey));
            }
            throw;
        }
    }

    private async Task HandlePaymentIntentSucceededAsync(
        string eventId,
        PaymentIntent paymentIntent,
        string operationKey,
        string operationToken,
        CancellationToken cancellationToken)
    {
        var metadata = paymentIntent.Metadata ?? new Dictionary<string, string>();

        if (!metadata.TryGetValue("user_id", out var userId) || string.IsNullOrWhiteSpace(userId))
        {
            _logger.LogWarning("Stripe payment intent {PaymentIntentId} missing user_id metadata",
                LoggingSanitizer.SanitizeId(paymentIntent.Id));
            return;
        }

        if (!metadata.TryGetValue("package_id", out var packageIdRaw) || !int.TryParse(packageIdRaw, out var packageId))
        {
            _logger.LogWarning("Stripe payment intent {PaymentIntentId} missing package_id metadata",
                LoggingSanitizer.SanitizeId(paymentIntent.Id));
            return;
        }

        metadata.TryGetValue("payment_transaction_id", out var transactionIdRaw);
        metadata.TryGetValue("preview_processed_image_id", out var previewProcessedImageIdRaw);
        var previewProcessedImageId = int.TryParse(previewProcessedImageIdRaw, out var parsedPreviewId) && parsedPreviewId > 0
            ? parsedPreviewId
            : (int?)null;
        metadata.TryGetValue("coupon_code", out var couponCode);
        metadata.TryGetValue("original_price", out var originalPriceRaw);
        metadata.TryGetValue("discount_amount", out var discountAmountRaw);

        PaymentTransaction? transaction = null;
        if (!string.IsNullOrWhiteSpace(transactionIdRaw) && int.TryParse(transactionIdRaw, out var transactionId))
        {
            transaction = await _dbContext.PaymentTransactions
                .FirstOrDefaultAsync(
                    t => t.Id == transactionId && t.ExternalTransactionId == paymentIntent.Id,
                    cancellationToken);
        }

        transaction ??= await _dbContext.PaymentTransactions
            .FirstOrDefaultAsync(t => t.ExternalTransactionId == paymentIntent.Id, cancellationToken);

        if (transaction == null)
        {
            _logger.LogWarning("No payment transaction found for payment intent {PaymentIntentId}",
                LoggingSanitizer.SanitizeId(paymentIntent.Id));
            return;
        }

        if (transaction.Status == PaymentStatus.PendingReview)
        {
            _logger.LogWarning(
                "Stripe payment intent {PaymentIntentId} remains blocked for manual review on transaction {TransactionId}",
                LoggingSanitizer.SanitizeId(paymentIntent.Id),
                transaction.Id);
            return;
        }

        if (!string.Equals(transaction.UserId, userId, StringComparison.Ordinal))
        {
            _logger.LogError(
                "Stripe payment intent {PaymentIntentId} user metadata does not match transaction {TransactionId}; fulfillment blocked",
                LoggingSanitizer.SanitizeId(paymentIntent.Id),
                transaction.Id);
            transaction.Status = PaymentStatus.PendingReview;
            transaction.FailureReason = "Stripe payment user metadata mismatch. Manual review required.";
            transaction.UpdatedAt = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync(cancellationToken);
            return;
        }

        if (transaction.Status is PaymentStatus.Completed or PaymentStatus.Succeeded)
        {
            var alreadyFulfilled = await _dbContext.CreditPurchases.AsNoTracking().AnyAsync(
                p => p.PaymentTransactionId == transaction.Id.ToString() &&
                     (p.Status == PaymentStatus.Completed || p.Status == PaymentStatus.Succeeded),
                cancellationToken);
            if (alreadyFulfilled)
            {
                _logger.LogInformation("Stripe payment intent {PaymentIntentId} already processed for transaction {TransactionId}",
                    LoggingSanitizer.SanitizeId(paymentIntent.Id),
                    transaction.Id);
                return;
            }
        }
        else
        {
            transaction.Status = PaymentStatus.Completed;
            transaction.ProcessedAt = DateTime.UtcNow;
            transaction.UpdatedAt = DateTime.UtcNow;
            transaction.FailureReason = null;

            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        bool couponRedemptionFailed = false;
        if (!string.IsNullOrWhiteSpace(couponCode)
            && decimal.TryParse(originalPriceRaw, NumberStyles.Number, CultureInfo.InvariantCulture, out var originalPrice)
            && decimal.TryParse(discountAmountRaw, NumberStyles.Number, CultureInfo.InvariantCulture, out var discountAmount)
            && discountAmount > 0)
        {
            var redeemed = await _couponService.RedeemCouponAsync(
                couponCode,
                userId,
                originalPrice,
                discountAmount,
                transaction.Id,
                operationKey,
                operationToken);
            if (!redeemed)
            {
                couponRedemptionFailed = true;
                _logger.LogError("CRITICAL: Coupon redemption failed after successful payment intent {PaymentIntentId}. Transaction {TransactionId} requires manual review. User {UserId} paid {Amount} but coupon {CouponCode} was not redeemed.",
                    LoggingSanitizer.SanitizeId(paymentIntent.Id),
                    transaction.Id,
                    LoggingSanitizer.SanitizeId(userId),
                    originalPrice,
                    LoggingSanitizer.Sanitize(couponCode));
            }
        }

        // If coupon redemption failed, flag transaction for manual review instead of processing normally
        if (couponRedemptionFailed)
        {
            transaction.Status = PaymentStatus.PendingReview;
            transaction.FailureReason = $"Coupon redemption failed for coupon {couponCode}. Manual review required.";
            transaction.ProcessedAt = DateTime.UtcNow;
            transaction.UpdatedAt = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogWarning("Transaction {TransactionId} marked as PendingReview due to coupon redemption failure. User {UserId} paid but credits were NOT awarded.",
                transaction.Id,
                LoggingSanitizer.SanitizeId(userId));
            return;
        }

        var purchaseResult = await _creditPackageService.PurchaseCreditPackageAsync(
            userId,
            packageId,
            transaction.Id.ToString(),
            previewProcessedImageId,
            operationKey,
            operationToken);
        if (!purchaseResult.Success)
        {
            _logger.LogWarning("Credit purchase not finalized after Stripe webhook for transaction {TransactionId}: Status={Status} Code={Code} Message={Message}",
                transaction.Id,
                purchaseResult.Status,
                LoggingSanitizer.Sanitize(purchaseResult.ErrorCode),
                LoggingSanitizer.Sanitize(purchaseResult.ErrorMessage));
            if (purchaseResult.ErrorCode is "PaymentVerificationUnavailable" or "PreviewUnavailable" or "PackageNotFound")
            {
                throw new InvalidOperationException("Paid purchase delivery is incomplete; Stripe should retry this webhook.");
            }

            transaction.Status = PaymentStatus.PendingReview;
            transaction.FailureReason = $"Package fulfillment failed ({purchaseResult.ErrorCode ?? "unknown"}). Manual review required.";
            transaction.UpdatedAt = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        else
        {
            _logger.LogInformation("Stripe payment intent {PaymentIntentId} processed successfully for transaction {TransactionId}",
                LoggingSanitizer.SanitizeId(paymentIntent.Id),
                transaction.Id);

            if (purchaseResult.Purchase != null)
            {
                try
                {
                    var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
                    await _emailNotificationService.SendPurchaseReceiptAsync(userId, user?.Email, purchaseResult.Purchase);
                }
                catch (Exception emailEx)
                {
                    _logger.LogWarning("Failed to send purchase receipt email for user {UserId}: {Reason}", LoggingSanitizer.SanitizeId(userId), LoggingSanitizer.Sanitize(emailEx.Message));
                }
            }
        }
    }

    private async Task HandlePaymentIntentFailedAsync(string eventId, PaymentIntent paymentIntent, CancellationToken cancellationToken)
    {
        var transaction = await FindTransactionAsync(paymentIntent, cancellationToken);
        if (transaction == null)
        {
            return;
        }

        if (transaction.Status is PaymentStatus.Completed or PaymentStatus.Succeeded)
        {
            _logger.LogWarning(
                "Ignoring out-of-order failed event for completed transaction {TransactionId}",
                transaction.Id);
            return;
        }

        transaction.Status = PaymentStatus.Failed;
        transaction.FailureReason = paymentIntent.LastPaymentError?.Message ?? "Payment failed";
        transaction.ProcessedAt ??= DateTime.UtcNow;
        transaction.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogWarning("Stripe payment intent {PaymentIntentId} failed. Transaction {TransactionId} marked as failed",
            LoggingSanitizer.SanitizeId(paymentIntent.Id),
            transaction.Id);
    }

    private async Task HandlePaymentIntentCanceledAsync(string eventId, PaymentIntent paymentIntent, CancellationToken cancellationToken)
    {
        var transaction = await FindTransactionAsync(paymentIntent, cancellationToken);
        if (transaction == null)
        {
            return;
        }

        if (transaction.Status is PaymentStatus.Completed or PaymentStatus.Succeeded)
        {
            _logger.LogWarning(
                "Ignoring out-of-order canceled event for completed transaction {TransactionId}",
                transaction.Id);
            return;
        }

        transaction.Status = PaymentStatus.Cancelled;
        transaction.FailureReason = "Payment cancelled";
        transaction.ProcessedAt ??= DateTime.UtcNow;
        transaction.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Stripe payment intent {PaymentIntentId} cancelled. Transaction {TransactionId} marked as cancelled",
            LoggingSanitizer.SanitizeId(paymentIntent.Id),
            transaction.Id);
    }

    private async Task<string?> AcquireWebhookOperationAsync(
        Event stripeEvent,
        string operationKey,
        string? paymentIntentId,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var operationToken = Guid.NewGuid().ToString("N");
        if (!_dbContext.Database.IsRelational())
        {
            var existing = await _dbContext.StripeWebhookOperations
                .FirstOrDefaultAsync(o => o.OperationKey == operationKey, cancellationToken);
            if (existing != null)
            {
                if (existing.Status == StripeWebhookOperationStatus.Succeeded)
                {
                    return null;
                }
                if (existing.Status == StripeWebhookOperationStatus.Processing)
                {
                    throw new InvalidOperationException("This Stripe webhook operation is already being processed or requires reconciliation.");
                }

                existing.StripeEventId = stripeEvent.Id;
                existing.Status = StripeWebhookOperationStatus.Processing;
                existing.OperationToken = operationToken;
                existing.AttemptCount += 1;
                existing.LeaseExpiresAt = now.Add(WebhookLeaseDuration);
                existing.FailureCode = null;
                existing.UpdatedAt = now;
                await _dbContext.SaveChangesAsync(cancellationToken);
                return operationToken;
            }

            _dbContext.StripeWebhookOperations.Add(CreateWebhookOperation(stripeEvent, operationKey, paymentIntentId, operationToken, now));
            await _dbContext.SaveChangesAsync(cancellationToken);
            return operationToken;
        }

        // Processing receipts are never reclaimed automatically. A crashed worker must be
        // reconciled before retry so fulfillment remains at-most-once.
        var reclaimed = await _dbContext.StripeWebhookOperations
            .Where(o => o.OperationKey == operationKey &&
                        o.Status == StripeWebhookOperationStatus.Failed)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(o => o.StripeEventId, stripeEvent.Id)
                .SetProperty(o => o.Status, StripeWebhookOperationStatus.Processing)
                .SetProperty(o => o.OperationToken, operationToken)
                .SetProperty(o => o.AttemptCount, o => o.AttemptCount + 1)
                .SetProperty(o => o.LeaseExpiresAt, now.Add(WebhookLeaseDuration))
                .SetProperty(o => o.FailureCode, (string?)null)
                .SetProperty(o => o.UpdatedAt, now), cancellationToken);
        if (reclaimed == 1)
        {
            return operationToken;
        }

        var existingStatus = await _dbContext.StripeWebhookOperations
            .AsNoTracking()
            .Where(o => o.OperationKey == operationKey)
            .Select(o => (StripeWebhookOperationStatus?)o.Status)
            .FirstOrDefaultAsync(cancellationToken);
        if (existingStatus == StripeWebhookOperationStatus.Succeeded)
        {
            return null;
        }
        if (existingStatus == StripeWebhookOperationStatus.Processing)
        {
            throw new InvalidOperationException("This Stripe webhook operation is already being processed.");
        }

        var operation = CreateWebhookOperation(stripeEvent, operationKey, paymentIntentId, operationToken, now);
        _dbContext.StripeWebhookOperations.Add(operation);
        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
            return operationToken;
        }
        catch (DbUpdateException)
        {
            _dbContext.Entry(operation).State = EntityState.Detached;
            var winner = await _dbContext.StripeWebhookOperations
                .AsNoTracking()
                .FirstOrDefaultAsync(o => o.OperationKey == operationKey, cancellationToken);
            if (winner == null)
            {
                throw;
            }
            if (winner.Status == StripeWebhookOperationStatus.Succeeded)
            {
                return null;
            }
            throw new InvalidOperationException("This Stripe webhook operation is already being processed.");
        }
    }

    private static StripeWebhookOperation CreateWebhookOperation(
        Event stripeEvent,
        string operationKey,
        string? paymentIntentId,
        string operationToken,
        DateTime now) => new()
    {
        OperationKey = operationKey,
        StripeEventId = stripeEvent.Id,
        EventType = stripeEvent.Type,
        PaymentIntentId = paymentIntentId,
        Status = StripeWebhookOperationStatus.Processing,
        OperationToken = operationToken,
        AttemptCount = 1,
        LeaseExpiresAt = now.Add(WebhookLeaseDuration),
        CreatedAt = now,
        UpdatedAt = now
    };

    private async Task EnsureWebhookOperationOwnershipAsync(
        string operationKey,
        string operationToken,
        CancellationToken cancellationToken)
    {
        var ownsOperation = await _dbContext.StripeWebhookOperations
            .AsNoTracking()
            .AnyAsync(o => o.OperationKey == operationKey &&
                           o.OperationToken == operationToken &&
                           o.Status == StripeWebhookOperationStatus.Processing,
                cancellationToken);
        if (!ownsOperation)
        {
            throw new InvalidOperationException("Stripe webhook operation ownership was lost.");
        }
    }

    private async Task CompleteWebhookOperationAsync(string operationKey, string operationToken, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        if (!_dbContext.Database.IsRelational())
        {
            var operation = await _dbContext.StripeWebhookOperations
                .FirstAsync(o => o.OperationKey == operationKey &&
                                 o.OperationToken == operationToken &&
                                 o.Status == StripeWebhookOperationStatus.Processing,
                    cancellationToken);
            operation.Status = StripeWebhookOperationStatus.Succeeded;
            operation.CompletedAt = now;
            operation.LeaseExpiresAt = now;
            operation.UpdatedAt = now;
            await _dbContext.SaveChangesAsync(cancellationToken);
            return;
        }

        var updated = await _dbContext.StripeWebhookOperations
            .Where(o => o.OperationKey == operationKey &&
                        o.OperationToken == operationToken &&
                        o.Status == StripeWebhookOperationStatus.Processing)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(o => o.Status, StripeWebhookOperationStatus.Succeeded)
                .SetProperty(o => o.CompletedAt, now)
                .SetProperty(o => o.LeaseExpiresAt, now)
                .SetProperty(o => o.UpdatedAt, now), cancellationToken);
        if (updated != 1)
        {
            throw new InvalidOperationException("Unable to complete the Stripe webhook operation.");
        }
    }

    private async Task FailWebhookOperationAsync(string operationKey, string operationToken, string failureCode, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        if (!_dbContext.Database.IsRelational())
        {
            var operation = await _dbContext.StripeWebhookOperations
                .FirstOrDefaultAsync(o => o.OperationKey == operationKey &&
                                           o.OperationToken == operationToken &&
                                           o.Status == StripeWebhookOperationStatus.Processing,
                    cancellationToken);
            if (operation != null)
            {
                operation.Status = StripeWebhookOperationStatus.Failed;
                operation.FailureCode = failureCode;
                operation.LeaseExpiresAt = now;
                operation.UpdatedAt = now;
                await _dbContext.SaveChangesAsync(cancellationToken);
            }
            return;
        }

        await _dbContext.StripeWebhookOperations
            .Where(o => o.OperationKey == operationKey &&
                        o.OperationToken == operationToken &&
                        o.Status == StripeWebhookOperationStatus.Processing)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(o => o.Status, StripeWebhookOperationStatus.Failed)
                .SetProperty(o => o.FailureCode, failureCode)
                .SetProperty(o => o.LeaseExpiresAt, now)
                .SetProperty(o => o.UpdatedAt, now), cancellationToken);
    }

    private async Task<PaymentTransaction?> FindTransactionAsync(PaymentIntent paymentIntent, CancellationToken cancellationToken)
    {
        var metadata = paymentIntent.Metadata ?? new Dictionary<string, string>();
        metadata.TryGetValue("payment_transaction_id", out var transactionIdRaw);

        if (!string.IsNullOrWhiteSpace(transactionIdRaw) && int.TryParse(transactionIdRaw, out var transactionId))
        {
            var transaction = await _dbContext.PaymentTransactions
                .FirstOrDefaultAsync(
                    t => t.Id == transactionId && t.ExternalTransactionId == paymentIntent.Id,
                    cancellationToken);
            if (transaction != null)
            {
                return transaction;
            }
        }

        return await _dbContext.PaymentTransactions
            .FirstOrDefaultAsync(t => t.ExternalTransactionId == paymentIntent.Id, cancellationToken);
    }
}
