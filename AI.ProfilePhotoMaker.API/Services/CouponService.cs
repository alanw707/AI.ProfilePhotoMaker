using AI.ProfilePhotoMaker.API.Data;
using AI.ProfilePhotoMaker.API.Infrastructure.Logging;
using AI.ProfilePhotoMaker.API.Models;
using Microsoft.EntityFrameworkCore;

namespace AI.ProfilePhotoMaker.API.Services;

public class CouponService : ICouponService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<CouponService> _logger;

    public CouponService(ApplicationDbContext context, ILogger<CouponService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<(bool IsValid, string Message, decimal DiscountAmount)> ValidateCouponAsync(string code, string userId, decimal originalPrice)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return (false, "Coupon code is required", 0m);
        }

        if (originalPrice <= 0)
        {
            return (false, "Original price must be greater than zero", 0m);
        }

        var normalizedCode = code.Trim().ToUpperInvariant();
        var coupon = await _context.Coupons
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Code == normalizedCode);

        if (coupon == null)
        {
            return (false, "Coupon not found", 0m);
        }

        if (!coupon.IsActive)
        {
            return (false, "Coupon is inactive", 0m);
        }

        if (coupon.ExpiresAt.HasValue && coupon.ExpiresAt.Value < DateTime.UtcNow)
        {
            return (false, "Coupon has expired", 0m);
        }

        if (coupon.CurrentUsages >= coupon.MaxUsages)
        {
            return (false, "Coupon has reached its usage limit", 0m);
        }

        var alreadyRedeemed = await _context.CouponRedemptions
            .AsNoTracking()
            .AnyAsync(r => r.CouponId == coupon.Id && r.UserId == userId);

        if (alreadyRedeemed)
        {
            return (false, "Coupon already used", 0m);
        }

        var discountAmount = coupon.DiscountType == DiscountType.Percentage
            ? Math.Round(originalPrice * (coupon.DiscountValue / 100m), 2, MidpointRounding.AwayFromZero)
            : Math.Min(coupon.DiscountValue, originalPrice);

        if (discountAmount <= 0)
        {
            return (false, "Coupon discount is invalid", 0m);
        }

        return (true, "Coupon is valid", discountAmount);
    }

    public async Task<bool> RedeemCouponAsync(
        string code,
        string userId,
        decimal originalPrice,
        decimal discountApplied,
        int? paymentTransactionId = null,
        string? webhookOperationKey = null,
        string? webhookOperationToken = null)
    {
        if (string.IsNullOrWhiteSpace(code) || discountApplied <= 0)
        {
            return false;
        }

        var normalizedCode = code.Trim().ToUpperInvariant();

        var strategy = _context.Database.CreateExecutionStrategy();
        var firstAttempt = true;
        try
        {
            return await strategy.ExecuteAsync(async () =>
            {
                if (!firstAttempt)
                {
                    _context.ChangeTracker.Clear();
                }
                firstAttempt = false;
                await using var transaction = await _context.Database.BeginTransactionAsync();

                if (!string.IsNullOrWhiteSpace(webhookOperationKey) && !string.IsNullOrWhiteSpace(webhookOperationToken))
                {
                    var fenced = await _context.StripeWebhookOperations
                        .Where(operation => operation.OperationKey == webhookOperationKey &&
                                            operation.OperationToken == webhookOperationToken &&
                                            operation.Status == StripeWebhookOperationStatus.Processing)
                        .ExecuteUpdateAsync(setters => setters
                            .SetProperty(operation => operation.UpdatedAt, operation => operation.UpdatedAt));
                    if (fenced != 1)
                    {
                        throw new InvalidOperationException("Stripe webhook operation ownership was lost before coupon redemption.");
                    }
                }

                var coupon = await _context.Coupons
                    .FirstOrDefaultAsync(c => c.Code == normalizedCode);

                if (coupon == null)
                {
                    return false;
                }

                var existingRedemption = await _context.CouponRedemptions
                    .AsNoTracking()
                    .FirstOrDefaultAsync(r => r.CouponId == coupon.Id && r.UserId == userId);
                if (existingRedemption != null)
                {
                    return paymentTransactionId.HasValue &&
                           existingRedemption.PaymentTransactionId == paymentTransactionId &&
                           existingRedemption.OriginalPrice == originalPrice &&
                           existingRedemption.DiscountApplied == discountApplied;
                }

                if (!coupon.IsActive)
                {
                    return false;
                }

                if (coupon.ExpiresAt.HasValue && coupon.ExpiresAt.Value < DateTime.UtcNow)
                {
                    return false;
                }

                if (coupon.CurrentUsages >= coupon.MaxUsages)
                {
                    return false;
                }

                coupon.CurrentUsages += 1;
                coupon.UpdatedAt = DateTime.UtcNow;

                var finalPrice = Math.Max(0m, originalPrice - discountApplied);
                _context.CouponRedemptions.Add(new CouponRedemption
                {
                    CouponId = coupon.Id,
                    UserId = userId,
                    DiscountApplied = discountApplied,
                    OriginalPrice = originalPrice,
                    FinalPrice = finalPrice,
                    PaymentTransactionId = paymentTransactionId,
                    RedeemedAt = DateTime.UtcNow
                });

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
                return true;
            });
        }
        catch (Exception ex)
        {
            // Rolled-back entities must not leak into the caller's receipt-status save.
            _context.ChangeTracker.Clear();
            _logger.LogError(ex, "Coupon redemption persistence failed for {CouponCode} and user {UserId}",
                LoggingSanitizer.SanitizeId(code),
                LoggingSanitizer.SanitizeId(userId));
            throw;
        }
    }
}
