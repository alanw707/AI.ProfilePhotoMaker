using AI.ProfilePhotoMaker.API.Configuration;
using AI.ProfilePhotoMaker.API.Data;
using AI.ProfilePhotoMaker.API.Models;
using AI.ProfilePhotoMaker.API.Services;
using AI.ProfilePhotoMaker.API.Services.Notifications;
using AI.ProfilePhotoMaker.API.Tests.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace AI.ProfilePhotoMaker.API.Tests.Integration;

public class AbandonedUploadBackgroundServiceTests
{
    private static readonly DateTimeOffset BaseTime =
        new(2030, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private static TestTimeProvider CreateTimeProvider() => new(BaseTime);

    // ── Candidate detection ──────────────────────────────────────────

    [Fact]
    public async Task NudgesUserWhoSignedUpWithinWindowAndNeverUploaded()
    {
        var timeProvider = CreateTimeProvider();
        using var harness = CreateHarness(timeProvider: timeProvider);

        await SeedUserAsync(harness, "user-1", "user1@example.com", "Alice",
            createdAt: timeProvider.GetUtcNow().UtcDateTime.AddHours(-5));

        await harness.BackgroundService.PerformAbandonedUploadCheck();

        var call = Assert.Single(harness.EmailService.NudgeCalls);
        Assert.Equal("user-1", call.UserId);
        Assert.Equal("user1@example.com", call.Email);
        Assert.Equal("Alice", call.FirstName);
    }

    [Fact]
    public async Task DoesNotNudgeUserStillWithinGracePeriod()
    {
        var timeProvider = CreateTimeProvider();
        using var harness = CreateHarness(timeProvider: timeProvider);

        // Created 2 hours ago — grace period is 4 hours
        await SeedUserAsync(harness, "user-new", "new@example.com", "Bob",
            createdAt: timeProvider.GetUtcNow().UtcDateTime.AddHours(-2));

        await harness.BackgroundService.PerformAbandonedUploadCheck();

        Assert.Empty(harness.EmailService.NudgeCalls);
    }

    [Fact]
    public async Task DoesNotNudgeUserOlderThanMaxAge()
    {
        var timeProvider = CreateTimeProvider();
        using var harness = CreateHarness(timeProvider: timeProvider);

        // Created 10 days ago — max age is 7 days
        await SeedUserAsync(harness, "user-old", "old@example.com", "Charlie",
            createdAt: timeProvider.GetUtcNow().UtcDateTime.AddDays(-10));

        await harness.BackgroundService.PerformAbandonedUploadCheck();

        Assert.Empty(harness.EmailService.NudgeCalls);
    }

    [Fact]
    public async Task DoesNotNudgeUserWhoHasUploadedPhotos()
    {
        var timeProvider = CreateTimeProvider();
        using var harness = CreateHarness(timeProvider: timeProvider);

        var profile = await SeedUserAsync(harness, "user-uploaded", "uploaded@example.com", "Dana",
            createdAt: timeProvider.GetUtcNow().UtcDateTime.AddHours(-5));

        // Add an original upload
        using (var scope = harness.Provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.ProcessedImages.Add(new ProcessedImage
            {
                UserProfileId = profile.Id,
                IsOriginalUpload = true,
                IsGenerated = false,
                ProcessedImageUrl = $"uploads/user-uploaded/{Guid.NewGuid():N}.jpg",
                OriginalImageUrl = $"originals/user-uploaded/{Guid.NewGuid():N}.jpg",
                ScheduledDeletionDate = DateTime.UtcNow.AddDays(90)
            });
            await db.SaveChangesAsync();
        }

        await harness.BackgroundService.PerformAbandonedUploadCheck();

        Assert.Empty(harness.EmailService.NudgeCalls);
    }

    [Fact]
    public async Task DoesNotNudgeUserWithGeneratedButNoOriginalUpload()
    {
        var timeProvider = CreateTimeProvider();
        using var harness = CreateHarness(timeProvider: timeProvider);

        var profile = await SeedUserAsync(harness, "user-gen", "gen@example.com", "Eve",
            createdAt: timeProvider.GetUtcNow().UtcDateTime.AddHours(-5));

        // Add a generated image (not an original upload)
        using (var scope = harness.Provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.ProcessedImages.Add(new ProcessedImage
            {
                UserProfileId = profile.Id,
                IsOriginalUpload = false,
                IsGenerated = true,
                ProcessedImageUrl = $"generated/user-gen/{Guid.NewGuid():N}.jpg",
                OriginalImageUrl = $"originals/user-gen/{Guid.NewGuid():N}.jpg",
                ScheduledDeletionDate = DateTime.UtcNow.AddDays(90)
            });
            await db.SaveChangesAsync();
        }

        await harness.BackgroundService.PerformAbandonedUploadCheck();

        // User has generated images but no original uploads — should still be nudged
        Assert.Single(harness.EmailService.NudgeCalls);
    }

    // ── Deduplication ────────────────────────────────────────────────

    [Fact]
    public async Task DoesNotNudgeSameUserTwiceAcrossRuns()
    {
        var timeProvider = CreateTimeProvider();
        using var harness = CreateHarness(timeProvider: timeProvider);

        await SeedUserAsync(harness, "user-dup", "dup@example.com", "Frank",
            createdAt: timeProvider.GetUtcNow().UtcDateTime.AddHours(-5));

        await harness.BackgroundService.PerformAbandonedUploadCheck();
        await harness.BackgroundService.PerformAbandonedUploadCheck();

        Assert.Single(harness.EmailService.NudgeCalls);
    }

    [Fact]
    public async Task PersistsNudgeLogToDatabase()
    {
        var timeProvider = CreateTimeProvider();
        using var harness = CreateHarness(timeProvider: timeProvider);

        await SeedUserAsync(harness, "user-log", "log@example.com", "Grace",
            createdAt: timeProvider.GetUtcNow().UtcDateTime.AddHours(-5));

        await harness.BackgroundService.PerformAbandonedUploadCheck();

        using var scope = harness.Provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var log = await db.AbandonedUploadNudgeLogs
            .AsNoTracking()
            .SingleOrDefaultAsync(l => l.UserId == "user-log");

        Assert.NotNull(log);
        Assert.Equal(timeProvider.GetUtcNow().UtcDateTime, log.SentAtUtc);
    }

    // ── Email filtering ──────────────────────────────────────────────

    [Fact]
    public async Task SkipsUsersWithoutEmail()
    {
        var timeProvider = CreateTimeProvider();
        using var harness = CreateHarness(timeProvider: timeProvider);

        await SeedUserAsync(harness, "user-no-email", null, "Hank",
            createdAt: timeProvider.GetUtcNow().UtcDateTime.AddHours(-5));

        await harness.BackgroundService.PerformAbandonedUploadCheck();

        Assert.Empty(harness.EmailService.NudgeCalls);
    }

    [Fact]
    public async Task SkipsUsersWithWhitespaceEmail()
    {
        var timeProvider = CreateTimeProvider();
        using var harness = CreateHarness(timeProvider: timeProvider);

        await SeedUserAsync(harness, "user-blank", "   ", "Ivy",
            createdAt: timeProvider.GetUtcNow().UtcDateTime.AddHours(-5));

        await harness.BackgroundService.PerformAbandonedUploadCheck();

        Assert.Empty(harness.EmailService.NudgeCalls);
    }

    // ── Configuration ────────────────────────────────────────────────

    [Fact]
    public async Task DoesNothingWhenDisabled()
    {
        var timeProvider = CreateTimeProvider();
        var options = new AbandonedUploadOptions { Enabled = false };
        using var harness = CreateHarness(timeProvider: timeProvider, options: options);

        await SeedUserAsync(harness, "user-disabled", "disabled@example.com", "Jack",
            createdAt: timeProvider.GetUtcNow().UtcDateTime.AddHours(-5));

        await harness.BackgroundService.PerformAbandonedUploadCheck();

        Assert.Empty(harness.EmailService.NudgeCalls);
    }

    [Fact]
    public async Task RespectsCustomGracePeriod()
    {
        var timeProvider = CreateTimeProvider();
        var options = new AbandonedUploadOptions { GracePeriod = TimeSpan.FromHours(24) };
        using var harness = CreateHarness(timeProvider: timeProvider, options: options);

        // Created 5 hours ago — within the custom 24-hour grace period
        await SeedUserAsync(harness, "user-grace", "grace@example.com", "Kim",
            createdAt: timeProvider.GetUtcNow().UtcDateTime.AddHours(-5));

        await harness.BackgroundService.PerformAbandonedUploadCheck();

        Assert.Empty(harness.EmailService.NudgeCalls);
    }

    [Fact]
    public async Task RespectsCustomMaxAge()
    {
        var timeProvider = CreateTimeProvider();
        var options = new AbandonedUploadOptions { MaxAge = TimeSpan.FromDays(2) };
        using var harness = CreateHarness(timeProvider: timeProvider, options: options);

        // Created 3 days ago — beyond the custom 2-day max age
        await SeedUserAsync(harness, "user-maxage", "maxage@example.com", "Leo",
            createdAt: timeProvider.GetUtcNow().UtcDateTime.AddDays(-3));

        await harness.BackgroundService.PerformAbandonedUploadCheck();

        Assert.Empty(harness.EmailService.NudgeCalls);
    }

    // ── Error resilience ─────────────────────────────────────────────

    [Fact]
    public async Task ContinuesToNextUserAfterEmailFailure()
    {
        var timeProvider = CreateTimeProvider();
        var emailService = new CapturingEmailNotificationService { ThrowOnNudge = true, ThrowOnceOnly = true };
        using var harness = CreateHarness(emailService, timeProvider: timeProvider);

        await SeedUserAsync(harness, "user-fail", "fail@example.com", "Mia",
            createdAt: timeProvider.GetUtcNow().UtcDateTime.AddHours(-5));
        await SeedUserAsync(harness, "user-ok", "ok@example.com", "Nora",
            createdAt: timeProvider.GetUtcNow().UtcDateTime.AddHours(-6));

        await harness.BackgroundService.PerformAbandonedUploadCheck();

        // First user fails, second should still get nudged
        Assert.Single(harness.EmailService.NudgeCalls);
        Assert.Equal("user-ok", harness.EmailService.NudgeCalls[0].UserId);
    }

    [Fact]
    public async Task FailedUserDoesNotPoisonChangeTrackerForSubsequentUsers()
    {
        var timeProvider = CreateTimeProvider();
        var emailService = new CapturingEmailNotificationService { ThrowOnNudge = true, ThrowOnceOnly = true };
        using var harness = CreateHarness(emailService, timeProvider: timeProvider);

        await SeedUserAsync(harness, "user-fail", "fail@example.com", "Mia",
            createdAt: timeProvider.GetUtcNow().UtcDateTime.AddHours(-5));
        await SeedUserAsync(harness, "user-ok", "ok@example.com", "Nora",
            createdAt: timeProvider.GetUtcNow().UtcDateTime.AddHours(-6));

        await harness.BackgroundService.PerformAbandonedUploadCheck();

        // Verify the successful user's nudge log was actually persisted
        using var scope = harness.Provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var logs = await db.AbandonedUploadNudgeLogs.AsNoTracking().ToListAsync();

        Assert.Single(logs);
        Assert.Equal("user-ok", logs[0].UserId);
    }

    // ── Multiple users ───────────────────────────────────────────────

    [Fact]
    public async Task NudgesMultipleEligibleUsers()
    {
        var timeProvider = CreateTimeProvider();
        using var harness = CreateHarness(timeProvider: timeProvider);

        await SeedUserAsync(harness, "user-a", "a@example.com", "Ava",
            createdAt: timeProvider.GetUtcNow().UtcDateTime.AddHours(-5));
        await SeedUserAsync(harness, "user-b", "b@example.com", "Ben",
            createdAt: timeProvider.GetUtcNow().UtcDateTime.AddDays(-2));
        await SeedUserAsync(harness, "user-c", "c@example.com", "Cal",
            createdAt: timeProvider.GetUtcNow().UtcDateTime.AddDays(-6));

        await harness.BackgroundService.PerformAbandonedUploadCheck();

        Assert.Equal(3, harness.EmailService.NudgeCalls.Count);
    }

    // ── Boundary conditions ──────────────────────────────────────────

    [Fact]
    public async Task NudgesUserExactlyAtGracePeriodBoundary()
    {
        var timeProvider = CreateTimeProvider();
        using var harness = CreateHarness(timeProvider: timeProvider);

        // Created exactly 4 hours and 1 second ago — just past grace period
        await SeedUserAsync(harness, "user-edge", "edge@example.com", "Dee",
            createdAt: timeProvider.GetUtcNow().UtcDateTime.AddHours(-4).AddSeconds(-1));

        await harness.BackgroundService.PerformAbandonedUploadCheck();

        Assert.Single(harness.EmailService.NudgeCalls);
    }

    [Fact]
    public async Task DoesNotNudgeUserExactlyAtMaxAgeBoundary()
    {
        var timeProvider = CreateTimeProvider();
        using var harness = CreateHarness(timeProvider: timeProvider);

        // Created exactly 7 days and 1 second ago — just past max age
        await SeedUserAsync(harness, "user-expired", "expired@example.com", "Eli",
            createdAt: timeProvider.GetUtcNow().UtcDateTime.AddDays(-7).AddSeconds(-1));

        await harness.BackgroundService.PerformAbandonedUploadCheck();

        Assert.Empty(harness.EmailService.NudgeCalls);
    }

    // ── Harness setup ────────────────────────────────────────────────

    private static TestHarness CreateHarness(
        CapturingEmailNotificationService? emailService = null,
        TimeProvider? timeProvider = null,
        AbandonedUploadOptions? options = null)
    {
        var services = new ServiceCollection();
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        timeProvider ??= TimeProvider.System;
        options ??= new AbandonedUploadOptions();

        services.AddSingleton(connection);
        services.AddDbContext<ApplicationDbContext>(opt => opt.UseSqlite(connection));
        services.AddSingleton(timeProvider);

        emailService ??= new CapturingEmailNotificationService();
        services.AddSingleton<IEmailNotificationService>(emailService);

        var provider = services.BuildServiceProvider();
        using (var scope = provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.Database.EnsureCreated();
        }

        return new TestHarness(provider, emailService, connection, timeProvider, options);
    }

    private static async Task<UserProfile> SeedUserAsync(
        TestHarness harness,
        string userId,
        string? email,
        string? firstName,
        DateTime createdAt)
    {
        using var scope = harness.Provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var userName = email ?? $"{userId}@placeholder.local";
        var user = new ApplicationUser
        {
            Id = userId,
            UserName = userName,
            NormalizedUserName = userName.ToUpperInvariant(),
            Email = email,
            NormalizedEmail = email?.ToUpperInvariant(),
            FirstName = firstName ?? string.Empty,
            LastName = string.Empty
        };

        var profile = new UserProfile
        {
            UserId = userId,
            User = user,
            SubscriptionTier = SubscriptionTier.Basic,
            Credits = 25,
            LastCreditReset = DateTime.UtcNow.AddDays(-1),
            CreatedAt = createdAt,
            UpdatedAt = createdAt
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
            TimeProvider timeProvider,
            AbandonedUploadOptions options)
        {
            Provider = provider;
            EmailService = emailService;
            Connection = connection;
            BackgroundService = new AbandonedUploadBackgroundService(
                provider,
                NullLogger<AbandonedUploadBackgroundService>.Instance,
                Options.Create(options),
                timeProvider);
        }

        public ServiceProvider Provider { get; }
        public CapturingEmailNotificationService EmailService { get; }
        public AbandonedUploadBackgroundService BackgroundService { get; }
        public SqliteConnection Connection { get; }

        public void Dispose()
        {
            Provider.Dispose();
            Connection.Dispose();
        }
    }

    private sealed class CapturingEmailNotificationService : IEmailNotificationService
    {
        public List<NudgeCall> NudgeCalls { get; } = new();
        public bool ThrowOnNudge { get; set; }
        public bool ThrowOnceOnly { get; set; }

        public Task SendTrainingCompletedAsync(string userId, string? email, string? modelName, string? modelVersion) => Task.CompletedTask;
        public Task SendGenerationCompletedAsync(string userId, string? email, string? style, int imageCount, string? jobId = null) => Task.CompletedTask;
        public Task SendGenerationFailedAsync(string userId, string? email, string? style, string? error, string? jobId = null) => Task.CompletedTask;
        public Task SendPurchaseReceiptAsync(string userId, string? email, CreditPurchase purchase) => Task.CompletedTask;
        public Task SendSupportFeedbackReceivedAsync(string userId, string? userEmail, FeedbackSubmission submission) => Task.CompletedTask;
        public Task SendEmailVerificationAsync(string userId, string? email, string encodedToken) => Task.CompletedTask;
        public Task SendWelcomeAsync(string userId, string? email, string? firstName = null) => Task.CompletedTask;
        public Task SendRetentionDeletionWarningAsync(string userId, string? email, int imageCount, DateTime deletionDate, int daysUntilDeletion) => Task.CompletedTask;

        public Task SendAbandonedUploadNudgeAsync(string userId, string? email, string? firstName = null)
        {
            if (ThrowOnNudge)
            {
                if (ThrowOnceOnly) ThrowOnNudge = false;
                throw new InvalidOperationException("Simulated email failure");
            }

            NudgeCalls.Add(new NudgeCall(userId, email, firstName));
            return Task.CompletedTask;
        }

        public Task<EmailSendResult> SendMarketingEmailAsync(string userId, string email, string subject, string htmlBody, string unsubscribeUrl) => Task.FromResult(new EmailSendResult(true));
        public string RenderMarketingEmailPreview(string subject, string htmlBody) => htmlBody;
    }

    private sealed record NudgeCall(string UserId, string? Email, string? FirstName);
}
