using AI.ProfilePhotoMaker.API.Data;
using AI.ProfilePhotoMaker.API.Models;
using AI.ProfilePhotoMaker.API.Services;
using AI.ProfilePhotoMaker.API.Services.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
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
    public async Task AbandonPreview_MarksOnlyAnUnpromotedFreePreviewAsAbandoned()
    {
        await using var context = CreateContext();
        var userId = "abandon-user";
        var profile = new UserProfile { UserId = userId };
        context.UserProfiles.Add(profile);
        await context.SaveChangesAsync();
        var preview = new ProcessedImage
        {
            UserProfileId = profile.Id,
            OriginalImageUrl = "uploads/source.png",
            ProcessedImageUrl = "generated/watermarked.png",
            RawImageStoragePath = "generated-private/raw.png",
            Style = "linkedin",
            GenerationMode = "instant_headshot",
            GenerationStatus = "succeeded"
        };
        context.ProcessedImages.Add(preview);
        await context.SaveChangesAsync();
        var service = new OutcomePackageService(context, NullLogger<OutcomePackageService>.Instance);

        Assert.True(await service.AbandonPreviewAsync(userId, preview.Id));
        Assert.Equal("abandoned", preview.GenerationStatus);
        Assert.Null(await service.GetResumablePreviewAsync(userId, preview.Id));
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
    public async Task ConsumeCandidates_KeepsPackageResumableUntilEveryCandidateSlotIsFilled()
    {
        await using var context = CreateContext();
        var package = new OutcomePackageDefinition
        {
            Code = "pro_package",
            Name = "Pro Package",
            Description = "Test package",
            IncludedCandidateCount = 9,
            IsActive = true
        };
        context.OutcomePackageDefinitions.Add(package);
        await context.SaveChangesAsync();
        context.UserPackageEntitlements.Add(new UserPackageEntitlement
        {
            UserId = "partial-user",
            OutcomePackageDefinitionId = package.Id,
            Status = PackageEntitlementStatus.Active,
            RemainingPackageUses = 1,
            RemainingCandidates = 8,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();
        var service = new OutcomePackageService(context, NullLogger<OutcomePackageService>.Instance);

        Assert.True(await service.ConsumeCandidatesAsync("partial-user", "pro_package", 3));
        var partial = await context.UserPackageEntitlements.SingleAsync();
        Assert.Equal(5, partial.RemainingCandidates);
        Assert.Equal(1, partial.RemainingPackageUses);

        Assert.True(await service.ConsumeCandidatesAsync("partial-user", "pro_package", 5));
        Assert.Equal(0, partial.RemainingCandidates);
        Assert.Equal(0, partial.RemainingPackageUses);
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
    public async Task GrantEntitlement_ConcurrentPromotionUsesWinnerWithoutDoubleConsumption()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"promotion-race-{Guid.NewGuid():N}.db");
        var connectionString = $"Data Source={databasePath}";
        var winnerOptions = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connectionString)
            .Options;

        try
        {
            int previewId;
            await using (var seed = new ApplicationDbContext(winnerOptions))
            {
                await seed.Database.EnsureCreatedAsync();
                var user = new ApplicationUser
                {
                    Id = "concurrent-user",
                    UserName = "concurrent@example.com",
                    Email = "concurrent@example.com"
                };
                var profile = new UserProfile { UserId = user.Id, User = user };
                var creditPackage = new CreditPackage
                {
                    Id = 12012,
                    Name = "Concurrent Credits",
                    Description = "Test package",
                    IsActive = true
                };
                var package = new OutcomePackageDefinition
                {
                    Code = "concurrent_package",
                    Name = "Concurrent Package",
                    Description = "Test package",
                    InternalCreditPackage = creditPackage,
                    IncludedCandidateCount = 3,
                    IsActive = true
                };
                seed.Users.Add(user);
                seed.CreditPackages.Add(creditPackage);
                seed.UserProfiles.Add(profile);
                seed.OutcomePackageDefinitions.Add(package);
                await seed.SaveChangesAsync();
                var preview = new ProcessedImage
                {
                    UserProfileId = profile.Id,
                    OriginalImageUrl = "uploads/concurrent-source.png",
                    ProcessedImageUrl = "generated/concurrent-preview.png",
                    RawImageStoragePath = "generated-private/concurrent-raw.png",
                    Style = "linkedin",
                    GenerationMode = "instant_headshot",
                    GenerationStatus = "succeeded"
                };
                seed.ProcessedImages.Add(preview);
                seed.PaymentTransactions.Add(new PaymentTransaction
                {
                    Id = 77,
                    UserId = user.Id,
                    ExternalTransactionId = "pi_concurrent",
                    Amount = 9.99m,
                    Currency = "usd",
                    PaymentProvider = "stripe",
                    Status = PaymentStatus.Completed,
                    Type = PaymentType.OneTime,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                });
                seed.UserPackageEntitlements.Add(new UserPackageEntitlement
                {
                    UserId = profile.UserId,
                    OutcomePackageDefinitionId = package.Id,
                    SourcePaymentTransactionId = 77,
                    Status = PackageEntitlementStatus.Active,
                    RemainingPackageUses = 1,
                    RemainingCandidates = 3,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                });
                await seed.SaveChangesAsync();
                previewId = preview.Id;
            }

            var interceptor = new ConcurrentPromotionInterceptor(winnerOptions);
            var loserOptions = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseSqlite(connectionString)
                .AddInterceptors(interceptor)
                .Options;
            await using var loser = new ApplicationDbContext(loserOptions);
            var service = new OutcomePackageService(loser, NullLogger<OutcomePackageService>.Instance);

            var entitlement = await service.GrantEntitlementForCreditPackageAsync(
                "concurrent-user",
                12012,
                "77",
                previewId);

            Assert.NotNull(entitlement);
            await using var verification = new ApplicationDbContext(winnerOptions);
            Assert.Single(await verification.ProcessedImages
                .Where(image => image.GenerationMode == "instant_headshot_promoted_preview")
                .ToListAsync());
            Assert.Equal(2, (await verification.UserPackageEntitlements.SingleAsync()).RemainingCandidates);
        }
        finally
        {
            File.Delete(databasePath);
        }
    }

    [Theory]
    [InlineData(8, 0, 9)]
    [InlineData(3, 5, 4)]
    public async Task GetResumablePreview_RestoresPromotedAndPaidCandidatesAfterInterruption(
        int generatedCandidateCount,
        int remainingCandidateCount,
        int expectedTotal)
    {
        await using var context = CreateContext();
        var userId = "resume-paid-user";
        var profile = new UserProfile { UserId = userId };
        var package = new OutcomePackageDefinition
        {
            Code = "pro_package",
            Name = "Pro Package",
            Description = "Test package",
            IncludedCandidateCount = 9,
            IncludedRefinementCount = 3,
            IncludedPremiumAugmentationCount = 3,
            IncludesPlatformExportKit = true,
            IsActive = true
        };
        var style = new Style
        {
            Name = "linkedin",
            Description = "Professional",
            PromptTemplate = "Professional portrait",
            NegativePromptTemplate = string.Empty,
            IsActive = true
        };
        context.UserProfiles.Add(profile);
        context.OutcomePackageDefinitions.Add(package);
        context.Styles.Add(style);
        await context.SaveChangesAsync();

        var preview = new ProcessedImage
        {
            UserProfileId = profile.Id,
            OriginalImageUrl = "uploads/source.png",
            ProcessedImageUrl = "generated/preview.png",
            RawImageStoragePath = "generated-private/raw.png",
            Style = style.Name,
            GenerationMode = "instant_headshot",
            GenerationStatus = "succeeded",
            CreatedAt = DateTime.UtcNow.AddMinutes(-5)
        };
        context.ProcessedImages.Add(preview);
        await context.SaveChangesAsync();

        var entitlement = new UserPackageEntitlement
        {
            UserId = userId,
            OutcomePackageDefinitionId = package.Id,
            Status = PackageEntitlementStatus.Active,
            RemainingPackageUses = remainingCandidateCount > 0 ? 1 : 0,
            RemainingCandidates = remainingCandidateCount,
            RemainingRefinements = 3,
            RemainingPremiumAugmentations = 3,
            PlatformExportKitAvailable = true,
            ActivatedAt = DateTime.UtcNow.AddMinutes(-4),
            CreatedAt = DateTime.UtcNow.AddMinutes(-4),
            UpdatedAt = DateTime.UtcNow
        };
        var promoted = new ProcessedImage
        {
            UserProfileId = profile.Id,
            OriginalImageUrl = preview.OriginalImageUrl,
            ProcessedImageUrl = preview.RawImageStoragePath!,
            Style = style.Name,
            GenerationMode = "instant_headshot_promoted_preview",
            GenerationStatus = "succeeded",
            CorrelationId = "purchase:1:promoted-preview",
            CreatedAt = DateTime.UtcNow.AddMinutes(-3)
        };
        context.UserPackageEntitlements.Add(entitlement);
        context.ProcessedImages.Add(promoted);
        context.ProcessedImages.AddRange(Enumerable.Range(1, generatedCandidateCount).Select(index => new ProcessedImage
        {
            UserProfileId = profile.Id,
            OriginalImageUrl = preview.OriginalImageUrl,
            ProcessedImageUrl = $"generated/paid-{index}.png",
            RawImageStoragePath = $"generated-private/paid-{index}.png",
            Style = style.Name,
            GenerationMode = "instant_headshot",
            GenerationStatus = "succeeded",
            CorrelationId = $"paid-batch:candidate:{index}",
            CreatedAt = DateTime.UtcNow.AddMinutes(-2).AddSeconds(index)
        }));
        await context.SaveChangesAsync();
        promoted.CorrelationId = $"purchase:{entitlement.Id}:promoted-preview";
        await context.SaveChangesAsync();

        var storage = new Mock<IStorageService>();
        storage.Setup(service => service.ExistsAsync(It.IsAny<string>())).ReturnsAsync(true);
        var service = new OutcomePackageService(
            context,
            NullLogger<OutcomePackageService>.Instance,
            storage.Object);

        var resumed = await service.GetResumablePreviewAsync(userId, preview.Id);

        Assert.NotNull(resumed);
        Assert.Equal(expectedTotal, resumed!.Candidates.Count);
        Assert.Equal(promoted.Id, resumed.Candidates[0].ProcessedImageId);
        Assert.Equal(remainingCandidateCount, resumed.RemainingCandidateCount);

        var replaced = await context.ProcessedImages.SingleAsync(image =>
            image.CorrelationId == "paid-batch:candidate:1");
        var refinement = new ProcessedImage
        {
            UserProfileId = profile.Id,
            OriginalImageUrl = preview.OriginalImageUrl,
            ProcessedImageUrl = "generated/refined.png",
            Style = style.Name,
            GenerationMode = "instant_headshot",
            GenerationStatus = "succeeded",
            CorrelationId = "refinement-1",
            ReplacesProcessedImageId = replaced.Id,
            CreatedAt = DateTime.UtcNow
        };
        context.ProcessedImages.Add(refinement);
        await context.SaveChangesAsync();

        var resumedAfterRefinement = await service.GetResumablePreviewAsync(userId, preview.Id);

        Assert.NotNull(resumedAfterRefinement);
        Assert.Equal(expectedTotal, resumedAfterRefinement!.Candidates.Count);
        Assert.Contains(resumedAfterRefinement.Candidates, candidate =>
            candidate.ProcessedImageId == refinement.Id);
        Assert.DoesNotContain(resumedAfterRefinement.Candidates, candidate =>
            candidate.ProcessedImageId == replaced.Id);

        var resumedWithoutPreviewId = await service.GetResumablePreviewAsync(userId);

        Assert.NotNull(resumedWithoutPreviewId);
        Assert.Equal(preview.Id, resumedWithoutPreviewId!.ProcessedImageId);
        Assert.Equal(expectedTotal, resumedWithoutPreviewId.Candidates.Count);
        Assert.Contains(resumedWithoutPreviewId.Candidates, candidate =>
            candidate.ProcessedImageId == refinement.Id);
    }

    [Fact]
    public async Task ConsumeCandidates_FreePreview_AllowsOnlyOneCandidateWithoutEntitlement()
    {
        await using var context = CreateContext();
        var service = new OutcomePackageService(context, NullLogger<OutcomePackageService>.Instance);

        Assert.True(await service.ConsumeCandidatesAsync("free-user", "free_preview", 1));
        Assert.False(await service.ConsumeCandidatesAsync("free-user", "free_preview", 2));
    }

    private sealed class ConcurrentPromotionInterceptor(
        DbContextOptions<ApplicationDbContext> winnerOptions) : SaveChangesInterceptor
    {
        private int _hasInjectedWinner;

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            var losingContext = (ApplicationDbContext)eventData.Context!;
            var attemptedPromotion = losingContext.ChangeTracker.Entries<ProcessedImage>()
                .FirstOrDefault(entry =>
                    entry.State == EntityState.Added &&
                    entry.Entity.GenerationMode == "instant_headshot_promoted_preview");
            if (attemptedPromotion == null || Interlocked.Exchange(ref _hasInjectedWinner, 1) != 0)
            {
                return result;
            }

            await using var winner = new ApplicationDbContext(winnerOptions);
            var entitlement = await winner.UserPackageEntitlements.SingleAsync(cancellationToken);
            winner.ProcessedImages.Add(new ProcessedImage
            {
                UserProfileId = attemptedPromotion.Entity.UserProfileId,
                OriginalImageUrl = attemptedPromotion.Entity.OriginalImageUrl,
                ProcessedImageUrl = attemptedPromotion.Entity.ProcessedImageUrl,
                Style = attemptedPromotion.Entity.Style,
                GenerationMode = "instant_headshot_promoted_preview",
                GenerationStatus = "succeeded",
                IsGenerated = true,
                ScheduledDeletionDate = DateTime.UtcNow.AddDays(30)
            });
            entitlement.RemainingCandidates--;
            entitlement.UpdatedAt = DateTime.UtcNow;
            await winner.SaveChangesAsync(cancellationToken);
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
