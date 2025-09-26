using System;
using System.Collections.Generic;
using System.Linq;
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
    public async Task HandleEventAsync_PaymentIntentFailed_UsesStripeErrorMessageWhenPresent()
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

        if (stripeEvent.Data.Object is PaymentIntent paymentIntent)
        {
            paymentIntent.LastPaymentError = new StripeError
            {
                Message = "Your card was declined"
            };
        }

        await service.HandleEventAsync(stripeEvent);

        var updatedTransaction = await context.PaymentTransactions
            .AsNoTracking()
            .FirstAsync(t => t.Id == transaction.Id);

        Assert.Equal(PaymentStatus.Failed, updatedTransaction.Status);
        Assert.Equal("Your card was declined", updatedTransaction.FailureReason);
        Assert.NotNull(updatedTransaction.ProcessedAt);
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

    [Fact]
    public async Task HandleEventAsync_PaymentIntentSucceeded_MissingUserMetadata_DoesNotProcess()
    {
        using var context = CreateContext();
        var (userId, package, transaction) = await SeedSuccessfulScenarioAsync(context);

        var service = CreateService(context);

        var metadata = new Dictionary<string, string>
        {
            ["package_id"] = package.Id.ToString(),
            ["payment_transaction_id"] = transaction.Id.ToString()
        };

        var stripeEvent = CreateStripeEvent(
            PaymentIntentSucceededEvent,
            transaction.ExternalTransactionId,
            metadata);

        await service.HandleEventAsync(stripeEvent);

        var refreshedTransaction = await context.PaymentTransactions
            .AsNoTracking()
            .FirstAsync(t => t.Id == transaction.Id);

        Assert.Equal(PaymentStatus.Pending, refreshedTransaction.Status);
        Assert.Null(refreshedTransaction.ProcessedAt);
        Assert.False(await context.CreditPurchases.AnyAsync());
    }

    [Fact]
    public async Task HandleEventAsync_PaymentIntentSucceeded_MissingPackageMetadata_DoesNotProcess()
    {
        using var context = CreateContext();
        var (userId, package, transaction) = await SeedSuccessfulScenarioAsync(context);

        var service = CreateService(context);

        var metadata = new Dictionary<string, string>
        {
            ["user_id"] = userId,
            ["payment_transaction_id"] = transaction.Id.ToString()
        };

        var stripeEvent = CreateStripeEvent(
            PaymentIntentSucceededEvent,
            transaction.ExternalTransactionId,
            metadata);

        await service.HandleEventAsync(stripeEvent);

        var refreshedTransaction = await context.PaymentTransactions
            .AsNoTracking()
            .FirstAsync(t => t.Id == transaction.Id);

        Assert.Equal(PaymentStatus.Pending, refreshedTransaction.Status);
        Assert.Null(refreshedTransaction.ProcessedAt);
        Assert.False(await context.CreditPurchases.AnyAsync());
    }

    [Fact]
    public async Task HandleEventAsync_PaymentIntentSucceeded_ReprocessingDoesNotDuplicateCredits()
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

        var profileAfterFirst = await context.UserProfiles
            .AsNoTracking()
            .FirstAsync(p => p.UserId == userId);

        Assert.Equal(package.TotalCredits, profileAfterFirst.PurchasedCredits);

        await service.HandleEventAsync(stripeEvent);

        var profileAfterSecond = await context.UserProfiles
            .AsNoTracking()
            .FirstAsync(p => p.UserId == userId);

        Assert.Equal(package.TotalCredits, profileAfterSecond.PurchasedCredits);

        var purchases = await context.CreditPurchases
            .AsNoTracking()
            .Where(p => p.UserId == userId)
            .ToListAsync();

        Assert.Single(purchases);
    }

    [Fact]
    public async Task HandleEventAsync_PaymentIntentSucceeded_WithMissingTransaction_DoesNotAwardCredits()
    {
        using var context = CreateContext();
        var (userId, package, transaction) = await SeedSuccessfulScenarioAsync(context);

        context.PaymentTransactions.Remove(transaction);
        await context.SaveChangesAsync();

        var service = CreateService(context);

        var stripeEvent = CreateStripeEvent(
            PaymentIntentSucceededEvent,
            transaction.ExternalTransactionId,
            userId,
            package.Id,
            transaction.Id);

        await service.HandleEventAsync(stripeEvent);

        var profile = await context.UserProfiles
            .AsNoTracking()
            .FirstAsync(p => p.UserId == userId);

        Assert.Equal(0, profile.PurchasedCredits);
        Assert.False(await context.CreditPurchases.AnyAsync());
    }

    [Fact]
    public async Task HandleEventAsync_PaymentIntentSucceeded_MismatchedUserMetadata_UsesTransactionUser()
    {
        using var context = CreateContext();
        var (userId, package, transaction) = await SeedSuccessfulScenarioAsync(context);

        var service = CreateService(context);
        var mismatchedUserId = Guid.NewGuid().ToString();

        var metadata = new Dictionary<string, string>
        {
            ["user_id"] = mismatchedUserId,
            ["package_id"] = package.Id.ToString(),
            ["payment_transaction_id"] = transaction.Id.ToString()
        };

        var stripeEvent = CreateStripeEvent(
            PaymentIntentSucceededEvent,
            transaction.ExternalTransactionId,
            metadata);

        await service.HandleEventAsync(stripeEvent);

        var profile = await context.UserProfiles
            .AsNoTracking()
            .FirstAsync(p => p.UserId == userId);

        Assert.Equal(package.TotalCredits, profile.PurchasedCredits);
        Assert.False(await context.UserProfiles.AnyAsync(p => p.UserId == mismatchedUserId));
    }

    [Fact]
    public async Task HandleEventAsync_UnknownEventType_Ignored()
    {
        using var context = CreateContext();
        var (userId, package, transaction) = await SeedSuccessfulScenarioAsync(context);

        var service = CreateService(context);

        var stripeEvent = CreateStripeEvent(
            "charge.refunded",
            transaction.ExternalTransactionId,
            userId,
            package.Id,
            transaction.Id);

        await service.HandleEventAsync(stripeEvent);

        var refreshedTransaction = await context.PaymentTransactions
            .AsNoTracking()
            .FirstAsync(t => t.Id == transaction.Id);

        Assert.Equal(PaymentStatus.Pending, refreshedTransaction.Status);
        Assert.Null(refreshedTransaction.ProcessedAt);
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

        return CreateStripeEvent(eventType, paymentIntentId, metadata);
    }

    private static Event CreateStripeEvent(
        string eventType,
        string paymentIntentId,
        Dictionary<string, string>? metadata)
    {
        var paymentIntent = new PaymentIntent
        {
            Id = paymentIntentId,
            Metadata = metadata != null
                ? new Dictionary<string, string>(metadata)
                : new Dictionary<string, string>(),
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
