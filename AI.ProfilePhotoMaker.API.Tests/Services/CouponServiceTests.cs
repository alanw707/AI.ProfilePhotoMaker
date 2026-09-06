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
    public async Task RedeemCoupon_RetryForSamePaymentTransactionIsIdempotent()
    {
        using var context = CreateContext();
        var coupon = await SeedCouponAsync(context);
        var service = CreateService(context);

        Assert.True(await service.RedeemCouponAsync("SAVE20", "u1", 10m, 2m, 42));
        Assert.True(await service.RedeemCouponAsync("SAVE20", "u1", 10m, 2m, 42));

        Assert.Single(await context.CouponRedemptions.ToListAsync());
        Assert.Equal(1, (await context.Coupons.SingleAsync(c => c.Id == coupon.Id)).CurrentUsages);
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

    [Fact]
    public async Task RedeemCoupon_PersistenceFailureDoesNotLeakChangesIntoLaterSave()
    {
        var interceptor = new FailRedemptionSaveOnce();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .AddInterceptors(interceptor)
            .Options;
        using var context = new ApplicationDbContext(options);
        await SeedCouponAsync(context);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreateService(context).RedeemCouponAsync("SAVE20", "u1", 10m, 2m, 42));

        // Saving a failed webhook receipt must not flush the rejected redemption.
        await context.SaveChangesAsync();
        Assert.Empty(await context.CouponRedemptions.ToListAsync());
        Assert.Equal(0, (await context.Coupons.AsNoTracking().SingleAsync()).CurrentUsages);
        Assert.True(await CreateService(context).RedeemCouponAsync("SAVE20", "u1", 10m, 2m, 42));
        Assert.Single(await context.CouponRedemptions.ToListAsync());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RedeemCoupon_ExecutionStrategyRetryRedeemsExactlyOnce(bool afterCommit)
    {
        var fault = new CreditPackageServiceTests.CommitFault(afterCommit);
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite("Data Source=:memory:")
            .ReplaceService<Microsoft.EntityFrameworkCore.Storage.IExecutionStrategyFactory,
                CreditPackageServiceTests.RetryFactory>()
            .AddInterceptors(fault).Options;
        using var context = new ApplicationDbContext(options);
        await context.Database.OpenConnectionAsync();
        await context.Database.EnsureCreatedAsync();
        context.Users.Add(new ApplicationUser { Id = "u1", UserName = "retry-test" });
        context.PaymentTransactions.Add(new PaymentTransaction
        {
            Id = 42, UserId = "u1", Amount = 10m, Status = PaymentStatus.Completed,
            Type = PaymentType.OneTime
        });
        await SeedCouponAsync(context);
        fault.Armed = true;

        Assert.True(await CreateService(context).RedeemCouponAsync("SAVE20", "u1", 10m, 2m, 42));

        Assert.True(fault.Fired);
        Assert.Single(await context.CouponRedemptions.AsNoTracking().ToListAsync());
        Assert.Equal(1, (await context.Coupons.AsNoTracking().SingleAsync()).CurrentUsages);
    }

    private sealed class FailRedemptionSaveOnce : SaveChangesInterceptor
    {
        private bool _failed;

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            if (!_failed && eventData.Context!.ChangeTracker.Entries<CouponRedemption>()
                    .Any(entry => entry.State == EntityState.Added))
            {
                _failed = true;
                throw new InvalidOperationException("Injected coupon persistence outage");
            }
            return ValueTask.FromResult(result);
        }
    }

    [Fact]
    public async Task RedeemCoupon_DifferentPaymentsCompeteForLastUseWithoutOversubscription()
    {
        await using var connection = new Microsoft.Data.Sqlite.SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options;
        using var first = new ApplicationDbContext(options);
        await first.Database.EnsureCreatedAsync();
        foreach (var id in new[] { 101, 102 })
        {
            var userId = $"coupon-user-{id}";
            first.Users.Add(new ApplicationUser { Id = userId, UserName = userId });
            first.PaymentTransactions.Add(new PaymentTransaction
            {
                Id = id, UserId = userId, Amount = 8m,
                ExternalTransactionId = $"pi_coupon_{id}", Status = PaymentStatus.Completed,
                Type = PaymentType.OneTime
            });
        }
        var coupon = await SeedCouponAsync(first, c => c.MaxUsages = 1);
        using var second = new ApplicationDbContext(options);
        // Both payment contexts read the final available use before either redemption.
        await second.Coupons.SingleAsync(c => c.Id == coupon.Id);
        Assert.True(await CreateService(first).RedeemCouponAsync("SAVE20", "coupon-user-101", 10m, 2m, 101));
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() =>
            CreateService(second).RedeemCouponAsync("SAVE20", "coupon-user-102", 10m, 2m, 102));
        Assert.Empty(second.ChangeTracker.Entries());
        await second.SaveChangesAsync();

        using var replay = new ApplicationDbContext(options);
        Assert.Equal(1, (await replay.Coupons.SingleAsync()).CurrentUsages);
        var redemption = Assert.Single(await replay.CouponRedemptions.ToListAsync());
        Assert.Equal(101, redemption.PaymentTransactionId);
        Assert.True(await CreateService(replay).RedeemCouponAsync("SAVE20", "coupon-user-101", 10m, 2m, 101));
        Assert.False(await CreateService(replay).RedeemCouponAsync("SAVE20", "coupon-user-102", 10m, 2m, 102));
        Assert.Single(await replay.CouponRedemptions.ToListAsync());
        Assert.Equal(1, (await replay.Coupons.AsNoTracking().SingleAsync()).CurrentUsages);
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
