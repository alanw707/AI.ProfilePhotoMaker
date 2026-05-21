using AI.ProfilePhotoMaker.API.Configuration;
using AI.ProfilePhotoMaker.API.Data;
using AI.ProfilePhotoMaker.API.Models;
using AI.ProfilePhotoMaker.API.Services;
using AI.ProfilePhotoMaker.API.Services.ImageProcessing;
using AI.ProfilePhotoMaker.API.Services.Notifications;
using AI.ProfilePhotoMaker.API.Services.Storage;
using AI.ProfilePhotoMaker.API.Tests.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace AI.ProfilePhotoMaker.API.Tests.Integration;

public class RetentionPolicyBackgroundServiceTests
{
    private static readonly DateTimeOffset BaseTime =
        new(2030, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private static TestTimeProvider CreateTimeProvider() => new(BaseTime);

    [Fact]
    public async Task ExecuteAsync_RunsCheckLoopAndStopsOnCancellation()
    {
        var trackingService = new TrackingRetentionPolicyService();
        var options = new RetentionPolicyBackgroundServiceOptions
        {
            InitialDelay = TimeSpan.Zero,
            CheckInterval = TimeSpan.FromHours(1),
            ErrorRetryDelay = TimeSpan.FromMinutes(1)
        };

        using var harness = CreateSchedulerHarness(trackingService, options);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        await harness.BackgroundService.StartAsync(cts.Token);

        await trackingService.FirstCheck.Task.WaitAsync(TimeSpan.FromSeconds(1));

        await harness.BackgroundService.StopAsync(CancellationToken.None);

        Assert.True(trackingService.CheckCount > 0);
    }
    [Fact]
    public async Task PerformRetentionPolicyCheck_SendsFourteenDayNotificationForUser()
    {
        var timeProvider = CreateTimeProvider();
        using var harness = CreateHarness(timeProvider: timeProvider);
        var deletionDate = timeProvider.GetUtcNow().UtcDateTime.AddDays(14);

        await SeedUserWithImagesAsync(harness, "user-14", "user14@example.com", deletionDate, imageCount: 2);

        await harness.BackgroundService.PerformRetentionPolicyCheck();

        var call = Assert.Single(harness.EmailService.RetentionWarningCalls);
        Assert.Equal("user-14", call.UserId);
        Assert.Equal("user14@example.com", call.Email);
        Assert.Equal(2, call.ImageCount);
        Assert.Equal(14, call.DaysUntilDeletion);
        Assert.Equal(deletionDate.Date, call.DeletionDate.Date);
    }

    [Fact]
    public async Task PerformRetentionPolicyCheck_SendsSevenDayNotificationForUser()
    {
        var timeProvider = CreateTimeProvider();
        using var harness = CreateHarness(timeProvider: timeProvider);
        var deletionDate = timeProvider.GetUtcNow().UtcDateTime.AddDays(7);

        await SeedUserWithImagesAsync(harness, "user-7", "user7@example.com", deletionDate, imageCount: 1);

        await harness.BackgroundService.PerformRetentionPolicyCheck();

        var call = Assert.Single(harness.EmailService.RetentionWarningCalls);
        Assert.Equal("user-7", call.UserId);
        Assert.Equal(7, call.DaysUntilDeletion);
    }

    [Fact]
    public async Task PerformRetentionPolicyCheck_SendsBothNotificationsAcrossRuns()
    {
        var timeProvider = CreateTimeProvider();
        using var harness = CreateHarness(timeProvider: timeProvider);
        var userId = "user-both";
        var initialDeletionDate = timeProvider.GetUtcNow().UtcDateTime.AddDays(14);

        await SeedUserWithImagesAsync(harness, userId, "userboth@example.com", initialDeletionDate, imageCount: 1);

        await harness.BackgroundService.PerformRetentionPolicyCheck();

        Assert.Single(harness.EmailService.RetentionWarningCalls);
        Assert.Equal(14, harness.EmailService.RetentionWarningCalls[0].DaysUntilDeletion);

        timeProvider.Advance(TimeSpan.FromDays(7));

        await harness.BackgroundService.PerformRetentionPolicyCheck();

        Assert.Equal(2, harness.EmailService.RetentionWarningCalls.Count);
        Assert.Contains(harness.EmailService.RetentionWarningCalls, call => call.DaysUntilDeletion == 14);
        Assert.Contains(harness.EmailService.RetentionWarningCalls, call => call.DaysUntilDeletion == 7);
    }

    [Fact]
    public async Task PerformRetentionPolicyCheck_DoesNotSendDuplicateEmailsForSameWindow()
    {
        var timeProvider = CreateTimeProvider();
        using var harness = CreateHarness(timeProvider: timeProvider);
        var deletionDate = timeProvider.GetUtcNow().UtcDateTime.AddDays(14);

        await SeedUserWithImagesAsync(harness, "user-dup", "dup@example.com", deletionDate, imageCount: 3);

        await harness.BackgroundService.PerformRetentionPolicyCheck();

        var call = Assert.Single(harness.EmailService.RetentionWarningCalls);
        Assert.Equal(3, call.ImageCount);
    }

    [Fact]
    public async Task PerformRetentionPolicyCheck_DoesNotSendDuplicateEmailsAcrossRuns()
    {
        var timeProvider = CreateTimeProvider();
        using var harness = CreateHarness(timeProvider: timeProvider);
        var deletionDate = timeProvider.GetUtcNow().UtcDateTime.AddDays(14);

        await SeedUserWithImagesAsync(harness, "user-dup-run", "dup-run@example.com", deletionDate, imageCount: 1);

        await harness.BackgroundService.PerformRetentionPolicyCheck();
        await harness.BackgroundService.PerformRetentionPolicyCheck();

        Assert.Single(harness.EmailService.RetentionWarningCalls);
    }

    [Fact]
    public async Task PerformRetentionPolicyCheck_OnlyMatchesExactFourteenDayDate()
    {
        var timeProvider = CreateTimeProvider();
        using var harness = CreateHarness(timeProvider: timeProvider);
        var baseTime = timeProvider.GetUtcNow().UtcDateTime;

        await SeedUserWithImagesAsync(harness, "user-14", "user14@example.com", baseTime.AddDays(14), imageCount: 1);
        await SeedUserWithImagesAsync(harness, "user-15", "user15@example.com", baseTime.AddDays(15), imageCount: 1);

        await harness.BackgroundService.PerformRetentionPolicyCheck();

        var fourteenDayCalls = harness.EmailService.RetentionWarningCalls
            .Where(call => call.DaysUntilDeletion == 14)
            .ToList();

        Assert.Single(fourteenDayCalls);
        Assert.Contains(fourteenDayCalls, call => call.UserId == "user-14");
    }

    [Fact]
    public async Task PerformRetentionPolicyCheck_OnlyMatchesExactSevenDayDate()
    {
        var timeProvider = CreateTimeProvider();
        using var harness = CreateHarness(timeProvider: timeProvider);
        var baseTime = timeProvider.GetUtcNow().UtcDateTime;

        await SeedUserWithImagesAsync(harness, "user-7", "user7@example.com", baseTime.AddDays(7), imageCount: 1);
        await SeedUserWithImagesAsync(harness, "user-8", "user8@example.com", baseTime.AddDays(8), imageCount: 1);

        await harness.BackgroundService.PerformRetentionPolicyCheck();

        var sevenDayCalls = harness.EmailService.RetentionWarningCalls
            .Where(call => call.DaysUntilDeletion == 7)
            .ToList();

        Assert.Single(sevenDayCalls);
        Assert.Contains(sevenDayCalls, call => call.UserId == "user-7");
    }

    [Fact]
    public async Task PerformRetentionPolicyCheck_SkipsUsersWithoutEmail()
    {
        var timeProvider = CreateTimeProvider();
        using var harness = CreateHarness(timeProvider: timeProvider);
        var deletionDate = timeProvider.GetUtcNow().UtcDateTime.AddDays(7);

        await SeedUserWithImagesAsync(harness, "user-no-email", null, deletionDate, imageCount: 1);

        await harness.BackgroundService.PerformRetentionPolicyCheck();

        Assert.Empty(harness.EmailService.RetentionWarningCalls);
    }

    [Fact]
    public async Task PerformRetentionPolicyCheck_SkipsWhitespaceEmail()
    {
        var timeProvider = CreateTimeProvider();
        using var harness = CreateHarness(timeProvider: timeProvider);
        var deletionDate = timeProvider.GetUtcNow().UtcDateTime.AddDays(7);

        await SeedUserWithImagesAsync(harness, "user-blank-email", "   ", deletionDate, imageCount: 1);

        await harness.BackgroundService.PerformRetentionPolicyCheck();

        Assert.Empty(harness.EmailService.RetentionWarningCalls);
    }

    [Fact]
    public async Task PerformRetentionPolicyCheck_DoesNotBlockDeletionWhenEmailFails()
    {
        var emailService = new CapturingEmailNotificationService { ThrowOnRetentionWarning = true };
        var timeProvider = CreateTimeProvider();
        using var harness = CreateHarness(emailService, timeProvider: timeProvider);
        var userId = "user-fail";
        var warningDeletionDate = timeProvider.GetUtcNow().UtcDateTime.AddDays(14);

        await SeedUserWithImagesAsync(harness, userId, "fail@example.com", warningDeletionDate, imageCount: 1);

        using (var scope = harness.Provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var profile = await db.UserProfiles.FirstAsync(p => p.UserId == userId);
            db.ProcessedImages.Add(new ProcessedImage
            {
                UserProfileId = profile.Id,
                UserProfile = profile,
                IsGenerated = true,
                ProcessedImageUrl = $"generated/{userId}/{Guid.NewGuid():N}.jpg",
                OriginalImageUrl = $"uploads/{userId}/{Guid.NewGuid():N}.jpg",
                ScheduledDeletionDate = timeProvider.GetUtcNow().UtcDateTime.AddDays(-1)
            });
            await db.SaveChangesAsync();
        }

        await harness.BackgroundService.PerformRetentionPolicyCheck();

        using (var scope = harness.Provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var remainingExpired = await db.ProcessedImages
                .Where(img => img.ScheduledDeletionDate <= timeProvider.GetUtcNow().UtcDateTime)
                .ToListAsync();

            Assert.Empty(remainingExpired);
        }
    }

    private static TestHarness CreateHarness(
        CapturingEmailNotificationService? emailService = null,
        TimeProvider? timeProvider = null)
    {
        var services = new ServiceCollection();
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        var environment = new Mock<IWebHostEnvironment>();
        environment.SetupGet(env => env.EnvironmentName).Returns("Testing");
        timeProvider ??= TimeProvider.System;

        services.AddSingleton(connection);
        services.AddDbContext<ApplicationDbContext>(options => options.UseSqlite(connection));
        services.AddSingleton<IWebHostEnvironment>(environment.Object);
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.AddSingleton<ILogger<StoragePathResolver>>(NullLogger<StoragePathResolver>.Instance);
        services.AddSingleton<StoragePathResolver>();
        services.AddSingleton<IStorageService, NoopStorageService>();
        services.AddSingleton<IReplicateApiClient>(new Mock<IReplicateApiClient>().Object);
        services.AddSingleton<IOptions<LegacyCompatibilityOptions>>(Options.Create(new LegacyCompatibilityOptions()));
        services.AddSingleton<ILogger<RetentionPolicyService>>(NullLogger<RetentionPolicyService>.Instance);
        services.AddSingleton(timeProvider);
        services.AddScoped<IRetentionPolicyService, RetentionPolicyService>();

        emailService ??= new CapturingEmailNotificationService();
        services.AddSingleton<IEmailNotificationService>(emailService);

        var provider = services.BuildServiceProvider();
        using (var scope = provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.Database.EnsureCreated();
        }

        return new TestHarness(provider, emailService, connection, timeProvider);
    }

    private static SchedulerHarness CreateSchedulerHarness(
        TrackingRetentionPolicyService retentionService,
        RetentionPolicyBackgroundServiceOptions options)
    {
        var services = new ServiceCollection();
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();

        services.AddSingleton(connection);
        services.AddDbContext<ApplicationDbContext>(dbOptions => dbOptions.UseSqlite(connection));
        services.AddSingleton<IRetentionPolicyService>(retentionService);
        services.AddSingleton<IEmailNotificationService, CapturingEmailNotificationService>();
        var timeProvider = TimeProvider.System;

        var provider = services.BuildServiceProvider();
        using (var scope = provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.Database.EnsureCreated();
        }

        return new SchedulerHarness(
            provider,
            retentionService,
            new RetentionPolicyBackgroundService(
                provider,
                NullLogger<RetentionPolicyBackgroundService>.Instance,
                Options.Create(options),
                Options.Create(new RetentionNotificationOptions()),
                timeProvider),
            connection);
    }

    private static async Task SeedUserWithImagesAsync(
        TestHarness harness,
        string userId,
        string? email,
        DateTime deletionDate,
        int imageCount)
    {
        using var scope = harness.Provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var profile = await SeedUserAsync(db, userId, email);

        var images = new List<ProcessedImage>();
        for (var i = 0; i < imageCount; i++)
        {
            images.Add(new ProcessedImage
            {
                UserProfileId = profile.Id,
                UserProfile = profile,
                IsGenerated = true,
                ProcessedImageUrl = $"generated/{userId}/{Guid.NewGuid():N}.jpg",
                OriginalImageUrl = $"uploads/{userId}/{Guid.NewGuid():N}.jpg",
                ScheduledDeletionDate = deletionDate
            });
        }

        db.ProcessedImages.AddRange(images);
        await db.SaveChangesAsync();
    }

    private static async Task<UserProfile> SeedUserAsync(ApplicationDbContext db, string userId, string? email)
    {
        var userName = email ?? $"{userId}@example.com";
        var user = new ApplicationUser
        {
            Id = userId,
            UserName = userName,
            NormalizedUserName = userName.ToUpperInvariant(),
            Email = email,
            NormalizedEmail = email?.ToUpperInvariant()
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

        db.Users.Add(user);
        db.UserProfiles.Add(profile);
        await db.SaveChangesAsync();

        return profile;
    }

    private sealed class TestHarness : IDisposable
    {
        public TestHarness(
            ServiceProvider provider,
            CapturingEmailNotificationService emailService,
            SqliteConnection connection,
            TimeProvider timeProvider)
        {
            Provider = provider;
            EmailService = emailService;
            Connection = connection;
            TimeProvider = timeProvider;
            BackgroundService = new RetentionPolicyBackgroundService(
                provider,
                NullLogger<RetentionPolicyBackgroundService>.Instance,
                Options.Create(new RetentionPolicyBackgroundServiceOptions()),
                Options.Create(new RetentionNotificationOptions()),
                timeProvider);
        }

        public ServiceProvider Provider { get; }
        public CapturingEmailNotificationService EmailService { get; }
        public RetentionPolicyBackgroundService BackgroundService { get; }
        public SqliteConnection Connection { get; }
        public TimeProvider TimeProvider { get; }

        public void Dispose()
        {
            Provider.Dispose();
            Connection.Dispose();
        }
    }

    private sealed class SchedulerHarness : IDisposable
    {
        public SchedulerHarness(
            ServiceProvider provider,
            TrackingRetentionPolicyService retentionService,
            RetentionPolicyBackgroundService backgroundService,
            SqliteConnection connection)
        {
            Provider = provider;
            RetentionService = retentionService;
            BackgroundService = backgroundService;
            Connection = connection;
        }

        public ServiceProvider Provider { get; }
        public TrackingRetentionPolicyService RetentionService { get; }
        public RetentionPolicyBackgroundService BackgroundService { get; }
        public SqliteConnection Connection { get; }

        public void Dispose()
        {
            Provider.Dispose();
            Connection.Dispose();
        }
    }

    private sealed class TrackingRetentionPolicyService : IRetentionPolicyService
    {
        public TaskCompletionSource<bool> FirstCheck { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int CheckCount { get; private set; }

        public Task<bool> RequestImageDeletionAsync(int imageId, string userId) => Task.FromResult(false);
        public Task<int> RequestAllImagesDeletionAsync(string userId) => Task.FromResult(0);
        public Task<List<ProcessedImage>> GetImagesScheduledForDeletionAsync(string userId) => Task.FromResult(new List<ProcessedImage>());
        public Task<bool> RestoreImageAsync(int imageId, string userId) => Task.FromResult(false);
        public Task<ProcessedImage?> GetImageRetentionInfoAsync(int imageId, string userId) => Task.FromResult<ProcessedImage?>(null);
        public Task<int> DeleteExpiredImagesAsync() => Task.FromResult(0);
        public Task<List<ProcessedImageCleanupResult>> GetExpiredImagesAsync() => Task.FromResult(new List<ProcessedImageCleanupResult>());

        public Task SetRetentionDatesForExistingImagesAsync()
        {
            CheckCount++;
            FirstCheck.TrySetResult(true);
            return Task.CompletedTask;
        }

        public Task<int> CleanupOrphanedEnhancedImagesAsync(TimeSpan maxAge) => Task.FromResult(0);

        public Task<Dictionary<string, (string? Email, List<ProcessedImage> Images)>> GetImagesApproachingDeletionAsync(
            int daysBeforeDeletion,
            int windowSizeDays = 1)
        {
            return Task.FromResult(new Dictionary<string, (string? Email, List<ProcessedImage> Images)>(StringComparer.OrdinalIgnoreCase));
        }
    }

    private sealed class CapturingEmailNotificationService : IEmailNotificationService
    {
        public List<RetentionWarningCall> RetentionWarningCalls { get; } = new();
        public bool ThrowOnRetentionWarning { get; set; }

        public Task SendTrainingCompletedAsync(string userId, string? email, string? modelName, string? modelVersion) => Task.CompletedTask;
        public Task SendGenerationCompletedAsync(string userId, string? email, string? style, int imageCount, string? jobId = null) => Task.CompletedTask;
        public Task SendGenerationFailedAsync(string userId, string? email, string? style, string? error, string? jobId = null) => Task.CompletedTask;
        public Task SendPurchaseReceiptAsync(string userId, string? email, CreditPurchase purchase) => Task.CompletedTask;
        public Task SendSupportFeedbackReceivedAsync(string userId, string? userEmail, FeedbackSubmission submission) => Task.CompletedTask;
        public Task SendEmailVerificationAsync(string userId, string? email, string encodedToken) => Task.CompletedTask;
        public Task SendWelcomeAsync(string userId, string? email, string? firstName = null) => Task.CompletedTask;

        public Task SendRetentionDeletionWarningAsync(string userId, string? email, int imageCount, DateTime deletionDate, int daysUntilDeletion)
        {
            RetentionWarningCalls.Add(new RetentionWarningCall(userId, email, imageCount, deletionDate, daysUntilDeletion));

            if (ThrowOnRetentionWarning)
            {
                throw new InvalidOperationException("Simulated email failure");
            }

            return Task.CompletedTask;
        }

        public Task SendAbandonedUploadNudgeAsync(string userId, string? email, string? firstName = null, int uploadedCount = 0, int minimumRequiredUploads = 5) => Task.CompletedTask;
        public Task<EmailSendResult> SendMarketingEmailAsync(string userId, string email, string subject, string htmlBody, string unsubscribeUrl) => Task.FromResult(new EmailSendResult(true));
        public string RenderMarketingEmailPreview(string subject, string htmlBody) => htmlBody;
    }

    private sealed record RetentionWarningCall(
        string UserId,
        string? Email,
        int ImageCount,
        DateTime DeletionDate,
        int DaysUntilDeletion);

    private sealed class NoopStorageService : IStorageService
    {
        public Task<string> SaveImageAsync(Stream imageStream, string fileName, string userId, string folderType = "generated")
            => Task.FromResult($"noop/{userId}/{folderType}/{fileName}");

        public Task<string> SaveImageToPathAsync(Stream imageStream, string storagePath)
            => Task.FromResult(storagePath);

        public Task<Stream?> GetImageAsync(string storagePath)
            => Task.FromResult<Stream?>(null);

        public Task<bool> DeleteImageAsync(string storagePath)
            => Task.FromResult(true);

        public Task<bool> ExistsAsync(string storagePath)
            => Task.FromResult(false);

        public string GetImageUrl(string storagePath)
            => storagePath;

        public Task<List<string>> ListUserImagesAsync(string userId)
            => Task.FromResult(new List<string>());

        public Task<StorageFileInfo?> GetFileInfoAsync(string storagePath)
            => Task.FromResult<StorageFileInfo?>(null);

        public Task<string> GenerateSasUrlAsync(string storagePath, TimeSpan expiry, Azure.Storage.Sas.BlobSasPermissions permissions = Azure.Storage.Sas.BlobSasPermissions.Read)
            => Task.FromResult(storagePath);

        public Task<string> SaveZipAsync(Stream zipStream, string storagePath)
            => Task.FromResult(storagePath);

        public Task<bool> DeleteDirectoryAsync(string directoryPath)
            => Task.FromResult(true);

        public Task<List<string>> ListFilesAsync(string prefix)
            => Task.FromResult(new List<string>());
    }
}
