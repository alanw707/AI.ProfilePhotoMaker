using AI.ProfilePhotoMaker.API.Data;
using AI.ProfilePhotoMaker.API.Models;
using AI.ProfilePhotoMaker.API.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace AI.ProfilePhotoMaker.API.Tests.Services;

public class OutcomePackageServiceTests
{
    [Fact]
    public async Task ConsumeMethods_DecrementPackageAllowancesAndRejectWhenUnavailable()
    {
        await using var context = CreateContext();
        var userId = "package-user";
        var package = new OutcomePackageDefinition
        {
            Code = "pro_package",
            Name = "Pro Package",
            Description = "Test package",
            IncludedCandidateCount = 9,
            IncludedRefinementCount = 3,
            IncludedPremiumAugmentationCount = 2,
            IncludesPlatformExportKit = true,
            IsActive = true
        };
        context.OutcomePackageDefinitions.Add(package);
        await context.SaveChangesAsync();

        context.UserPackageEntitlements.Add(new UserPackageEntitlement
        {
            UserId = userId,
            OutcomePackageDefinitionId = package.Id,
            Status = PackageEntitlementStatus.Active,
            RemainingPackageUses = 1,
            RemainingCandidates = 9,
            RemainingRefinements = 1,
            RemainingPremiumAugmentations = 1,
            PlatformExportKitAvailable = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();

        var service = new OutcomePackageService(context, NullLogger<OutcomePackageService>.Instance);

        Assert.False(await service.ConsumeCandidatesAsync(userId, "pro_package", 0));
        Assert.False(await service.ConsumeCandidatesAsync(userId, "pro_package", -1));
        Assert.False(await service.ConsumeCandidatesAsync(userId, "pro_package", 10));
        Assert.True(await service.ConsumeCandidatesAsync(userId, "pro_package", 9));
        Assert.False(await service.ConsumeCandidatesAsync(userId, "pro_package", 1));
        Assert.True(await service.ConsumeRefinementAsync(userId));
        Assert.False(await service.ConsumeRefinementAsync(userId));
        Assert.True(await service.ConsumePremiumAugmentationAsync(userId));
        Assert.False(await service.ConsumePremiumAugmentationAsync(userId));
        Assert.True(await service.ConsumeExportKitAsync(userId));
        Assert.False(await service.ConsumeExportKitAsync(userId));

        var entitlement = await context.UserPackageEntitlements.SingleAsync(e => e.UserId == userId);
        Assert.Equal(PackageEntitlementStatus.Consumed, entitlement.Status);
        Assert.Equal(0, entitlement.RemainingCandidates);
        Assert.Equal(0, entitlement.RemainingRefinements);
        Assert.Equal(0, entitlement.RemainingPremiumAugmentations);
        Assert.False(entitlement.PlatformExportKitAvailable);
    }

    [Fact]
    public async Task ExpiredEntitlement_IsReportedExpired_AndCannotBeConsumed()
    {
        await using var context = CreateContext();
        var package = new OutcomePackageDefinition
        {
            Code = "pro_package", Name = "Pro", Description = "Test", IsActive = true
        };
        context.OutcomePackageDefinitions.Add(package);
        context.UserPackageEntitlements.Add(new UserPackageEntitlement
        {
            UserId = "expired-user", OutcomePackageDefinition = package,
            Status = PackageEntitlementStatus.Active, RemainingPackageUses = 1,
            RemainingCandidates = 9, RemainingRefinements = 5,
            RemainingPremiumAugmentations = 3, PlatformExportKitAvailable = true,
            ExpiresAt = DateTime.UtcNow.AddMinutes(-1)
        });
        await context.SaveChangesAsync();
        var service = new OutcomePackageService(context, NullLogger<OutcomePackageService>.Instance);

        var entitlement = Assert.Single(await service.GetUserEntitlementsAsync("expired-user"));
        Assert.Equal("expired", entitlement.Status);
        Assert.Null(await service.GetActiveEntitlementAsync("expired-user", "pro_package"));
        Assert.False(await service.ConsumeCandidatesAsync("expired-user", "pro_package", 1));
        Assert.False(await service.ConsumeRefinementAsync("expired-user"));
        Assert.False(await service.ConsumePremiumAugmentationAsync("expired-user"));
        Assert.False(await service.ConsumeExportKitAsync("expired-user"));
    }

    [Fact]
    public async Task ConsumeCandidates_FreePreview_AllowsOnlyOneCandidateWithoutEntitlement()
    {
        await using var context = CreateContext();
        var service = new OutcomePackageService(context, NullLogger<OutcomePackageService>.Instance);

        Assert.True(await service.ConsumeCandidatesAsync("free-user", "free_preview", 1));
        Assert.False(await service.ConsumeCandidatesAsync("free-user", "free_preview", 2));
    }

    [Theory]
    [InlineData("candidates")]
    [InlineData("refinement")]
    [InlineData("premium")]
    [InlineData("export")]
    public async Task CompetingConsumptionAcrossContexts_OnlyOneRequestConsumesLastAllowance(string kind)
    {
        await using var connection = new Microsoft.Data.Sqlite.SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options;
        using (var setup = new ApplicationDbContext(options))
        {
            await setup.Database.EnsureCreatedAsync();
            setup.Users.Add(new ApplicationUser { Id = "race-user", UserName = "race-user" });
            var package = await setup.OutcomePackageDefinitions.SingleAsync(p => p.Code == "pro_package");
            setup.UserPackageEntitlements.Add(new UserPackageEntitlement
            {
                UserId = "race-user", OutcomePackageDefinitionId = package.Id,
                Status = PackageEntitlementStatus.Active, RemainingPackageUses = 1,
                RemainingCandidates = 1, RemainingRefinements = 1,
                RemainingPremiumAugmentations = 1, PlatformExportKitAvailable = true
            });
            await setup.SaveChangesAsync();
        }
        var firstGate = new ConsumptionSaveGate();
        var secondGate = new ConsumptionSaveGate();
        using var first = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connection).AddInterceptors(firstGate).Options);
        using var second = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connection).AddInterceptors(secondGate).Options);
        Task<bool> Consume(ApplicationDbContext context)
        {
            var service = new OutcomePackageService(context, NullLogger<OutcomePackageService>.Instance);
            return kind switch
            {
                "candidates" => service.ConsumeCandidatesAsync("race-user", "pro_package", 1),
                "refinement" => service.ConsumeRefinementAsync("race-user"),
                "premium" => service.ConsumePremiumAugmentationAsync("race-user"),
                _ => service.ConsumeExportKitAsync("race-user")
            };
        }
        try
        {
            var winner = Consume(first);
            await firstGate.Reached.Task.WaitAsync(TimeSpan.FromSeconds(10));
            var loser = Consume(second);
            await secondGate.Reached.Task.WaitAsync(TimeSpan.FromSeconds(10));
            firstGate.Release.TrySetResult();
            Assert.True(await winner);
            secondGate.Release.TrySetResult();
            Assert.False(await loser);
            // A caller's later save must not flush the rejected consumption.
            await second.SaveChangesAsync();
            using var verification = new ApplicationDbContext(options);
            Assert.False(await Consume(verification));
            var row = await verification.UserPackageEntitlements.SingleAsync();
            Assert.Equal(kind == "candidates" ? 0 : 1, row.RemainingCandidates);
            Assert.Equal(kind == "candidates" ? 0 : 1, row.RemainingPackageUses);
            Assert.Equal(kind == "refinement" ? 0 : 1, row.RemainingRefinements);
            Assert.Equal(kind == "premium" ? 0 : 1, row.RemainingPremiumAugmentations);
            Assert.Equal(kind != "export", row.PlatformExportKitAvailable);
        }
        finally
        {
            firstGate.Release.TrySetResult();
            secondGate.Release.TrySetResult();
        }
    }

    private sealed class ConsumptionSaveGate : Microsoft.EntityFrameworkCore.Diagnostics.SaveChangesInterceptor
    {
        public TaskCompletionSource Reached { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public override async ValueTask<Microsoft.EntityFrameworkCore.Diagnostics.InterceptionResult<int>> SavingChangesAsync(
            Microsoft.EntityFrameworkCore.Diagnostics.DbContextEventData eventData,
            Microsoft.EntityFrameworkCore.Diagnostics.InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            Reached.TrySetResult();
            await Release.Task.WaitAsync(cancellationToken);
            return result;
        }
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(options);
    }
}
