using System.Collections.Generic;
using AI.ProfilePhotoMaker.API.Data;
using AI.ProfilePhotoMaker.API.Models;
using Microsoft.EntityFrameworkCore;
using Stripe;

namespace AI.ProfilePhotoMaker.API.Services.Payments;

public class StripeWebhookService : IStripeWebhookService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly ICreditPackageService _creditPackageService;
    private readonly ILogger<StripeWebhookService> _logger;

    public StripeWebhookService(
        ApplicationDbContext dbContext,
        ICreditPackageService creditPackageService,
        ILogger<StripeWebhookService> logger)
    {
        _dbContext = dbContext;
        _creditPackageService = creditPackageService;
        _logger = logger;
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
            _logger.LogWarning("Stripe payment intent {PaymentIntentId} missing user_id metadata", paymentIntent.Id);
            return;
        }

        if (!metadata.TryGetValue("package_id", out var packageIdRaw) || !int.TryParse(packageIdRaw, out var packageId))
        {
            _logger.LogWarning("Stripe payment intent {PaymentIntentId} missing package_id metadata", paymentIntent.Id);
            return;
        }

        metadata.TryGetValue("payment_transaction_id", out var transactionIdRaw);

        PaymentTransaction? transaction = null;
        if (!string.IsNullOrWhiteSpace(transactionIdRaw) && int.TryParse(transactionIdRaw, out var transactionId))
        {
            transaction = await _dbContext.PaymentTransactions
                .FirstOrDefaultAsync(t => t.Id == transactionId, cancellationToken);
        }

        transaction ??= await _dbContext.PaymentTransactions
            .FirstOrDefaultAsync(t => t.ExternalTransactionId == paymentIntent.Id, cancellationToken);

        if (transaction == null)
        {
            _logger.LogWarning("No payment transaction found for payment intent {PaymentIntentId}", paymentIntent.Id);
            return;
        }

        if (!string.Equals(transaction.UserId, userId, StringComparison.Ordinal))
        {
            _logger.LogWarning("Payment transaction {TransactionId} user mismatch. Transaction user {TransactionUserId}, metadata user {MetadataUserId}", transaction.Id, transaction.UserId, userId);
            userId = transaction.UserId;
        }

        if (transaction.Status is PaymentStatus.Completed or PaymentStatus.Succeeded)
        {
            _logger.LogInformation("Stripe payment intent {PaymentIntentId} already processed for transaction {TransactionId}", paymentIntent.Id, transaction.Id);
        }
        else
        {
            transaction.Status = PaymentStatus.Completed;
            transaction.ProcessedAt = DateTime.UtcNow;
            transaction.UpdatedAt = DateTime.UtcNow;
            transaction.FailureReason = null;

            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        var purchaseResult = await _creditPackageService.PurchaseCreditPackageAsync(userId, packageId, transaction.Id.ToString());
        if (!purchaseResult.Success)
        {
            _logger.LogWarning("Credit purchase not finalized after Stripe webhook for transaction {TransactionId}: Status={Status} Code={Code} Message={Message}",
                transaction.Id, purchaseResult.Status, purchaseResult.ErrorCode, purchaseResult.ErrorMessage);
        }
        else
        {
            _logger.LogInformation("Stripe payment intent {PaymentIntentId} processed successfully for transaction {TransactionId}", paymentIntent.Id, transaction.Id);
        }
    }

    private async Task HandlePaymentIntentFailedAsync(string eventId, PaymentIntent paymentIntent, CancellationToken cancellationToken)
    {
        var transaction = await FindTransactionAsync(paymentIntent, cancellationToken);
        if (transaction == null)
        {
            return;
        }

        transaction.Status = PaymentStatus.Failed;
        transaction.FailureReason = paymentIntent.LastPaymentError?.Message ?? "Payment failed";
        transaction.ProcessedAt ??= DateTime.UtcNow;
        transaction.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogWarning("Stripe payment intent {PaymentIntentId} failed. Transaction {TransactionId} marked as failed", paymentIntent.Id, transaction.Id);
    }

    private async Task HandlePaymentIntentCanceledAsync(string eventId, PaymentIntent paymentIntent, CancellationToken cancellationToken)
    {
        var transaction = await FindTransactionAsync(paymentIntent, cancellationToken);
        if (transaction == null)
        {
            return;
        }

        transaction.Status = PaymentStatus.Cancelled;
        transaction.FailureReason = "Payment cancelled";
        transaction.ProcessedAt ??= DateTime.UtcNow;
        transaction.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Stripe payment intent {PaymentIntentId} cancelled. Transaction {TransactionId} marked as cancelled", paymentIntent.Id, transaction.Id);
    }

    private async Task<PaymentTransaction?> FindTransactionAsync(PaymentIntent paymentIntent, CancellationToken cancellationToken)
    {
        var metadata = paymentIntent.Metadata ?? new Dictionary<string, string>();
        metadata.TryGetValue("payment_transaction_id", out var transactionIdRaw);

        if (!string.IsNullOrWhiteSpace(transactionIdRaw) && int.TryParse(transactionIdRaw, out var transactionId))
        {
            var transaction = await _dbContext.PaymentTransactions
                .FirstOrDefaultAsync(t => t.Id == transactionId, cancellationToken);
            if (transaction != null)
            {
                return transaction;
            }
        }

        return await _dbContext.PaymentTransactions
            .FirstOrDefaultAsync(t => t.ExternalTransactionId == paymentIntent.Id, cancellationToken);
    }
}
