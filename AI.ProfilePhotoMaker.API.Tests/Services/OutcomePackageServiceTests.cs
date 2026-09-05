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

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(options);
    }
}
