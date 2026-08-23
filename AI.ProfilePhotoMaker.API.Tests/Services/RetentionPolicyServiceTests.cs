using AI.ProfilePhotoMaker.API.Configuration;
using AI.ProfilePhotoMaker.API.Data;
using AI.ProfilePhotoMaker.API.Models;
using AI.ProfilePhotoMaker.API.Services;
using AI.ProfilePhotoMaker.API.Services.ImageProcessing;
using AI.ProfilePhotoMaker.API.Services.Storage;
using AI.ProfilePhotoMaker.API.Tests.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace AI.ProfilePhotoMaker.API.Tests.Services;

public class RetentionPolicyServiceTests
{
    [Fact]
    public async Task GetImagesApproachingDeletionAsync_ReturnsResultsForExactFourteenDayDate()
    {
        using var context = CreateContext();
        var user = await SeedUserAsync(context, "user14@example.com");
        var timeProvider = new TestTimeProvider(new DateTimeOffset(2030, 1, 1, 0, 0, 0, TimeSpan.Zero));
        var now = timeProvider.GetUtcNow().UtcDateTime;

        var inWindow = new ProcessedImage
        {
            UserProfile = user,
            ScheduledDeletionDate = now.AddDays(14),
            IsGenerated = true
        };
        var outWindow = new ProcessedImage
        {
            UserProfile = user,
            ScheduledDeletionDate = now.AddDays(20),
            IsGenerated = true
        };

        context.ProcessedImages.AddRange(inWindow, outWindow);
        await context.SaveChangesAsync();

        var service = CreateService(context, timeProvider: timeProvider);
        var results = await service.GetImagesApproachingDeletionAsync(14, windowSizeDays: 1);

        Assert.True(results.TryGetValue(user.UserId, out var entry));
        Assert.Equal("user14@example.com", entry.Email);
        Assert.Single(entry.Images);
        Assert.Equal(inWindow.Id, entry.Images[0].Id);
    }

    [Fact]
    public async Task GetImagesApproachingDeletionAsync_ReturnsResultsForExactSevenDayDate()
    {
        using var context = CreateContext();
        var user = await SeedUserAsync(context, "user7@example.com");
        var timeProvider = new TestTimeProvider(new DateTimeOffset(2030, 1, 1, 0, 0, 0, TimeSpan.Zero));
        var now = timeProvider.GetUtcNow().UtcDateTime;

        var inWindow = new ProcessedImage
        {
            UserProfile = user,
            ScheduledDeletionDate = now.AddDays(7),
            IsGenerated = true
        };
        var outWindow = new ProcessedImage
        {
            UserProfile = user,
            ScheduledDeletionDate = now.AddDays(12),
            IsGenerated = true
        };

        context.ProcessedImages.AddRange(inWindow, outWindow);
        await context.SaveChangesAsync();

        var service = CreateService(context, timeProvider: timeProvider);
        var results = await service.GetImagesApproachingDeletionAsync(7, windowSizeDays: 1);

        Assert.True(results.TryGetValue(user.UserId, out var entry));
        Assert.Equal("user7@example.com", entry.Email);
        Assert.Single(entry.Images);
        Assert.Equal(inWindow.Id, entry.Images[0].Id);
    }

    [Fact]
    public async Task DeleteExpiredImagesAsync_DeletesEnvironmentPrefixedPrivateRawAsset()
    {
        using var context = CreateContext();
        var user = await SeedUserAsync(context, "private-retention@example.com");
        var timeProvider = new TestTimeProvider(new DateTimeOffset(2030, 1, 1, 0, 0, 0, TimeSpan.Zero));
        var rawPath = $"prod/generated-private/{user.UserId}/preview-raw.png";
        context.ProcessedImages.Add(new ProcessedImage
        {
            UserProfile = user,
            UserProfileId = user.Id,
            OriginalImageUrl = $"prod/uploads/{user.UserId}/source.png",
            ProcessedImageUrl = $"prod/generated/{user.UserId}/preview.png",
            RawImageStoragePath = rawPath,
            IsGenerated = true,
            ScheduledDeletionDate = timeProvider.GetUtcNow().UtcDateTime.AddMinutes(-1)
        });
        await context.SaveChangesAsync();
        var storageService = new Mock<IStorageService>();
        storageService.Setup(service => service.DeleteImageAsync(It.IsAny<string>())).ReturnsAsync(true);
        var service = CreateService(context, storageService.Object, timeProvider);

        var deletedCount = await service.DeleteExpiredImagesAsync();

        Assert.Equal(1, deletedCount);
        storageService.Verify(service => service.DeleteImageAsync(rawPath), Times.Once);
    }

