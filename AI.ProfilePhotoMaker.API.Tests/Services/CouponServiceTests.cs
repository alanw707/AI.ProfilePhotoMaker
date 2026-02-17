using AI.ProfilePhotoMaker.API.Data;
using AI.ProfilePhotoMaker.API.Models;
using AI.ProfilePhotoMaker.API.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace AI.ProfilePhotoMaker.API.Tests.Services;

public class CouponServiceTests
{
    [Fact]
    public async Task ValidateCoupon_ReturnsInvalid_WhenExpired()
    {
        using var context = CreateContext();
        await SeedCouponAsync(context, c => c.ExpiresAt = DateTime.UtcNow.AddDays(-1));
        var service = CreateService(context);

        var result = await service.ValidateCouponAsync("SAVE20", "u1", 10m);
        Assert.False(result.IsValid);
        Assert.Contains("expired", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ValidateCoupon_ReturnsInvalid_WhenMaxUsagesReached()
    {
        using var context = CreateContext();
        await SeedCouponAsync(context, c => c.CurrentUsages = c.MaxUsages);
        var service = CreateService(context);

        var result = await service.ValidateCouponAsync("SAVE20", "u1", 10m);
        Assert.False(result.IsValid);
        Assert.Contains("usage limit", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ValidateCoupon_ReturnsInvalid_WhenAlreadyRedeemedByUser()
    {
        using var context = CreateContext();
        var coupon = await SeedCouponAsync(context);
        context.CouponRedemptions.Add(new CouponRedemption { CouponId = coupon.Id, UserId = "u1", DiscountApplied = 1, OriginalPrice = 10, FinalPrice = 9 });
        await context.SaveChangesAsync();

        var service = CreateService(context);
        var result = await service.ValidateCouponAsync("SAVE20", "u1", 10m);

        Assert.False(result.IsValid);
        Assert.Contains("already used", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ValidateCoupon_ReturnsValid_WithCorrectDiscount_Percentage()
    {
        using var context = CreateContext();
        await SeedCouponAsync(context, c =>
        {
            c.DiscountType = DiscountType.Percentage;
            c.DiscountValue = 20;
        });
        var service = CreateService(context);

        var result = await service.ValidateCouponAsync("SAVE20", "u1", 50m);
        Assert.True(result.IsValid);
        Assert.Equal(10m, result.DiscountAmount);
    }

    [Fact]
    public async Task ValidateCoupon_ReturnsValid_WithCorrectDiscount_FixedAmount()
    {
        using var context = CreateContext();
        await SeedCouponAsync(context, c =>
        {
            c.DiscountType = DiscountType.FixedAmount;
            c.DiscountValue = 15;
        });
        var service = CreateService(context);

        var result = await service.ValidateCouponAsync("SAVE20", "u1", 50m);
        Assert.True(result.IsValid);
        Assert.Equal(15m, result.DiscountAmount);
    }

    [Fact]
    public async Task ValidateCoupon_ReturnsValid_WithCorrectDiscount_100Percent()
    {
        using var context = CreateContext();
        await SeedCouponAsync(context, c =>
        {
            c.DiscountType = DiscountType.Percentage;
            c.DiscountValue = 100;
        });
        var service = CreateService(context);

        var result = await service.ValidateCouponAsync("SAVE20", "u1", 25m);
        Assert.True(result.IsValid);
        Assert.Equal(25m, result.DiscountAmount);
    }

    [Fact]
    public async Task ValidateCoupon_FixedAmount_CapsAtOriginalPrice()
    {
        using var context = CreateContext();
        await SeedCouponAsync(context, c =>
        {
            c.DiscountType = DiscountType.FixedAmount;
            c.DiscountValue = 99;
        });
        var service = CreateService(context);

        var result = await service.ValidateCouponAsync("SAVE20", "u1", 20m);
        Assert.True(result.IsValid);
        Assert.Equal(20m, result.DiscountAmount);
    }

    [Fact]
    public async Task RedeemCoupon_IncrementsCurrentUsages()
    {
        using var context = CreateContext();
        var coupon = await SeedCouponAsync(context);
        var service = CreateService(context);

        var ok = await service.RedeemCouponAsync("SAVE20", "u1", 10m, 2m);
        Assert.True(ok);

        var updated = await context.Coupons.FirstAsync(c => c.Id == coupon.Id);
        Assert.Equal(1, updated.CurrentUsages);
    }

    [Fact]
    public async Task RedeemCoupon_CreatesCouponRedemptionRecord()
    {
        using var context = CreateContext();
        await SeedCouponAsync(context);
        var service = CreateService(context);

        var ok = await service.RedeemCouponAsync("SAVE20", "u1", 30m, 5m);
        Assert.True(ok);

        var redemption = await context.CouponRedemptions.FirstOrDefaultAsync(r => r.UserId == "u1");
        Assert.NotNull(redemption);
        Assert.Equal(25m, redemption!.FinalPrice);
    }

    [Fact]
    public async Task RedeemCoupon_UsesTransaction()
    {
        using var context = CreateContext();
        await SeedCouponAsync(context, c => c.CurrentUsages = c.MaxUsages);
        var service = CreateService(context);

        var ok = await service.RedeemCouponAsync("SAVE20", "u1", 10m, 2m);
        Assert.False(ok);
        Assert.Empty(await context.CouponRedemptions.ToListAsync());
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        return new ApplicationDbContext(options);
    }

    private static CouponService CreateService(ApplicationDbContext context)
    {
        return new CouponService(context, NullLogger<CouponService>.Instance);
    }

    private static async Task<Coupon> SeedCouponAsync(ApplicationDbContext context, Action<Coupon>? configure = null)
    {
        var coupon = new Coupon
        {
            Code = "SAVE20",
            DiscountType = DiscountType.Percentage,
            DiscountValue = 20,
            MaxUsages = 10,
            CurrentUsages = 0,
            IsActive = true,
            CreatedByAdminId = "admin-1",
            CreatedAt = DateTime.UtcNow
        };

        configure?.Invoke(coupon);
        context.Coupons.Add(coupon);
        await context.SaveChangesAsync();
        return coupon;
    }
}
