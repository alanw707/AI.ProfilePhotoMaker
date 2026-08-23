using AI.ProfilePhotoMaker.API.Configuration;
using AI.ProfilePhotoMaker.API.Data;
using AI.ProfilePhotoMaker.API.Models;
using AI.ProfilePhotoMaker.API.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace AI.ProfilePhotoMaker.API.Tests.Services;

public class CreditPackageServiceTests
{
    private static readonly string TestStripeWebhookKey = new('W', 32);

    [Fact]
    public async Task PurchaseCreditPackageAsync_ReturnsPending_WhenTransactionIsPending()
    {
        using var context = CreateContext();
        var userId = await SeedUserProfileAsync(context);
        var creditPackage = await SeedCreditPackageAsync(context);
        var packageId = creditPackage.PackageId;
        var packagePrice = creditPackage.Price;
        var totalCredits = creditPackage.TotalCredits;

        context.PaymentTransactions.Add(new PaymentTransaction
        {
            Id = 1,
            UserId = userId,
            ExternalTransactionId = "pi_pending",
            Amount = packagePrice,
            Currency = "USD",
            PaymentProvider = "stripe",
            Status = PaymentStatus.Pending,
            Type = PaymentType.OneTime,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });

        await context.SaveChangesAsync();

        var service = CreateService(context);

        var result = await service.PurchaseCreditPackageAsync(userId, packageId, "1");

        Assert.False(result.Success);
        Assert.Equal(PaymentStatus.Pending, result.Status);
        Assert.Equal("PaymentPending", result.ErrorCode);
        Assert.Empty(await context.CreditPurchases.ToListAsync());

        var profile = await context.UserProfiles.FirstAsync(p => p.UserId == userId);
        Assert.Equal(5, profile.Credits);
    }

    [Fact]
    public async Task PurchaseCreditPackageAsync_CompletesAndAwardsCredits_WhenTransactionCompleted()
    {
        using var context = CreateContext();
        var userId = await SeedUserProfileAsync(context);
        var creditPackage = await SeedCreditPackageAsync(context);
        var packageId = creditPackage.PackageId;
        var packagePrice = creditPackage.Price;
        var totalCredits = creditPackage.TotalCredits;

        context.PaymentTransactions.Add(new PaymentTransaction
        {
            Id = 2,
            UserId = userId,
            ExternalTransactionId = "pi_success",
            Amount = packagePrice,
            Currency = "USD",
            PaymentProvider = "stripe",
            Status = PaymentStatus.Completed,
            Type = PaymentType.OneTime,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            ProcessedAt = DateTime.UtcNow
        });

        await context.SaveChangesAsync();

        var service = CreateService(context);

        var result = await service.PurchaseCreditPackageAsync(userId, packageId, "2");

        Assert.True(result.Success);
        Assert.NotNull(result.Purchase);
        Assert.Equal(PaymentStatus.Completed, result.Status);
        Assert.Equal(totalCredits, result.Purchase!.CreditsAwarded);
        Assert.Equal("2", result.Purchase.PaymentTransactionId);

        var profile = await context.UserProfiles.FirstAsync(p => p.UserId == userId);
        Assert.Equal(5 + totalCredits, profile.Credits);

        // Ensure calling again is idempotent and does not double-award credits
        var secondResult = await service.PurchaseCreditPackageAsync(userId, packageId, "2");
        Assert.True(secondResult.Success);
        Assert.NotNull(secondResult.Purchase);
        Assert.Equal(result.Purchase!.Id, secondResult.Purchase!.Id);

        profile = await context.UserProfiles.FirstAsync(p => p.UserId == userId);
        Assert.Equal(5 + totalCredits, profile.Credits);
    }

    [Fact]
    public async Task PurchaseCreditPackageAsync_ReturnsFailure_WhenTransactionMissingAndSimulationDisabled()
    {
        using var context = CreateContext();
        var userId = await SeedUserProfileAsync(context);
        var creditPackage = await SeedCreditPackageAsync(context);

        var stripeOptions = new StripeOptions
        {
            PublishableKey = new string('P', 24),
            SecretKey = new string('S', 32),
            WebhookSecret = TestStripeWebhookKey
        };

        var simulationOptions = new PaymentSimulationOptions
        {
            Enabled = false,
            SkipStripeIntegration = false
        };

        var service = CreateService(context, stripeOptions, simulationOptions);

        var result = await service.PurchaseCreditPackageAsync(userId, creditPackage.PackageId, null);

        Assert.False(result.Success);
        Assert.Equal(PaymentStatus.Failed, result.Status);
        Assert.Equal("PaymentRequired", result.ErrorCode);
        Assert.Empty(await context.CreditPurchases.ToListAsync());

        var profile = await context.UserProfiles.FirstAsync(p => p.UserId == userId);
        Assert.Equal(5, profile.Credits);
    }