    [Fact]
    public async Task DeleteExpiredImagesAsync_PreservesRawAssetReferencedByLivePromotion()
    {
        using var context = CreateContext();
        var user = await SeedUserAsync(context, "promoted-retention@example.com");
        var timeProvider = new TestTimeProvider(new DateTimeOffset(2030, 1, 1, 0, 0, 0, TimeSpan.Zero));
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var rawPath = $"prod/generated-private/{user.UserId}/preview-raw.png";
        var preview = new ProcessedImage
        {
            UserProfile = user,
            UserProfileId = user.Id,
            RawImageStoragePath = rawPath,
            GenerationMode = "instant_headshot",
            IsGenerated = true,
            ScheduledDeletionDate = now.AddMinutes(-1)
        };
        var promotion = new ProcessedImage
        {
            UserProfile = user,
            UserProfileId = user.Id,
            ProcessedImageUrl = rawPath,
            GenerationMode = "instant_headshot_promoted_preview",
            IsGenerated = true,
            ScheduledDeletionDate = now.AddDays(10)
        };
        context.ProcessedImages.AddRange(preview, promotion);
        await context.SaveChangesAsync();
        var storageService = new Mock<IStorageService>();
        storageService.Setup(service => service.DeleteImageAsync(It.IsAny<string>())).ReturnsAsync(true);
        var service = CreateService(context, storageService.Object, timeProvider);

        var deletedCount = await service.DeleteExpiredImagesAsync();

        Assert.Equal(1, deletedCount);
        Assert.False(await context.ProcessedImages.AnyAsync(value => value.Id == preview.Id));
        Assert.True(await context.ProcessedImages.AnyAsync(value => value.Id == promotion.Id));
        storageService.Verify(service => service.DeleteImageAsync(rawPath), Times.Never);

        timeProvider.Advance(TimeSpan.FromDays(11));
        var finalDeletedCount = await service.DeleteExpiredImagesAsync();

        Assert.Equal(1, finalDeletedCount);
        Assert.False(await context.ProcessedImages.AnyAsync(value => value.Id == promotion.Id));
        storageService.Verify(service => service.DeleteImageAsync(rawPath), Times.Once);
    }

    [Fact]
    public async Task DeleteExpiredImagesAsync_RetainsRecordWhenPrivateAssetDeletionFails()
    {
        using var context = CreateContext();
        var user = await SeedUserAsync(context, "private-retry@example.com");
        var timeProvider = new TestTimeProvider(new DateTimeOffset(2030, 1, 1, 0, 0, 0, TimeSpan.Zero));
        var rawPath = $"prod/generated-private/{user.UserId}/preview-raw.png";
        var image = new ProcessedImage
        {
            UserProfile = user,
            UserProfileId = user.Id,
            RawImageStoragePath = rawPath,
            IsGenerated = true,
            ScheduledDeletionDate = timeProvider.GetUtcNow().UtcDateTime.AddMinutes(-1)
        };
        context.ProcessedImages.Add(image);
        await context.SaveChangesAsync();
        var storageService = new Mock<IStorageService>();
        storageService.Setup(service => service.DeleteImageAsync(rawPath)).ReturnsAsync(false);
        var service = CreateService(context, storageService.Object, timeProvider);

        var deletedCount = await service.DeleteExpiredImagesAsync();

        Assert.Equal(0, deletedCount);
        Assert.True(await context.ProcessedImages.AnyAsync(value => value.Id == image.Id));
        Assert.Empty(context.AdminAuditLogs);
    }

