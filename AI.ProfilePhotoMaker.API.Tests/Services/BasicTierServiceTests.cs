using AI.ProfilePhotoMaker.API.Data;
using AI.ProfilePhotoMaker.API.Models;
using AI.ProfilePhotoMaker.API.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace AI.ProfilePhotoMaker.API.Tests.Services;

public class BasicTierServiceTests
{
    [Fact]
    public async Task ConsumeCreditsAsync_ReturnsBreakdownAcrossWeeklyAndPurchasedSources()
    {
        using var context = CreateContext();
        var userId = await SeedUserProfileAsync(context, weeklyCredits: 4, purchasedCredits: 5);
        var service = CreateService(context);

        var result = await service.ConsumeCreditsAsync(userId, 5, "photo_enhancement");

        Assert.True(result.Success);
        Assert.Equal(4, result.WeeklyCreditsConsumed);
        Assert.Equal(1, result.PurchasedCreditsConsumed);

        var profile = await context.UserProfiles.FirstAsync(p => p.UserId == userId);
        Assert.Equal(0, profile.Credits);
        Assert.Equal(4, profile.PurchasedCredits);
    }

    [Fact]
    public async Task RefundCreditsAsync_RestoresPurchasedCreditsWhenWeeklyUnavailable()
    {
        using var context = CreateContext();
        var userId = await SeedUserProfileAsync(context, weeklyCredits: 0, purchasedCredits: 5);
        var service = CreateService(context);

        var consumption = await service.ConsumeCreditsAsync(userId, 2, "photo_enhancement");
        var profileAfterConsumption = await context.UserProfiles.FirstAsync(p => p.UserId == userId);
        Assert.Equal(0, profileAfterConsumption.Credits);
        Assert.Equal(3, profileAfterConsumption.PurchasedCredits);

        var refunded = await service.RefundCreditsAsync(userId, consumption);
        Assert.True(refunded);

        var profile = await context.UserProfiles.FirstAsync(p => p.UserId == userId);
        Assert.Equal(0, profile.Credits);
        Assert.Equal(5, profile.PurchasedCredits);
    }

    [Fact]
    public async Task RefundCreditsAsync_ReroutesWeeklyRefundWhenAllowanceAlreadyReset()
    {
        using var context = CreateContext();
        var startingWeeklyCredits = 5;
        var userId = await SeedUserProfileAsync(context, weeklyCredits: startingWeeklyCredits, purchasedCredits: 0);
        var service = CreateService(context);

        var consumption = await service.ConsumeCreditsAsync(userId, 2, "photo_enhancement");
        Assert.True(consumption.Success);

        // Simulate an out-of-band weekly reset that already restored the free allowance
        var profile = await context.UserProfiles.FirstAsync(p => p.UserId == userId);
        profile.Credits = startingWeeklyCredits;
        await context.SaveChangesAsync();

        var refunded = await service.RefundCreditsAsync(userId, consumption);
        Assert.True(refunded);

        profile = await context.UserProfiles.FirstAsync(p => p.UserId == userId);
        Assert.Equal(startingWeeklyCredits, profile.Credits); // stays capped
        Assert.Equal(consumption.WeeklyCreditsConsumed, profile.PurchasedCredits); // rollover goes to purchased bucket
    }

    [Fact]
    public async Task ConsumeCreditsAsync_RejectsNonPositiveCosts()
    {
        using var context = CreateContext();
        var userId = await SeedUserProfileAsync(context, weeklyCredits: 3, purchasedCredits: 2);
        var service = CreateService(context);

        var zeroCost = await service.ConsumeCreditsAsync(userId, 0, "styled_generation");
        Assert.False(zeroCost.Success);
        Assert.Equal("invalid_credit_cost", zeroCost.Error);

        var negativeCost = await service.ConsumeCreditsAsync(userId, -5, "styled_generation");
        Assert.False(negativeCost.Success);
        Assert.Equal("invalid_credit_cost", negativeCost.Error);

        var profile = await context.UserProfiles.FirstAsync(p => p.UserId == userId);
        Assert.Equal(3, profile.Credits);
        Assert.Equal(2, profile.PurchasedCredits);
    }