    [Fact]
    public async Task PurchaseCreditPackageAsync_RejectsMissingRawPreviewBeforeApplyingPurchase()
    {
        using var context = CreateContext();
        var userId = await SeedUserProfileAsync(context);
        var creditPackage = await SeedCreditPackageAsync(context);
        var packages = new Mock<IOutcomePackageService>();
        packages.Setup(s => s.CanPromotePreviewAsync(userId, 123, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        var service = CreateService(context, outcomePackageService: packages.Object);

        var result = await service.PurchaseCreditPackageAsync(userId, creditPackage.PackageId, "2", 123);

        Assert.False(result.Success);
        Assert.Equal("PreviewUnavailable", result.ErrorCode);
        Assert.Empty(context.CreditPurchases);
        Assert.Equal(5, (await context.UserProfiles.SingleAsync(p => p.UserId == userId)).Credits);
    }

    [Fact]
    public async Task PurchaseCreditPackageAsync_AllowsSimulation_WhenStripeNotConfiguredAndSimulationEnabled()
    {
        using var context = CreateContext();
        var userId = await SeedUserProfileAsync(context);
        var creditPackage = await SeedCreditPackageAsync(context);
        context.OutcomePackageDefinitions.Add(new OutcomePackageDefinition
        {
            Code = "starter_package",
            Name = "Starter Package",
            Description = "Test package",
            InternalCreditPackageId = creditPackage.PackageId,
            IncludedCandidateCount = 3,
            IsActive = true
        });
        await context.SaveChangesAsync();

        var stripeOptions = new StripeOptions();
        var simulationOptions = new PaymentSimulationOptions
        {
            Enabled = true,
            SkipStripeIntegration = true
        };
        var outcomePackages = new OutcomePackageService(
            context,
            NullLogger<OutcomePackageService>.Instance);
        var service = CreateService(context, stripeOptions, simulationOptions, outcomePackages);

        var result = await service.PurchaseCreditPackageAsync(userId, creditPackage.PackageId, "sim_flow");

        Assert.True(result.Success);
        Assert.NotNull(result.Purchase);
        Assert.Equal(PaymentStatus.Completed, result.Status);
        Assert.Equal("sim_flow", result.Purchase!.PaymentTransactionId);

        var retried = await service.PurchaseCreditPackageAsync(userId, creditPackage.PackageId, "sim_flow");
        Assert.True(retried.Success);

        var profile = await context.UserProfiles.FirstAsync(p => p.UserId == userId);
        Assert.Equal(5 + creditPackage.TotalCredits, profile.Credits);
        Assert.Single(context.UserPackageEntitlements);
    }

    private static CreditPackageService CreateService(
        ApplicationDbContext context,
        StripeOptions? stripeOptions = null,
        PaymentSimulationOptions? simulationOptions = null,
        IOutcomePackageService? outcomePackageService = null)
    {
        stripeOptions ??= new StripeOptions
        {
            PublishableKey = "pk_test_default",
            SecretKey = "sk_test_default",
            WebhookSecret = "whsec_default"
        };

        simulationOptions ??= new PaymentSimulationOptions
        {
            Enabled = false,
            SkipStripeIntegration = false
        };

        var basicTierService = new BasicTierService(context, NullLogger<BasicTierService>.Instance);
        return new CreditPackageService(
            context,
            basicTierService,
            NullLogger<CreditPackageService>.Instance,
            Options.Create(stripeOptions),
            Options.Create(simulationOptions),
            outcomePackageService: outcomePackageService);
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var context = new ApplicationDbContext(options);
        context.Database.EnsureCreated();
        return context;
    }

    private static async Task<string> SeedUserProfileAsync(ApplicationDbContext context)
    {
        var userId = Guid.NewGuid().ToString();
        var user = new ApplicationUser
        {
            Id = userId,
            UserName = "test-user@example.com",
            Email = "test-user@example.com"
        };

        context.Users.Add(user);
        context.UserProfiles.Add(new UserProfile
        {
            UserId = userId,
            User = user,
            SubscriptionTier = SubscriptionTier.Basic,
            Credits = 5,
            LastCreditReset = DateTime.UtcNow
        });

        await context.SaveChangesAsync();
        return userId;
    }

    private static async Task<(int PackageId, decimal Price, int TotalCredits)> SeedCreditPackageAsync(ApplicationDbContext context)
    {
        var package = new CreditPackage
        {
            Name = $"Test Package {Guid.NewGuid():N}",
            Credits = 10,
            BonusCredits = 2,
            Price = 9.99m,
            Description = "Test package",
            DisplayOrder = 99,
            IsActive = true
        };

        context.CreditPackages.Add(package);
        await context.SaveChangesAsync();

        return (package.Id, package.Price, package.Credits + package.BonusCredits);
    }
}
