using AI.ProfilePhotoMaker.API.Configuration;
using AI.ProfilePhotoMaker.API.Data;
using AI.ProfilePhotoMaker.API.Models;
using AI.ProfilePhotoMaker.API.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;
using Stripe;
using System.Net;
using System.Text;
using System.Text.Json;

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
    public async Task PurchaseCreditPackageAsync_AllowsSimulation_WhenStripeNotConfiguredAndSimulationEnabled()
    {
        using var context = CreateContext();
        var userId = await SeedUserProfileAsync(context);
        var creditPackage = await SeedCreditPackageAsync(context);

        var stripeOptions = new StripeOptions();
        var simulationOptions = new PaymentSimulationOptions
        {
            Enabled = true,
            SkipStripeIntegration = true
        };

        var service = CreateService(context, stripeOptions, simulationOptions);

        var result = await service.PurchaseCreditPackageAsync(userId, creditPackage.PackageId, "sim_flow");

        Assert.True(result.Success);
        Assert.NotNull(result.Purchase);
        Assert.Equal(PaymentStatus.Completed, result.Status);
        Assert.Equal("sim_flow", result.Purchase!.PaymentTransactionId);

        var profile = await context.UserProfiles.FirstAsync(p => p.UserId == userId);
        Assert.Equal(5 + creditPackage.TotalCredits, profile.Credits);
    }

    [Fact]
    public async Task PurchaseCreditPackageAsync_DoesNotExposeAnotherUsersExistingPurchase()
    {
        using var context = CreateContext();
        var owner = await SeedUserProfileAsync(context);
        var package = await SeedCreditPackageAsync(context);
        var service = CreateService(context, new StripeOptions(), new PaymentSimulationOptions { Enabled = true, SkipStripeIntegration = true });
        var purchase = await service.PurchaseCreditPackageAsync(owner, package.PackageId, "sim-owner");
        Assert.True(purchase.Success);

        var result = await service.PurchaseCreditPackageAsync("another-user", package.PackageId, "sim-owner");

        Assert.False(result.Success);
        Assert.Null(result.Purchase);
        Assert.Equal("TransactionMismatch", result.ErrorCode);
    }

    [Fact]
    public async Task PurchaseCreditPackageAsync_DoesNotFulfillPendingReviewTransaction()
    {
        using var context = CreateContext();
        var userId = await SeedUserProfileAsync(context);
        var package = await SeedCreditPackageAsync(context);
        context.PaymentTransactions.Add(new PaymentTransaction
        {
            Id = 71, UserId = userId, ExternalTransactionId = "pi_review",
            Amount = package.Price, Status = PaymentStatus.PendingReview, Type = PaymentType.OneTime
        });
        await context.SaveChangesAsync();

        var result = await CreateService(context).PurchaseCreditPackageAsync(userId, package.PackageId, "71");

        Assert.False(result.Success);
        Assert.Equal(PaymentStatus.PendingReview, result.Status);
        Assert.Empty(await context.CreditPurchases.ToListAsync());
        Assert.Equal(5, (await context.UserProfiles.SingleAsync(p => p.UserId == userId)).Credits);
    }

    [Theory]
    [InlineData("matching", true)]
    [InlineData("package", false)]
    [InlineData("user", false)]
    [InlineData("amount", false)]
    [InlineData("currency", false)]
    public async Task FirstFulfillment_MustMatchVerifiedStripeIntent(string mismatch, bool expectedSuccess)
    {
        using var context = CreateContext();
        var userId = await SeedUserProfileAsync(context);
        var package = await SeedCreditPackageAsync(context);
        context.PaymentTransactions.Add(new PaymentTransaction
        {
            Id = 81, UserId = userId, ExternalTransactionId = "pi_bound",
            Amount = package.Price, Currency = "USD", Status = PaymentStatus.Completed, Type = PaymentType.OneTime
        });
        await context.SaveChangesAsync();
        var amount = (long)(package.Price * 100);
        var json = JsonSerializer.Serialize(new
        {
            id = "pi_bound", @object = "payment_intent", status = "succeeded",
            amount = mismatch == "amount" ? amount - 1 : amount,
            amount_received = amount,
            currency = mismatch == "currency" ? "eur" : "usd",
            metadata = new { user_id = mismatch == "user" ? "another-user" : userId,
                package_id = mismatch == "package" ? "999999" : package.PackageId.ToString() }
        });
        using var http = new HttpClient(new IntentResponseHandler(json));
        var stripe = new StripeClient("sk_test_unit", httpClient: new SystemNetHttpClient(http));
        var service = CreateService(context, stripeClient: stripe);

        var result = await service.PurchaseCreditPackageAsync(userId, package.PackageId, "81");

        Assert.Equal(expectedSuccess, result.Success);
        Assert.Equal(expectedSuccess ? 1 : 0, await context.CreditPurchases.CountAsync());
        Assert.Equal(expectedSuccess ? 5 + package.TotalCredits : 5,
            (await context.UserProfiles.SingleAsync(p => p.UserId == userId)).Credits);
    }

    private sealed class IntentResponseHandler(string json) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            });
    }

    private static CreditPackageService CreateService(
        ApplicationDbContext context,
        StripeOptions? stripeOptions = null,
        PaymentSimulationOptions? simulationOptions = null,
        StripeClient? stripeClient = null)
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
            stripeClient);
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