    [Fact]
    public async Task RefundCreditsAsync_SkipsWhenChargeLogMissingWithCorrelationId()
    {
        using var context = CreateContext();
        var userId = await SeedUserProfileAsync(context, weeklyCredits: 2, purchasedCredits: 1);
        var service = CreateService(context);

        var correlationId = $"photo_enhancement:{Guid.NewGuid()}";
        var consumption = CreditConsumptionResult.Succeeded("photo_enhancement", weeklyCredits: 1, purchasedCredits: 0, correlationId: correlationId);

        var refunded = await service.RefundCreditsAsync(userId, consumption);
        Assert.True(refunded);

        var profile = await context.UserProfiles.FirstAsync(p => p.UserId == userId);
        Assert.Equal(2, profile.Credits);
        Assert.Equal(1, profile.PurchasedCredits);
    }

    [Fact]
    public async Task RefundCreditsAsync_RefundsWhenChargeLogPresentWithCorrelationId()
    {
        using var context = CreateContext();
        var userId = await SeedUserProfileAsync(context, weeklyCredits: 2, purchasedCredits: 2);
        var service = CreateService(context);

        var correlationId = $"photo_enhancement:{Guid.NewGuid()}";
        var consumption = await service.ConsumeCreditsAsync(userId, 2, "photo_enhancement", correlationId);
        Assert.True(consumption.Success);

        var refunded = await service.RefundCreditsAsync(userId, consumption);
        Assert.True(refunded);

        var profile = await context.UserProfiles.FirstAsync(p => p.UserId == userId);
        Assert.Equal(2, profile.Credits);
        Assert.Equal(2, profile.PurchasedCredits);

        var refundLog = await context.UsageLogs
            .FirstOrDefaultAsync(l => l.UserId == userId
                                      && l.Action == "photo_enhancement_refund"
                                      && l.Details != null
                                      && l.Details.Contains($"correlationId={correlationId}"));
        Assert.NotNull(refundLog);
    }

    [Fact]
    public async Task RefundCreditsAsync_SkipsDuplicateRefundWithCorrelationId()
    {
        using var context = CreateContext();
        var userId = await SeedUserProfileAsync(context, weeklyCredits: 2, purchasedCredits: 0);
        var service = CreateService(context);

        var correlationId = $"photo_enhancement:{Guid.NewGuid()}";
        var consumption = await service.ConsumeCreditsAsync(userId, 2, "photo_enhancement", correlationId);
        Assert.True(consumption.Success);

        var firstRefund = await service.RefundCreditsAsync(userId, consumption);
        Assert.True(firstRefund);

        var secondRefund = await service.RefundCreditsAsync(userId, consumption);
        Assert.True(secondRefund);

        var profile = await context.UserProfiles.FirstAsync(p => p.UserId == userId);
        Assert.Equal(2, profile.Credits);
        Assert.Equal(0, profile.PurchasedCredits);

        var refundLogs = await context.UsageLogs
            .Where(l => l.UserId == userId
                        && l.Action == "photo_enhancement_refund"
                        && l.Details != null
                        && l.Details.Contains($"correlationId={correlationId}"))
            .ToListAsync();

        Assert.Single(refundLogs);
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(options);
    }

    private static BasicTierService CreateService(ApplicationDbContext context)
    {
        return new BasicTierService(context, NullLogger<BasicTierService>.Instance);
    }

    private static async Task<string> SeedUserProfileAsync(ApplicationDbContext context, int weeklyCredits, int purchasedCredits)
    {
        var userId = Guid.NewGuid().ToString();
        var user = new ApplicationUser
        {
            Id = userId,
            UserName = $"user_{userId}",
            NormalizedUserName = $"USER_{userId}",
            Email = $"{userId}@example.com",
            NormalizedEmail = $"{userId}@EXAMPLE.COM"
        };

        context.Users.Add(user);
        context.UserProfiles.Add(new UserProfile
        {
            UserId = userId,
            User = user,
            SubscriptionTier = SubscriptionTier.Basic,
            Credits = weeklyCredits,
            PurchasedCredits = purchasedCredits,
            LastCreditReset = DateTime.UtcNow.AddDays(-1),
            CreatedAt = DateTime.UtcNow.AddMinutes(-10),
            UpdatedAt = DateTime.UtcNow.AddMinutes(-10)
        });

        await context.SaveChangesAsync();
        return userId;
    }
}
