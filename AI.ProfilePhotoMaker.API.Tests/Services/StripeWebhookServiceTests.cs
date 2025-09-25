using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AI.ProfilePhotoMaker.API.Data;
using AI.ProfilePhotoMaker.API.Models;
using AI.ProfilePhotoMaker.API.Services;
using AI.ProfilePhotoMaker.API.Services.Payments;
using AI.ProfilePhotoMaker.API.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Stripe;
using Xunit;

namespace AI.ProfilePhotoMaker.API.Tests.Services;

public class StripeWebhookServiceTests
{
    private const string PaymentIntentSucceededEvent = "payment_intent.succeeded";
    private const string PaymentIntentFailedEvent = "payment_intent.payment_failed";
    private const string PaymentIntentCancelledEvent = "payment_intent.canceled";

    [Fact]
    public async Task HandleEventAsync_PaymentIntentSucceeded_CompletesTransactionAndAwardsCredits()
    {
        using var context = CreateContext();
        var (userId, package, transaction) = await SeedSuccessfulScenarioAsync(context);

        var service = CreateService(context);

        var stripeEvent = CreateStripeEvent(
            PaymentIntentSucceededEvent,
            transaction.ExternalTransactionId,
            userId,
            package.Id,
            transaction.Id);

        await service.HandleEventAsync(stripeEvent);

        var updatedTransaction = await context.PaymentTransactions
            .AsNoTracking()
            .FirstAsync(t => t.Id == transaction.Id);

        Assert.Equal(PaymentStatus.Completed, updatedTransaction.Status);
        Assert.NotNull(updatedTransaction.ProcessedAt);

        var purchase = await context.CreditPurchases
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.PaymentTransactionId == transaction.Id.ToString());

        Assert.NotNull(purchase);
        Assert.Equal(package.Id, purchase!.PackageId);
        Assert.Equal(PaymentStatus.Completed, purchase.Status);
        Assert.Equal(package.TotalCredits, purchase.CreditsAwarded);

        var profile = await context.UserProfiles
            .AsNoTracking()
            .FirstAsync(p => p.UserId == userId);

        Assert.Equal(package.TotalCredits, profile.PurchasedCredits);
    }

    [Fact]
    public async Task HandleEventAsync_PaymentIntentFailed_MarksTransactionFailed()
    {
        using var context = CreateContext();
        var (userId, package, transaction) = await SeedSuccessfulScenarioAsync(context);

        var service = CreateService(context);

        var stripeEvent = CreateStripeEvent(
            PaymentIntentFailedEvent,
            transaction.ExternalTransactionId,
            userId,
            package.Id,
            transaction.Id);

        await service.HandleEventAsync(stripeEvent);

        var updatedTransaction = await context.PaymentTransactions
            .AsNoTracking()
            .FirstAsync(t => t.Id == transaction.Id);

        Assert.Equal(PaymentStatus.Failed, updatedTransaction.Status);
        Assert.Equal("Payment failed", updatedTransaction.FailureReason);
        Assert.NotNull(updatedTransaction.ProcessedAt);

        Assert.False(await context.CreditPurchases.AnyAsync());
    }

    [Fact]
    public async Task HandleEventAsync_PaymentIntentCanceled_MarksTransactionCancelled()
    {
        using var context = CreateContext();
        var (userId, package, transaction) = await SeedSuccessfulScenarioAsync(context);

        var service = CreateService(context);

        var stripeEvent = CreateStripeEvent(
            PaymentIntentCancelledEvent,
            transaction.ExternalTransactionId,
            userId,
            package.Id,
            transaction.Id);

        await service.HandleEventAsync(stripeEvent);

        var updatedTransaction = await context.PaymentTransactions
            .AsNoTracking()
            .FirstAsync(t => t.Id == transaction.Id);

        Assert.Equal(PaymentStatus.Cancelled, updatedTransaction.Status);
        Assert.Equal("Payment cancelled", updatedTransaction.FailureReason);
        Assert.NotNull(updatedTransaction.ProcessedAt);

        Assert.False(await context.CreditPurchases.AnyAsync());
    }

    private static StripeWebhookService CreateService(ApplicationDbContext context)
    {
        var basicTierService = new BasicTierService(context, NullLogger<BasicTierService>.Instance);

        var stripeOptions = new StripeOptions
        {
            PublishableKey = "pk_test_default",
            SecretKey = "sk_test_default",
            WebhookSecret = "whsec_default"
        };

        var simulationOptions = new PaymentSimulationOptions
        {
            Enabled = false,
            SkipStripeIntegration = false
        };

        var creditPackageService = new CreditPackageService(
            context,
            basicTierService,
            NullLogger<CreditPackageService>.Instance,
            Options.Create(stripeOptions),
            Options.Create(simulationOptions));

        return new StripeWebhookService(context, creditPackageService, NullLogger<StripeWebhookService>.Instance);
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

    private static async Task<(string userId, CreditPackage package, PaymentTransaction transaction)> SeedSuccessfulScenarioAsync(ApplicationDbContext context)
    {
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid().ToString(),
            UserName = "test-user@example.com",
            Email = "test-user@example.com"
        };

        context.Users.Add(user);

        var profile = new UserProfile
        {
            UserId = user.Id,
            User = user,
            SubscriptionTier = SubscriptionTier.Basic,
            Credits = 5,
            PurchasedCredits = 0,
            LastCreditReset = DateTime.UtcNow
        };

        context.UserProfiles.Add(profile);

        var package = new CreditPackage
        {
            Name = "Starter Pack",
            Credits = 10,
            BonusCredits = 5,
            Price = 9.99m,
            Description = "Test package",
            DisplayOrder = 1,
            IsActive = true
        };

        context.CreditPackages.Add(package);
        await context.SaveChangesAsync();

        var transaction = new PaymentTransaction
        {
            UserId = user.Id,
            User = user,
            ExternalTransactionId = $"pi_{Guid.NewGuid():N}",
            Amount = package.Price,
            Currency = "usd",
            PaymentProvider = "stripe",
            Status = PaymentStatus.Pending,
            Type = PaymentType.OneTime,
            Description = "Credit package purchase",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        context.PaymentTransactions.Add(transaction);
        await context.SaveChangesAsync();

        return (user.Id, package, transaction);
    }

    private static Event CreateStripeEvent(
        string eventType,
        string paymentIntentId,
        string userId,
        int packageId,
        int transactionId)
    {
        var metadata = new Dictionary<string, string>
        {
            ["user_id"] = userId,
            ["package_id"] = packageId.ToString(),
            ["payment_transaction_id"] = transactionId.ToString()
        };

        var paymentIntent = new PaymentIntent
        {
            Id = paymentIntentId,
            Metadata = new Dictionary<string, string>(metadata),
            Currency = "usd"
        };

        return new Event
        {
            Id = $"evt_{Guid.NewGuid():N}",
            Type = eventType,
            Data = new EventData
            {
                Object = paymentIntent
            }
        };
    }
}
