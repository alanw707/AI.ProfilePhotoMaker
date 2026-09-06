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

    [Fact]
    public async Task FirstFulfillment_ReportsTemporaryStripeFailureAsUnavailable()
    {
        using var context = CreateContext();
        var userId = await SeedUserProfileAsync(context);
        var package = await SeedCreditPackageAsync(context);
        context.PaymentTransactions.Add(new PaymentTransaction
        {
            Id = 82, UserId = userId, ExternalTransactionId = "pi_unavailable",
            Amount = package.Price, Currency = "USD", Status = PaymentStatus.Completed, Type = PaymentType.OneTime
        });
        await context.SaveChangesAsync();
        using var http = new HttpClient(new IntentResponseHandler("{}", HttpStatusCode.ServiceUnavailable));
        var stripe = new StripeClient("sk_test_unit", httpClient: new SystemNetHttpClient(http));

        var result = await CreateService(context, stripeClient: stripe)
            .PurchaseCreditPackageAsync(userId, package.PackageId, "82");

        Assert.False(result.Success);
        Assert.Equal("PaymentVerificationUnavailable", result.ErrorCode);
        Assert.Empty(await context.CreditPurchases.ToListAsync());
    }

    private sealed class IntentResponseHandler(string json, HttpStatusCode status = HttpStatusCode.OK) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(new HttpResponseMessage(status)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            });
    }

    [Fact]
    public async Task PurchaseCreditPackageAsync_CreditAllocationFailureRollsBackAndAllowsReplay()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite("Data Source=:memory:").Options;
        using var context = new ApplicationDbContext(options);
        await context.Database.OpenConnectionAsync();
        await context.Database.EnsureCreatedAsync();
        var userId = await SeedUserProfileAsync(context);
        var package = await SeedCreditPackageAsync(context);
        var profile = await context.UserProfiles.SingleAsync(p => p.UserId == userId);
        context.UserProfiles.Remove(profile);
        await context.SaveChangesAsync();
        var service = CreateService(context, new StripeOptions(),
            new PaymentSimulationOptions { Enabled = true, SkipStripeIntegration = true });

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.PurchaseCreditPackageAsync(userId, package.PackageId, "sim-retry-allocation"));
        await context.SaveChangesAsync();
        Assert.Empty(await context.CreditPurchases.ToListAsync());

        context.Users.Attach(profile.User);
        context.UserProfiles.Add(profile);
        await context.SaveChangesAsync();
        Assert.True((await service.PurchaseCreditPackageAsync(
            userId, package.PackageId, "sim-retry-allocation")).Success);
        Assert.True((await service.PurchaseCreditPackageAsync(
            userId, package.PackageId, "sim-retry-allocation")).Success);
        Assert.Single(await context.CreditPurchases.ToListAsync());
        Assert.Equal(5 + package.TotalCredits,
            (await context.UserProfiles.AsNoTracking().SingleAsync(p => p.UserId == userId)).Credits);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PurchaseCreditPackageAsync_ExecutionStrategyRetryAwardsExactlyOnce(bool afterCommit)
    {
        var fault = new CommitFault(afterCommit);
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite("Data Source=:memory:")
            .ReplaceService<Microsoft.EntityFrameworkCore.Storage.IExecutionStrategyFactory, RetryFactory>()
            .AddInterceptors(fault).Options;
        using var context = new ApplicationDbContext(options);
        await context.Database.OpenConnectionAsync();
        await context.Database.EnsureCreatedAsync();
        var userId = await SeedUserProfileAsync(context);
        var package = await SeedCreditPackageAsync(context);
        var service = CreateService(context, new StripeOptions(),
            new PaymentSimulationOptions { Enabled = true, SkipStripeIntegration = true });
        fault.Armed = true;

        var result = await service.PurchaseCreditPackageAsync(userId, package.PackageId, "sim-commit-retry");

        Assert.True(fault.Fired);
        Assert.True(result.Success);
        Assert.Single(await context.CreditPurchases.AsNoTracking().ToListAsync());
        Assert.Equal(5 + package.TotalCredits,
            (await context.UserProfiles.AsNoTracking().SingleAsync(p => p.UserId == userId)).Credits);
        Assert.True((await service.PurchaseCreditPackageAsync(
            userId, package.PackageId, "sim-commit-retry")).Success);
    }

    public sealed class RetryFactory(Microsoft.EntityFrameworkCore.Storage.ExecutionStrategyDependencies dependencies)
        : Microsoft.EntityFrameworkCore.Storage.IExecutionStrategyFactory
    {
        public Microsoft.EntityFrameworkCore.Storage.IExecutionStrategy Create() => new RetryStrategy(dependencies);
    }

    private sealed class RetryStrategy(Microsoft.EntityFrameworkCore.Storage.ExecutionStrategyDependencies dependencies)
        : Microsoft.EntityFrameworkCore.Storage.ExecutionStrategy(dependencies, 1, TimeSpan.Zero)
    {
        protected override bool ShouldRetryOn(Exception exception) => exception is TimeoutException;
    }

    public sealed class CommitFault(bool afterCommit) : Microsoft.EntityFrameworkCore.Diagnostics.DbTransactionInterceptor
    {
        public bool Armed { get; set; }
        public bool Fired { get; private set; }

        private void FailOnce(bool committed)
        {
            if (Armed && !Fired && committed == afterCommit)
            {
                Fired = true;
                throw new TimeoutException("Injected local commit outage");
            }
        }

        public override ValueTask<Microsoft.EntityFrameworkCore.Diagnostics.InterceptionResult> TransactionCommittingAsync(
            System.Data.Common.DbTransaction transaction,
            Microsoft.EntityFrameworkCore.Diagnostics.TransactionEventData eventData,
            Microsoft.EntityFrameworkCore.Diagnostics.InterceptionResult result,
            CancellationToken cancellationToken = default)
        {
            FailOnce(false);
            return ValueTask.FromResult(result);
        }

        public override Task TransactionCommittedAsync(
            System.Data.Common.DbTransaction transaction,
            Microsoft.EntityFrameworkCore.Diagnostics.TransactionEndEventData eventData,
            CancellationToken cancellationToken = default)
        {
            FailOnce(true);
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task DistinctPayments_StaleAccountBalanceCannotLoseAwardAndReplayCompletes()
    {
        await using var connection = new Microsoft.Data.Sqlite.SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options;
        using var first = new ApplicationDbContext(options);
        await first.Database.EnsureCreatedAsync();
        var userId = await SeedUserProfileAsync(first);
        var package = await SeedCreditPackageAsync(first);
        foreach (var id in new[] { 101, 102 })
        {
            first.PaymentTransactions.Add(new PaymentTransaction
            {
                Id = id, UserId = userId, Amount = package.Price,
                ExternalTransactionId = $"pi_distinct_{id}", Status = PaymentStatus.Completed,
                Type = PaymentType.OneTime
            });
        }
        await first.SaveChangesAsync();
        using var second = new ApplicationDbContext(options);
        // Both replicas have read B before either payment writes its award.
        await second.UserProfiles.SingleAsync(p => p.UserId == userId);
        Assert.True((await CreateService(first).PurchaseCreditPackageAsync(userId, package.PackageId, "101")).Success);
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() =>
            CreateService(second).PurchaseCreditPackageAsync(userId, package.PackageId, "102"));

        using var replay = new ApplicationDbContext(options);
        Assert.Single(await replay.CreditPurchases.ToListAsync());
        Assert.True((await CreateService(replay).PurchaseCreditPackageAsync(userId, package.PackageId, "102")).Success);
        Assert.True((await CreateService(replay).PurchaseCreditPackageAsync(userId, package.PackageId, "101")).Success);
        Assert.Equal(2, await replay.CreditPurchases.CountAsync());
        Assert.Equal(5 + 2 * package.TotalCredits,
            (await replay.UserProfiles.AsNoTracking().SingleAsync()).Credits);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task PurchaseAndGenerationDebit_StaleBalanceCannotOverwriteOtherChange(bool purchaseFirst)
    {
        await using var connection = new Microsoft.Data.Sqlite.SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options;
        using var purchaseContext = new ApplicationDbContext(options);
        await purchaseContext.Database.EnsureCreatedAsync();
        var userId = await SeedUserProfileAsync(purchaseContext);
        var package = await SeedCreditPackageAsync(purchaseContext);
        using var generationContext = new ApplicationDbContext(options);
        await generationContext.UserProfiles.SingleAsync(p => p.UserId == userId);
        var purchasing = CreateService(purchaseContext, new StripeOptions(),
            new PaymentSimulationOptions { Enabled = true, SkipStripeIntegration = true });
        var debiting = new BasicTierService(generationContext, NullLogger<BasicTierService>.Instance);
        if (purchaseFirst)
        {
            Assert.True((await purchasing.PurchaseCreditPackageAsync(userId, package.PackageId, "sim-contention")).Success);
            await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() =>
                debiting.ConsumeCreditsAsync(userId, 1, "instant_headshot_generation", "contention"));
        }
        else
        {
            Assert.True((await debiting.ConsumeCreditsAsync(userId, 1, "instant_headshot_generation", "contention")).Success);
            await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() =>
                purchasing.PurchaseCreditPackageAsync(userId, package.PackageId, "sim-contention"));
        }
        using var replay = new ApplicationDbContext(options);
        if (purchaseFirst)
        {
            Assert.True((await new BasicTierService(replay, NullLogger<BasicTierService>.Instance)
                .ConsumeCreditsAsync(userId, 1, "instant_headshot_generation", "contention")).Success);
        }
        else
        {
            Assert.True((await CreateService(replay, new StripeOptions(),
                new PaymentSimulationOptions { Enabled = true, SkipStripeIntegration = true })
                .PurchaseCreditPackageAsync(userId, package.PackageId, "sim-contention")).Success);
        }
        Assert.Equal(5 + package.TotalCredits - 1,
            (await replay.UserProfiles.AsNoTracking().SingleAsync()).Credits);
        Assert.Single(await replay.CreditPurchases.ToListAsync());
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
