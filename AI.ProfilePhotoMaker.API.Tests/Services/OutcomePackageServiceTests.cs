using AI.ProfilePhotoMaker.API.Data;
using AI.ProfilePhotoMaker.API.Models;
using AI.ProfilePhotoMaker.API.Services;
using AI.ProfilePhotoMaker.API.Services.Storage;
using Microsoft.EntityFrameworkCore;
using Moq;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace AI.ProfilePhotoMaker.API.Tests.Services;

public class OutcomePackageServiceTests
{
    [Fact]
    public async Task ReservePreviewForPurchase_VerifiesRawAssetAndExtendsRetention()
    {
        await using var context = CreateContext();
        var userId = "checkout-user";
        var profile = new UserProfile { UserId = userId };
        context.UserProfiles.Add(profile);
        await context.SaveChangesAsync();
        var preview = new ProcessedImage
        {
            UserProfileId = profile.Id,
            OriginalImageUrl = "uploads/source.png",
            ProcessedImageUrl = "generated/preview.png",
            RawImageStoragePath = "generated-private/raw.png",
            Style = "linkedin",
            GenerationMode = "instant_headshot",
            GenerationStatus = "succeeded",
            ScheduledDeletionDate = DateTime.UtcNow.AddMinutes(1)
        };
        context.ProcessedImages.Add(preview);
        await context.SaveChangesAsync();
        var storage = new Mock<IStorageService>();
        storage.Setup(service => service.ExistsAsync(preview.RawImageStoragePath)).ReturnsAsync(true);
        var service = new OutcomePackageService(
            context,
            NullLogger<OutcomePackageService>.Instance,
            storage.Object);
        var beforeReservation = DateTime.UtcNow;

        var reserved = await service.ReservePreviewForPurchaseAsync(userId, preview.Id);

        Assert.True(reserved);
        Assert.True(preview.ScheduledDeletionDate >= beforeReservation.AddHours(24));
        storage.Verify(service => service.ExistsAsync(preview.RawImageStoragePath), Times.Once);
    }

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
    public async Task GrantEntitlement_PromotesRawPreviewAndConsumesOneCandidateSlot()
    {
        await using var context = CreateContext();
        var userId = "promotion-user";
        var profile = new UserProfile { UserId = userId };
        var package = new OutcomePackageDefinition
        {
            Code = "starter_package",
            Name = "Starter Package",
            Description = "Test package",
            InternalCreditPackageId = 7,
            IncludedCandidateCount = 3,
            IsActive = true
        };
        context.UserProfiles.Add(profile);
        context.OutcomePackageDefinitions.Add(package);
        await context.SaveChangesAsync();

        var preview = new ProcessedImage
        {
            UserProfileId = profile.Id,
            OriginalImageUrl = "uploads/source.png",
            ProcessedImageUrl = "generated/preview.png",
            RawImageStoragePath = "generated-private/raw.png",
            Style = "linkedin",
            IsGenerated = true,
            GenerationMode = "instant_headshot",
            GenerationStatus = "succeeded",
            Provider = "openai",
            ProviderModel = "gpt-image-2",
            ScheduledDeletionDate = DateTime.UtcNow.AddDays(30)
        };
        context.ProcessedImages.Add(preview);
        await context.SaveChangesAsync();

        var service = new OutcomePackageService(context, NullLogger<OutcomePackageService>.Instance);
        var entitlement = await service.GrantEntitlementForCreditPackageAsync(userId, 7, "42", preview.Id);

        Assert.NotNull(entitlement);
        Assert.Equal(2, entitlement!.RemainingCandidates);
        var promoted = await context.ProcessedImages.SingleAsync(i => i.GenerationMode == "instant_headshot_promoted_preview");
        Assert.Equal("generated-private/raw.png", promoted.ProcessedImageUrl);
        Assert.Equal(0, promoted.CreditCost);
    }

