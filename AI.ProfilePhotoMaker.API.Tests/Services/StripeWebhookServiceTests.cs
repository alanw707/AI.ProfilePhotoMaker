using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AI.ProfilePhotoMaker.API.Data;
using AI.ProfilePhotoMaker.API.Models;
using AI.ProfilePhotoMaker.API.Services;
using AI.ProfilePhotoMaker.API.Services.Payments;
using AI.ProfilePhotoMaker.API.Configuration;
using AI.ProfilePhotoMaker.API.Services.Notifications;
using Microsoft.Data.Sqlite;
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

        Assert.Equal(5 + package.TotalCredits, profile.Credits);
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
    public async Task HandleEventAsync_PaymentIntentFailed_MissingTransactionMetadata_ResolvesByExternalId()
    {
        using var context = CreateContext();
        var (userId, package, transaction) = await SeedSuccessfulScenarioAsync(context);

        var service = CreateService(context);

        var metadata = new Dictionary<string, string>
        {
            ["user_id"] = userId,
            ["package_id"] = package.Id.ToString()
        };

        var stripeEvent = CreateStripeEvent(
            PaymentIntentFailedEvent,
            transaction.ExternalTransactionId,
            metadata);

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


    [Fact]
    public async Task HandleEventAsync_PaymentIntentCanceled_MissingTransactionMetadata_ResolvesByExternalId()
    {
        using var context = CreateContext();
        var (userId, package, transaction) = await SeedSuccessfulScenarioAsync(context);

        var service = CreateService(context);

        var metadata = new Dictionary<string, string>
        {
            ["user_id"] = userId,
            ["package_id"] = package.Id.ToString()
        };

        var stripeEvent = CreateStripeEvent(
            PaymentIntentCancelledEvent,
            transaction.ExternalTransactionId,
            metadata);

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
    public async Task HandleEventAsync_PaymentIntentSucceeded_MissingTransactionMetadata_ResolvesByExternalId()
    {
        using var context = CreateContext();
        var (userId, package, transaction) = await SeedSuccessfulScenarioAsync(context);

        var service = CreateService(context);

        var metadata = new Dictionary<string, string>
        {
            ["user_id"] = userId,
            ["package_id"] = package.Id.ToString()
        };

        var stripeEvent = CreateStripeEvent(
            PaymentIntentSucceededEvent,
            transaction.ExternalTransactionId,
            metadata);

        await service.HandleEventAsync(stripeEvent);

        var updatedTransaction = await context.PaymentTransactions
            .AsNoTracking()
            .FirstAsync(t => t.Id == transaction.Id);

        Assert.Equal(PaymentStatus.Completed, updatedTransaction.Status);
        Assert.NotNull(updatedTransaction.ProcessedAt);
        Assert.Null(updatedTransaction.FailureReason);

        var purchase = await context.CreditPurchases
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.PaymentTransactionId == transaction.Id.ToString());

        Assert.NotNull(purchase);
        Assert.Equal(package.TotalCredits, purchase!.CreditsAwarded);
        Assert.Equal(PaymentStatus.Completed, purchase.Status);

        var profile = await context.UserProfiles
            .AsNoTracking()
            .FirstAsync(p => p.UserId == userId);

        Assert.Equal(5 + package.TotalCredits, profile.Credits);
    }


    [Fact]
    public async Task HandleEventAsync_PaymentIntentSucceeded_InactivePackageDoesNotAwardCredits()
    {
        using var context = CreateContext();
        var (userId, package, transaction) = await SeedSuccessfulScenarioAsync(context);

        package.IsActive = false;
        context.CreditPackages.Update(package);
        await context.SaveChangesAsync();

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

        Assert.Equal(PaymentStatus.PendingReview, updatedTransaction.Status);
        Assert.Contains("PackageNotFound", updatedTransaction.FailureReason);

        Assert.False(await context.CreditPurchases.AnyAsync());

        var profile = await context.UserProfiles
            .AsNoTracking()
            .FirstAsync(p => p.UserId == userId);

        Assert.Equal(5, profile.Credits);
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

        Assert.Equal(5 + package.TotalCredits, profileAfterFirst.Credits);

        await service.HandleEventAsync(stripeEvent);

        var profileAfterSecond = await context.UserProfiles
            .AsNoTracking()
            .FirstAsync(p => p.UserId == userId);

        Assert.Equal(5 + package.TotalCredits, profileAfterSecond.Credits);

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

        Assert.Equal(5, profile.Credits);
        Assert.False(await context.CreditPurchases.AnyAsync());
    }

    [Fact]
    public async Task HandleEventAsync_PaymentIntentSucceeded_MismatchedUserMetadata_BlocksFulfillment()
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

        Assert.Equal(5, profile.Credits);
        Assert.False(await context.UserProfiles.AnyAsync(p => p.UserId == mismatchedUserId));
        Assert.Equal(PaymentStatus.PendingReview,
            (await context.PaymentTransactions.SingleAsync(t => t.Id == transaction.Id)).Status);
    }

    [Fact]
    public async Task HandleEventAsync_PaymentVerificationOutage_ThrowsForStripeRetry()
    {
        using var context = CreateContext();
        var (userId, package, transaction) = await SeedSuccessfulScenarioAsync(context);
        using var http = new HttpClient(new UnavailableStripeHandler());
        var stripe = new StripeClient("sk_test_unit", httpClient: new SystemNetHttpClient(http));
        var service = CreateService(context, stripe);
        var stripeEvent = CreateStripeEvent(
            PaymentIntentSucceededEvent,
            transaction.ExternalTransactionId,
            userId,
            package.Id,
            transaction.Id);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.HandleEventAsync(stripeEvent));

        Assert.Empty(await context.CreditPurchases.ToListAsync());
        var failedOperation = await context.StripeWebhookOperations.SingleAsync();
        Assert.Equal(StripeWebhookOperationStatus.Failed, failedOperation.Status);
        Assert.Equal(1, failedOperation.AttemptCount);

        await CreateService(context).HandleEventAsync(stripeEvent);

        Assert.Single(await context.CreditPurchases.ToListAsync());
        var completedOperation = await context.StripeWebhookOperations.SingleAsync();
        Assert.Equal(StripeWebhookOperationStatus.Succeeded, completedOperation.Status);
        Assert.Equal(2, completedOperation.AttemptCount);
    }

    [Fact]
    public async Task HandleEventAsync_ConcurrentDiscountedReplayAcrossContextsFulfillsExactlyOnce()
    {
        var databaseName = $"stripe-{Guid.NewGuid():N}";
        var connectionString = $"Data Source={databaseName};Mode=Memory;Cache=Shared;Default Timeout=30";
        await using var keeper = new SqliteConnection(connectionString);
        await keeper.OpenAsync();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connectionString)
            .Options;

        string userId;
        PaymentTransaction transaction;
        CreditPackage package;
        await using (var setup = new ApplicationDbContext(options))
        {
            await setup.Database.EnsureCreatedAsync();
            package = await setup.CreditPackages.SingleAsync(p => p.Id == 1);
            var user = new ApplicationUser
            {
                Id = Guid.NewGuid().ToString(),
                UserName = "stripe-concurrency@example.com",
                Email = "stripe-concurrency@example.com"
            };
            userId = user.Id;
            setup.Users.Add(user);
            setup.UserProfiles.Add(new UserProfile
            {
                UserId = userId,
                User = user,
                SubscriptionTier = SubscriptionTier.Basic,
                Credits = 5,
                LastCreditReset = DateTime.UtcNow
            });
            setup.Coupons.Add(new AI.ProfilePhotoMaker.API.Models.Coupon
            {
                Code = "CONCURRENT20",
                DiscountType = DiscountType.FixedAmount,
                DiscountValue = 2m,
                MaxUsages = 10,
                IsActive = true,
                CreatedByAdminId = "admin-test",
                CreatedAt = DateTime.UtcNow
            });
            transaction = new PaymentTransaction
            {
                UserId = userId,
                User = user,
                ExternalTransactionId = $"pi_{Guid.NewGuid():N}",
                Amount = 7.99m,
                Currency = "usd",
                PaymentProvider = "stripe",
                Status = PaymentStatus.Pending,
                Type = PaymentType.OneTime,
                Description = "Discounted package purchase",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            setup.PaymentTransactions.Add(transaction);
            await setup.SaveChangesAsync();
        }

        var metadata = new Dictionary<string, string>
        {
            ["user_id"] = userId,
            ["package_id"] = package.Id.ToString(),
            ["payment_transaction_id"] = transaction.Id.ToString(),
            ["coupon_code"] = "CONCURRENT20",
            ["original_price"] = "9.99",
            ["discount_amount"] = "2.00"
        };
        var stripeEvent = CreateStripeEvent(PaymentIntentSucceededEvent, transaction.ExternalTransactionId, metadata);

        await using var firstContext = new ApplicationDbContext(options);
        var blockingCoupon = new BlockingCouponService(new AI.ProfilePhotoMaker.API.Services.CouponService(firstContext, NullLogger<AI.ProfilePhotoMaker.API.Services.CouponService>.Instance));
        var first = CreateRealService(firstContext, blockingCoupon).HandleEventAsync(stripeEvent);
        await blockingCoupon.Started.WaitAsync(TimeSpan.FromSeconds(10));

        await using (var secondContext = new ApplicationDbContext(options))
        {
            var duplicateTask = CreateRealService(
                    secondContext,
                    new AI.ProfilePhotoMaker.API.Services.CouponService(secondContext, NullLogger<AI.ProfilePhotoMaker.API.Services.CouponService>.Instance))
                .HandleEventAsync(stripeEvent);
            await Task.WhenAny(duplicateTask, Task.Delay(TimeSpan.FromSeconds(2)));
            blockingCoupon.Release();
            await Assert.ThrowsAsync<InvalidOperationException>(() => duplicateTask);
        }
        await first;

        await using (var replayContext = new ApplicationDbContext(options))
        {
            await CreateRealService(
                    replayContext,
                    new AI.ProfilePhotoMaker.API.Services.CouponService(replayContext, NullLogger<AI.ProfilePhotoMaker.API.Services.CouponService>.Instance))
                .HandleEventAsync(stripeEvent);
        }

        await using var verify = new ApplicationDbContext(options);
        Assert.Equal(5 + package.TotalCredits, (await verify.UserProfiles.SingleAsync(p => p.UserId == userId)).Credits);
        Assert.Single(await verify.CouponRedemptions.ToListAsync());
        Assert.Equal(1, (await verify.Coupons.SingleAsync(c => c.Code == "CONCURRENT20")).CurrentUsages);
        Assert.Single(await verify.CreditPurchases.Where(p => p.PaymentTransactionId == transaction.Id.ToString()).ToListAsync());
        Assert.Single(await verify.UserPackageEntitlements.Where(e => e.SourcePaymentTransactionId == transaction.Id).ToListAsync());
        var operation = await verify.StripeWebhookOperations.SingleAsync();
        Assert.Equal(StripeWebhookOperationStatus.Succeeded, operation.Status);
        Assert.Equal(1, operation.AttemptCount);
    }

    [Fact]
    public async Task HandleEventAsync_TransientCouponFailureRemainsRetryableAndFulfillsOnReplay()
    {
        var databaseName = $"stripe-coupon-retry-{Guid.NewGuid():N}";
        var connectionString = $"Data Source={databaseName};Mode=Memory;Cache=Shared;Default Timeout=30";
        await using var keeper = new SqliteConnection(connectionString);
        await keeper.OpenAsync();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connectionString)
            .Options;

        string userId;
        PaymentTransaction transaction;
        CreditPackage package;
        await using (var setup = new ApplicationDbContext(options))
        {
            await setup.Database.EnsureCreatedAsync();
            (userId, package, transaction) = await SeedSqliteScenarioAsync(setup, "coupon-retry@example.com");
            setup.Coupons.Add(new AI.ProfilePhotoMaker.API.Models.Coupon
            {
                Code = "RETRY20",
                DiscountType = DiscountType.FixedAmount,
                DiscountValue = 2m,
                MaxUsages = 10,
                IsActive = true,
                CreatedByAdminId = "admin-test",
                CreatedAt = DateTime.UtcNow
            });
            await setup.SaveChangesAsync();
        }

        var metadata = new Dictionary<string, string>
        {
            ["user_id"] = userId,
            ["package_id"] = package.Id.ToString(),
            ["payment_transaction_id"] = transaction.Id.ToString(),
            ["coupon_code"] = "RETRY20",
            ["original_price"] = "9.99",
            ["discount_amount"] = "2.00"
        };
        var stripeEvent = CreateStripeEvent(PaymentIntentSucceededEvent, transaction.ExternalTransactionId, metadata);

        await using (var failedContext = new ApplicationDbContext(options))
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                CreateRealService(failedContext, new ThrowingCouponService()).HandleEventAsync(stripeEvent));
        }

        await using (var failedVerification = new ApplicationDbContext(options))
        {
            Assert.Equal(PaymentStatus.Completed,
                (await failedVerification.PaymentTransactions.SingleAsync(t => t.Id == transaction.Id)).Status);
            Assert.Empty(await failedVerification.CouponRedemptions.ToListAsync());
            Assert.Empty(await failedVerification.CreditPurchases.ToListAsync());
            var failedOperation = await failedVerification.StripeWebhookOperations.SingleAsync();
            Assert.Equal(StripeWebhookOperationStatus.Failed, failedOperation.Status);
            Assert.Equal(1, failedOperation.AttemptCount);
        }

        await using (var retryContext = new ApplicationDbContext(options))
        {
            await CreateRealService(
                    retryContext,
                    new AI.ProfilePhotoMaker.API.Services.CouponService(retryContext, NullLogger<AI.ProfilePhotoMaker.API.Services.CouponService>.Instance))
                .HandleEventAsync(stripeEvent);
        }

        await using var verification = new ApplicationDbContext(options);
        Assert.Single(await verification.CouponRedemptions.ToListAsync());
        Assert.Single(await verification.CreditPurchases.ToListAsync());
        Assert.Single(await verification.UserPackageEntitlements.ToListAsync());
        Assert.Equal(5 + package.TotalCredits,
            (await verification.UserProfiles.SingleAsync(p => p.UserId == userId)).Credits);
        var completedOperation = await verification.StripeWebhookOperations.SingleAsync();
        Assert.Equal(StripeWebhookOperationStatus.Succeeded, completedOperation.Status);
        Assert.Equal(2, completedOperation.AttemptCount);
    }

    [Fact]
    public async Task HandleEventAsync_FencesWorkerThatLosesReceiptOwnershipBeforeDiscountFulfillment()
    {
        var databaseName = $"stripe-fence-{Guid.NewGuid():N}";
        var connectionString = $"Data Source={databaseName};Mode=Memory;Cache=Shared;Default Timeout=30";
        await using var keeper = new SqliteConnection(connectionString);
        await keeper.OpenAsync();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connectionString)
            .Options;

        string userId;
        PaymentTransaction transaction;
        CreditPackage package;
        await using (var setup = new ApplicationDbContext(options))
        {
            await setup.Database.EnsureCreatedAsync();
            (userId, package, transaction) = await SeedSqliteScenarioAsync(setup, "webhook-fence@example.com");
            setup.Coupons.Add(new AI.ProfilePhotoMaker.API.Models.Coupon
            {
                Code = "FENCE20",
                DiscountType = DiscountType.FixedAmount,
                DiscountValue = 2m,
                MaxUsages = 10,
                IsActive = true,
                CreatedByAdminId = "admin-test",
                CreatedAt = DateTime.UtcNow
            });
            await setup.SaveChangesAsync();
        }

        var metadata = new Dictionary<string, string>
        {
            ["user_id"] = userId,
            ["package_id"] = package.Id.ToString(),
            ["payment_transaction_id"] = transaction.Id.ToString(),
            ["coupon_code"] = "FENCE20",
            ["original_price"] = "9.99",
            ["discount_amount"] = "2.00"
        };
        var stripeEvent = CreateStripeEvent(PaymentIntentSucceededEvent, transaction.ExternalTransactionId, metadata);

        await using var firstContext = new ApplicationDbContext(options);
        var blockingCoupon = new BlockingCouponService(
            new AI.ProfilePhotoMaker.API.Services.CouponService(firstContext, NullLogger<AI.ProfilePhotoMaker.API.Services.CouponService>.Instance));
        var first = CreateRealService(firstContext, blockingCoupon).HandleEventAsync(stripeEvent);
        await blockingCoupon.Started.WaitAsync(TimeSpan.FromSeconds(10));

        const string replacementToken = "reconciled-webhook-owner";
        await using (var fencingContext = new ApplicationDbContext(options))
        {
            await fencingContext.StripeWebhookOperations.ExecuteUpdateAsync(setters => setters
                .SetProperty(operation => operation.OperationToken, replacementToken));
        }

        blockingCoupon.Release();
        await Assert.ThrowsAsync<InvalidOperationException>(() => first);

        await using var verification = new ApplicationDbContext(options);
        Assert.Empty(await verification.CouponRedemptions.ToListAsync());
        Assert.Empty(await verification.CreditPurchases.ToListAsync());
        Assert.Equal(5, (await verification.UserProfiles.SingleAsync(p => p.UserId == userId)).Credits);
        var operation = await verification.StripeWebhookOperations.SingleAsync();
        Assert.Equal(StripeWebhookOperationStatus.Processing, operation.Status);
        Assert.Equal(replacementToken, operation.OperationToken);
    }

    [Fact]
    public async Task HandleEventAsync_DoesNotReclaimExpiredProcessingReceipt()
    {
        using var context = CreateContext();
        var (userId, package, transaction) = await SeedSuccessfulScenarioAsync(context);
        var stripeEvent = CreateStripeEvent(
            PaymentIntentSucceededEvent,
            transaction.ExternalTransactionId,
            userId,
            package.Id,
            transaction.Id);
        var operationKey = $"{PaymentIntentSucceededEvent}:{transaction.ExternalTransactionId}";
        context.StripeWebhookOperations.Add(new StripeWebhookOperation
        {
            OperationKey = operationKey,
            StripeEventId = stripeEvent.Id,
            EventType = stripeEvent.Type,
            PaymentIntentId = transaction.ExternalTransactionId,
            Status = StripeWebhookOperationStatus.Processing,
            OperationToken = "original-owner",
            LeaseExpiresAt = DateTime.UtcNow.AddMinutes(-1),
            CreatedAt = DateTime.UtcNow.AddMinutes(-20),
            UpdatedAt = DateTime.UtcNow.AddMinutes(-20)
        });
        await context.SaveChangesAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() => CreateService(context).HandleEventAsync(stripeEvent));

        Assert.Equal(5, (await context.UserProfiles.SingleAsync(p => p.UserId == userId)).Credits);
        Assert.Empty(await context.CreditPurchases.ToListAsync());
        var operation = await context.StripeWebhookOperations.SingleAsync();
        Assert.Equal(StripeWebhookOperationStatus.Processing, operation.Status);
        Assert.Equal("original-owner", operation.OperationToken);
        Assert.Equal(1, operation.AttemptCount);
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

    private static StripeWebhookService CreateService(ApplicationDbContext context, StripeClient? stripeClient = null)
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
            Options.Create(simulationOptions),
            stripeClient);

        var email = new DummyEmailNotificationService();
        return new StripeWebhookService(
            context,
            creditPackageService,
            NullLogger<StripeWebhookService>.Instance,
            email,
            new DummyCouponService());
    }

    private static StripeWebhookService CreateRealService(ApplicationDbContext context, ICouponService couponService)
    {
        var basicTierService = new BasicTierService(context, NullLogger<BasicTierService>.Instance);
        var outcomePackageService = new OutcomePackageService(context, NullLogger<OutcomePackageService>.Instance);
        var creditPackageService = new CreditPackageService(
            context,
            basicTierService,
            NullLogger<CreditPackageService>.Instance,
            Options.Create(new StripeOptions
            {
                PublishableKey = "pk_test_default",
                SecretKey = "sk_test_default",
                WebhookSecret = "whsec_default"
            }),
            Options.Create(new PaymentSimulationOptions()),
            stripeClient: null,
            outcomePackageService: outcomePackageService);
        return new StripeWebhookService(
            context,
            creditPackageService,
            NullLogger<StripeWebhookService>.Instance,
            new DummyEmailNotificationService(),
            couponService);
    }

    private sealed class BlockingCouponService : ICouponService
    {
        private readonly ICouponService _inner;
        private readonly TaskCompletionSource _started = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _release = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public BlockingCouponService(ICouponService inner) => _inner = inner;
        public Task Started => _started.Task;
        public void Release() => _release.TrySetResult();

        public Task<(bool IsValid, string Message, decimal DiscountAmount)> ValidateCouponAsync(string code, string userId, decimal originalPrice)
            => _inner.ValidateCouponAsync(code, userId, originalPrice);

        public async Task<bool> RedeemCouponAsync(
            string code,
            string userId,
            decimal originalPrice,
            decimal discountApplied,
            int? paymentTransactionId = null,
            string? webhookOperationKey = null,
            string? webhookOperationToken = null)
        {
            _started.TrySetResult();
            await _release.Task;
            return await _inner.RedeemCouponAsync(
                code,
                userId,
                originalPrice,
                discountApplied,
                paymentTransactionId,
                webhookOperationKey,
                webhookOperationToken);
        }
    }

    private sealed class ThrowingCouponService : ICouponService
    {
        public Task<(bool IsValid, string Message, decimal DiscountAmount)> ValidateCouponAsync(
            string code,
            string userId,
            decimal originalPrice) => Task.FromResult((true, "ok", 2m));

        public Task<bool> RedeemCouponAsync(
            string code,
            string userId,
            decimal originalPrice,
            decimal discountApplied,
            int? paymentTransactionId = null,
            string? webhookOperationKey = null,
            string? webhookOperationToken = null) =>
            throw new InvalidOperationException("transient coupon persistence failure");
    }

    private sealed class UnavailableStripeHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.ServiceUnavailable));
    }

    private sealed class DummyEmailNotificationService : IEmailNotificationService
    {
        public Task SendTrainingCompletedAsync(string userId, string? email, string? modelName, string? modelVersion) => Task.CompletedTask;
        public Task SendGenerationCompletedAsync(string userId, string? email, string? style, int imageCount, string? jobId = null) => Task.CompletedTask;
        public Task SendGenerationFailedAsync(string userId, string? email, string? style, string? error, string? jobId = null) => Task.CompletedTask;
        public Task SendPurchaseReceiptAsync(string userId, string? email, CreditPurchase purchase) => Task.CompletedTask;
        public Task SendSupportFeedbackReceivedAsync(string userId, string? userEmail, FeedbackSubmission submission) => Task.CompletedTask;
        public Task SendEmailVerificationAsync(string userId, string? email, string encodedToken) => Task.CompletedTask;
        public Task SendWelcomeAsync(string userId, string? email, string? firstName = null) => Task.CompletedTask;
        public Task SendRetentionDeletionWarningAsync(string userId, string? email, int imageCount, DateTime deletionDate, int daysUntilDeletion) => Task.CompletedTask;
        public Task SendAbandonedUploadNudgeAsync(string userId, string? email, string? firstName = null, int uploadedCount = 0, int minimumRequiredUploads = 5) => Task.CompletedTask;
        public Task<EmailSendResult> SendMarketingEmailAsync(string userId, string email, string subject, string htmlBody, string unsubscribeUrl) => Task.FromResult(new EmailSendResult(true));
        public string RenderMarketingEmailPreview(string subject, string htmlBody) => htmlBody;
    }

    private sealed class DummyCouponService : ICouponService
    {
        public Task<(bool IsValid, string Message, decimal DiscountAmount)> ValidateCouponAsync(string code, string userId, decimal originalPrice)
            => Task.FromResult((true, "ok", 0m));

        public Task<bool> RedeemCouponAsync(
            string code,
            string userId,
            decimal originalPrice,
            decimal discountApplied,
            int? paymentTransactionId = null,
            string? webhookOperationKey = null,
            string? webhookOperationToken = null)
            => Task.FromResult(true);
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

    private static async Task<(string userId, CreditPackage package, PaymentTransaction transaction)> SeedSqliteScenarioAsync(
        ApplicationDbContext context,
        string email)
    {
        var package = await context.CreditPackages.SingleAsync(p => p.Id == 1);
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid().ToString(),
            UserName = email,
            Email = email
        };
        context.Users.Add(user);
        context.UserProfiles.Add(new UserProfile
        {
            UserId = user.Id,
            User = user,
            SubscriptionTier = SubscriptionTier.Basic,
            Credits = 5,
            LastCreditReset = DateTime.UtcNow
        });
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