    [Fact]
    public async Task CleanupOrphanedEnhancedImagesAsync_DeletesUnreferencedPrivateRawAsset()
    {
        using var context = CreateContext();
        var timeProvider = new TestTimeProvider(new DateTimeOffset(2030, 1, 1, 0, 0, 0, TimeSpan.Zero));
        var rawPath = "testing/generated-private/orphan/preview-raw.png";
        var storageService = new Mock<IStorageService>();
        storageService.Setup(service => service.ListFilesAsync(It.IsAny<string>()))
            .ReturnsAsync(new List<string>());
        storageService.Setup(service => service.ListFilesAsync("testing/generated-private/"))
            .ReturnsAsync(new List<string> { rawPath });
        storageService.Setup(service => service.GetFileInfoAsync(rawPath)).ReturnsAsync(new StorageFileInfo
        {
            FileName = "preview-raw.png",
            CreatedAt = timeProvider.GetUtcNow().UtcDateTime.AddDays(-2)
        });
        storageService.Setup(service => service.DeleteImageAsync(rawPath)).ReturnsAsync(true);
        var service = CreateService(context, storageService.Object, timeProvider);

        var deletedCount = await service.CleanupOrphanedEnhancedImagesAsync(TimeSpan.FromDays(1));

        Assert.Equal(1, deletedCount);
        storageService.Verify(service => service.DeleteImageAsync(rawPath), Times.Once);
    }

    [Fact]
    public async Task DeleteExpiredImagesAsync_WritesRetentionAuditLog()
    {
        using var context = CreateContext();
        var user = await SeedUserAsync(context, "retention@example.com");
        var timeProvider = new TestTimeProvider(new DateTimeOffset(2030, 1, 1, 0, 0, 0, TimeSpan.Zero));
        var now = timeProvider.GetUtcNow().UtcDateTime;

        var expiredImage = new ProcessedImage
        {
            UserProfile = user,
            UserProfileId = user.Id,
            OriginalImageUrl = "/uploads/user/expired.jpg",
            ProcessedImageUrl = "/uploads/user/expired.jpg",
            IsOriginalUpload = true,
            ScheduledDeletionDate = now.AddDays(-1),
            CreatedAt = now.AddDays(-30)
        };

        context.ProcessedImages.Add(expiredImage);
        await context.SaveChangesAsync();

        var storageService = new Mock<IStorageService>();
        storageService.Setup(service => service.DeleteImageAsync(It.IsAny<string>())).ReturnsAsync(true);

        var service = CreateService(context, storageService.Object, timeProvider);
        var deletedCount = await service.DeleteExpiredImagesAsync();

        Assert.Equal(1, deletedCount);
        var auditLog = await context.AdminAuditLogs.SingleAsync();
        Assert.Equal("system:retention", auditLog.AdminUserId);
        Assert.Equal(user.UserId, auditLog.TargetUserId);
        Assert.Equal("ImageDeletedByRetention", auditLog.Action);
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(options);
    }

    private static RetentionPolicyService CreateService(
        ApplicationDbContext context,
        IStorageService? storageService = null,
        TimeProvider? timeProvider = null)
    {
        storageService ??= new Mock<IStorageService>().Object;
        var replicateClient = new Mock<IReplicateApiClient>().Object;
        var environment = new Mock<IWebHostEnvironment>();
        environment.SetupGet(env => env.EnvironmentName).Returns("Testing");

        var configuration = new ConfigurationBuilder().Build();
        var pathResolver = new StoragePathResolver(environment.Object, configuration, NullLogger<StoragePathResolver>.Instance);

        return new RetentionPolicyService(
            context,
            storageService,
            pathResolver,
            replicateClient,
            NullLogger<RetentionPolicyService>.Instance,
            Options.Create(new LegacyCompatibilityOptions()),
            timeProvider ?? TimeProvider.System);
    }

    private static async Task<UserProfile> SeedUserAsync(ApplicationDbContext context, string email)
    {
        var userId = Guid.NewGuid().ToString();
        var user = new ApplicationUser
        {
            Id = userId,
            UserName = email,
            NormalizedUserName = email.ToUpperInvariant(),
            Email = email,
            NormalizedEmail = email.ToUpperInvariant()
        };

        var profile = new UserProfile
        {
            UserId = userId,
            User = user,
            SubscriptionTier = SubscriptionTier.Basic,
            Credits = 0,
            LastCreditReset = DateTime.UtcNow.AddDays(-1),
            CreatedAt = DateTime.UtcNow.AddMinutes(-10),
            UpdatedAt = DateTime.UtcNow.AddMinutes(-10)
        };

        context.Users.Add(user);
        context.UserProfiles.Add(profile);
        await context.SaveChangesAsync();

        return profile;
    }
}