    [Fact]
    public async Task GrantEntitlement_RejectsExplicitPreviewWhenRawAssetIsMissing()
    {
        await using var context = CreateContext();
        var userId = "expired-preview-user";
        var profile = new UserProfile { UserId = userId };
        context.UserProfiles.Add(profile);
        context.OutcomePackageDefinitions.Add(new OutcomePackageDefinition
        {
            Code = "starter_package",
            Name = "Starter Package",
            Description = "Test package",
            InternalCreditPackageId = 8,
            IncludedCandidateCount = 3,
            IsActive = true
        });
        await context.SaveChangesAsync();
        var preview = new ProcessedImage
        {
            UserProfileId = profile.Id,
            OriginalImageUrl = "uploads/source.png",
            ProcessedImageUrl = "generated/preview.png",
            RawImageStoragePath = "generated-private/missing.png",
            Style = "linkedin",
            GenerationMode = "instant_headshot",
            GenerationStatus = "succeeded"
        };
        context.ProcessedImages.Add(preview);
        await context.SaveChangesAsync();
        var storage = new Mock<IStorageService>();
        storage.Setup(s => s.ExistsAsync(preview.RawImageStoragePath)).ReturnsAsync(false);
        var service = new OutcomePackageService(context, NullLogger<OutcomePackageService>.Instance, storage.Object);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.GrantEntitlementForCreditPackageAsync(userId, 8, "43", preview.Id));
        Assert.Empty(context.UserPackageEntitlements);
        Assert.Empty(context.ProcessedImages.Where(i => i.GenerationMode == "instant_headshot_promoted_preview"));
    }

    [Fact]
    public async Task GrantEntitlement_RejectsLatestPreviewWhenRawAssetIsMissingWithoutClientPreviewId()
    {
        await using var context = CreateContext();
        var userId = "server-selected-preview-user";
        var profile = new UserProfile { UserId = userId };
        context.UserProfiles.Add(profile);
        context.OutcomePackageDefinitions.Add(new OutcomePackageDefinition
        {
            Code = "starter_package",
            Name = "Starter Package",
            Description = "Test package",
            InternalCreditPackageId = 11,
            IncludedCandidateCount = 3,
            IsActive = true
        });
        await context.SaveChangesAsync();
        var preview = new ProcessedImage
        {
            UserProfileId = profile.Id,
            OriginalImageUrl = "uploads/source.png",
            ProcessedImageUrl = "generated/preview.png",
            RawImageStoragePath = "generated-private/missing-latest.png",
            Style = "linkedin",
            GenerationMode = "instant_headshot",
            GenerationStatus = "succeeded",
            CreatedAt = DateTime.UtcNow
        };
        context.ProcessedImages.Add(preview);
        await context.SaveChangesAsync();
        var storage = new Mock<IStorageService>();
        storage.Setup(s => s.ExistsAsync(preview.RawImageStoragePath)).ReturnsAsync(false);
        var service = new OutcomePackageService(context, NullLogger<OutcomePackageService>.Instance, storage.Object);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.GrantEntitlementForCreditPackageAsync(userId, 11, "46"));

        Assert.Empty(context.ProcessedImages.Where(i => i.GenerationMode == "instant_headshot_promoted_preview"));
        Assert.Equal(3, (await context.UserPackageEntitlements.SingleAsync()).RemainingCandidates);
    }

    [Fact]
    public async Task GrantEntitlement_AllowsStandalonePurchaseWhenUserHasNoPreview()
    {
        await using var context = CreateContext();
        context.OutcomePackageDefinitions.Add(new OutcomePackageDefinition
        {
            Code = "starter_package",
            Name = "Starter Package",
            Description = "Test package",
            InternalCreditPackageId = 12,
            IncludedCandidateCount = 3,
            IsActive = true
        });
        await context.SaveChangesAsync();
        var service = new OutcomePackageService(context, NullLogger<OutcomePackageService>.Instance, Mock.Of<IStorageService>());

        var entitlement = await service.GrantEntitlementForCreditPackageAsync("standalone-user", 12, "47");

        Assert.NotNull(entitlement);
        Assert.Equal(3, entitlement!.RemainingCandidates);
        Assert.Empty(context.ProcessedImages);
    }

    [Fact]
    public async Task GrantEntitlement_FailsExplicitlyWhenRawAssetDisappearsAfterPreflight()
    {
        await using var context = CreateContext();
        var userId = "promotion-race-user";
        var profile = new UserProfile { UserId = userId };
        context.UserProfiles.Add(profile);
        context.OutcomePackageDefinitions.Add(new OutcomePackageDefinition
        {
            Code = "starter_package",
            Name = "Starter Package",
            Description = "Test package",
            InternalCreditPackageId = 10,
            IncludedCandidateCount = 3,
            IsActive = true
        });
        await context.SaveChangesAsync();
        var preview = new ProcessedImage
        {
            UserProfileId = profile.Id,
            OriginalImageUrl = "uploads/source.png",
            ProcessedImageUrl = "generated/preview.png",
            RawImageStoragePath = "generated-private/racy.png",
            Style = "linkedin",
            GenerationMode = "instant_headshot",
            GenerationStatus = "succeeded"
        };
        context.ProcessedImages.Add(preview);
        await context.SaveChangesAsync();
        var storage = new Mock<IStorageService>();
        storage.SetupSequence(s => s.ExistsAsync(preview.RawImageStoragePath))
            .ReturnsAsync(true)
            .ReturnsAsync(false);
        var service = new OutcomePackageService(context, NullLogger<OutcomePackageService>.Instance, storage.Object);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.GrantEntitlementForCreditPackageAsync(userId, 10, "45", preview.Id));

        Assert.Empty(context.ProcessedImages.Where(i => i.GenerationMode == "instant_headshot_promoted_preview"));
        Assert.Equal(3, (await context.UserPackageEntitlements.SingleAsync()).RemainingCandidates);
    }

    [Fact]
    public async Task GrantEntitlement_RetryDoesNotDuplicatePromotionOrConsumeAnotherCandidate()
    {
        await using var context = CreateContext();
        var userId = "promotion-retry-user";
        var profile = new UserProfile { UserId = userId };
        context.UserProfiles.Add(profile);
        context.OutcomePackageDefinitions.Add(new OutcomePackageDefinition
        {
            Code = "starter_package",
            Name = "Starter Package",
            Description = "Test package",
            InternalCreditPackageId = 9,
            IncludedCandidateCount = 3,
            IsActive = true
        });
        await context.SaveChangesAsync();
        var preview = new ProcessedImage
        {
            UserProfileId = profile.Id,
            OriginalImageUrl = "uploads/source.png",
            ProcessedImageUrl = "generated/preview.png",
            RawImageStoragePath = "generated-private/raw.png",
            Style = "linkedin",
            GenerationMode = "instant_headshot",
            GenerationStatus = "succeeded"
        };
        context.ProcessedImages.Add(preview);
        await context.SaveChangesAsync();
        var storage = new Mock<IStorageService>();
        storage.Setup(s => s.ExistsAsync(preview.RawImageStoragePath)).ReturnsAsync(true);
        var service = new OutcomePackageService(context, NullLogger<OutcomePackageService>.Instance, storage.Object);

        await service.GrantEntitlementForCreditPackageAsync(userId, 9, "44", preview.Id);
        var retried = await service.GrantEntitlementForCreditPackageAsync(userId, 9, "44", preview.Id);

        Assert.NotNull(retried);
        Assert.Equal(2, retried!.RemainingCandidates);
        Assert.Single(context.ProcessedImages.Where(i => i.GenerationMode == "instant_headshot_promoted_preview"));
    }

    [Fact]
    public async Task PromotedPreviewDownload_RequiresOwnerPackageEntitlement()
    {
        await using var context = CreateContext();
        var userId = "download-user";
        var profile = new UserProfile { UserId = userId };
        var package = new OutcomePackageDefinition
        {
            Code = "starter_package",
            Name = "Starter Package",
            Description = "Test package",
            IsActive = true
        };
        context.UserProfiles.Add(profile);
        context.OutcomePackageDefinitions.Add(package);
        await context.SaveChangesAsync();
        var promoted = new ProcessedImage
        {
            UserProfileId = profile.Id,
            OriginalImageUrl = "uploads/source.png",
            ProcessedImageUrl = "generated-private/raw.png",
            Style = "linkedin",
            GenerationMode = "instant_headshot_promoted_preview",
            GenerationStatus = "succeeded"
        };
        context.ProcessedImages.Add(promoted);
        await context.SaveChangesAsync();
        var storage = new Mock<IStorageService>();
        storage.Setup(s => s.GetImageAsync(promoted.ProcessedImageUrl)).ReturnsAsync(new MemoryStream([1, 2, 3]));
        var service = new OutcomePackageService(context, NullLogger<OutcomePackageService>.Instance, storage.Object);

        Assert.Null(await service.GetPromotedPreviewDownloadAsync(userId, promoted.Id));

        context.UserPackageEntitlements.Add(new UserPackageEntitlement
        {
            UserId = userId,
            OutcomePackageDefinitionId = package.Id,
            Status = PackageEntitlementStatus.Active
        });
        await context.SaveChangesAsync();

        var download = await service.GetPromotedPreviewDownloadAsync(userId, promoted.Id);
        Assert.NotNull(download);
        Assert.Equal(promoted.Id, download!.ImageId);
        await download.Content.DisposeAsync();
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
