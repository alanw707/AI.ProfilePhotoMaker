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

        var service = CreateService(context, timeProvider);
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

        var service = CreateService(context, timeProvider);
        var results = await service.GetImagesApproachingDeletionAsync(7, windowSizeDays: 1);

        Assert.True(results.TryGetValue(user.UserId, out var entry));
        Assert.Equal("user7@example.com", entry.Email);
        Assert.Single(entry.Images);
        Assert.Equal(inWindow.Id, entry.Images[0].Id);
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(options);
    }

    private static RetentionPolicyService CreateService(ApplicationDbContext context, TimeProvider? timeProvider = null)
    {
        var storageService = new Mock<IStorageService>().Object;
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
            PurchasedCredits = 0,
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
