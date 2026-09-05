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
        switch (stripeEvent.Type)
        {
            case "payment_intent.succeeded":
                if (stripeEvent.Data.Object is PaymentIntent succeededIntent)
                {
                    await HandlePaymentIntentSucceededAsync(stripeEvent.Id, succeededIntent, cancellationToken);
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
    }

    private async Task HandlePaymentIntentSucceededAsync(string eventId, PaymentIntent paymentIntent, CancellationToken cancellationToken)
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
            var redeemed = await _couponService.RedeemCouponAsync(couponCode, userId, originalPrice, discountAmount);
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

        var purchaseResult = await _creditPackageService.PurchaseCreditPackageAsync(userId, packageId, transaction.Id.ToString());
        if (!purchaseResult.Success)
        {
            _logger.LogWarning("Credit purchase not finalized after Stripe webhook for transaction {TransactionId}: Status={Status} Code={Code} Message={Message}",
                transaction.Id,
                purchaseResult.Status,
                LoggingSanitizer.Sanitize(purchaseResult.ErrorCode),
                LoggingSanitizer.Sanitize(purchaseResult.ErrorMessage));

            if (purchaseResult.ErrorCode == "PaymentVerificationUnavailable")
            {
                throw new InvalidOperationException("Stripe payment verification is temporarily unavailable.");
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
