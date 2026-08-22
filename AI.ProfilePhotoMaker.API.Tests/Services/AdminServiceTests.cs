using AI.ProfilePhotoMaker.API.Data;
using AI.ProfilePhotoMaker.API.Models;
using AI.ProfilePhotoMaker.API.Models.DTOs;
using AI.ProfilePhotoMaker.API.Services;
using AI.ProfilePhotoMaker.API.Services.Storage;
using AI.ProfilePhotoMaker.API.Tests.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace AI.ProfilePhotoMaker.API.Tests.Services;

public class AdminServiceTests
{
    [Fact]
    public async Task AdjustCredits_PreventsNegativeBalance()
    {
        using var context = CreateContext();
        var userId = await SeedUserWithProfileAsync(context, credits: 2);
        var userManager = UserManagerMockFactory.Create();
        var service = CreateService(context, userManager.Object);

        var result = await service.AdjustCreditsAsync(new AdminCreditAdjustmentDto
        {
            UserId = userId,
            Amount = -5,
            Reason = "manual"
        }, "admin-1");

        Assert.False(result.Success);
        Assert.Contains("cannot go negative", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AdjustCredits_WritesAuditLog()
    {
        using var context = CreateContext();
        var userId = await SeedUserWithProfileAsync(context, credits: 10);
        var userManager = UserManagerMockFactory.Create();
        var service = CreateService(context, userManager.Object);

        var result = await service.AdjustCreditsAsync(new AdminCreditAdjustmentDto
        {
            UserId = userId,
            Amount = 5,
            Reason = "manual"
        }, "admin-1");

        Assert.True(result.Success);

        var log = await context.AdminAuditLogs.FirstOrDefaultAsync();
        Assert.NotNull(log);
        Assert.Equal("CreditsAdded", log!.Action);
    }

    [Fact]
    public async Task DeactivateUser_SetsLockoutEndToMaxValue()
    {
        using var context = CreateContext();
        var user = new ApplicationUser { Id = "u1", UserName = "u1", Email = "u1@test.com" };
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var userManager = UserManagerMockFactory.Create();
        userManager.Setup(m => m.FindByIdAsync("u1")).ReturnsAsync(user);
        userManager.Setup(m => m.SetLockoutEnabledAsync(user, true)).ReturnsAsync(IdentityResult.Success);
        userManager.Setup(m => m.SetLockoutEndDateAsync(user, DateTimeOffset.MaxValue)).ReturnsAsync(IdentityResult.Success);

        var service = CreateService(context, userManager.Object);
        var ok = await service.DeactivateUserAsync("u1", "admin-1", "policy");

        Assert.True(ok);
        userManager.Verify(m => m.SetLockoutEndDateAsync(user, DateTimeOffset.MaxValue), Times.Once);
    }

    [Fact]
    public async Task DeactivateUser_RejectsSelfDeactivation()
    {
        using var context = CreateContext();
        var userManager = UserManagerMockFactory.Create();
        var service = CreateService(context, userManager.Object);

        var ok = await service.DeactivateUserAsync("admin-1", "admin-1", "self");
        Assert.False(ok);
    }

    [Fact]
    public async Task ReactivateUser_ClearsLockoutEnd()
    {
        using var context = CreateContext();
        var user = new ApplicationUser { Id = "u1", UserName = "u1", Email = "u1@test.com" };
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var userManager = UserManagerMockFactory.Create();
        userManager.Setup(m => m.FindByIdAsync("u1")).ReturnsAsync(user);
        userManager.Setup(m => m.SetLockoutEndDateAsync(user, null)).ReturnsAsync(IdentityResult.Success);

        var service = CreateService(context, userManager.Object);
        var ok = await service.ReactivateUserAsync("u1", "admin-1", "ok");

        Assert.True(ok);
        userManager.Verify(m => m.SetLockoutEndDateAsync(user, null), Times.Once);
    }

    [Fact]
    public async Task DeleteUser_CascadeDeletesRelatedData()
    {
        using var context = CreateContext();
        var userId = await SeedUserWithProfileAsync(context, credits: 5);
        var profile = await context.UserProfiles.FirstAsync(p => p.UserId == userId);
        context.ProcessedImages.Add(new ProcessedImage
        {
            UserProfileId = profile.Id,
            OriginalImageUrl = "o",
            ProcessedImageUrl = "p",
            Style = "s",
            CreatedAt = DateTime.UtcNow,
            ScheduledDeletionDate = DateTime.UtcNow.AddDays(30)
        });
        context.UsageLogs.Add(new UsageLog { UserId = userId, Action = "a", CreditsCost = 1, CreditsRemaining = 4, CreatedAt = DateTime.UtcNow });
        context.Predictions.Add(new Prediction { Id = "pred_1", UserId = userId, Style = "s", CreatedAt = DateTime.UtcNow });
        await context.SaveChangesAsync();

        var user = await context.Users.FirstAsync(u => u.Id == userId);
        var adminUsers = new List<ApplicationUser>
        {
            new() { Id = "admin-a", UserName = "a", Email = "a@test.com" },
            new() { Id = "admin-b", UserName = "b", Email = "b@test.com" }
        };

        var userManager = UserManagerMockFactory.Create();
        userManager.Setup(m => m.FindByIdAsync(userId)).ReturnsAsync(user);
        userManager.Setup(m => m.GetUsersInRoleAsync("Admin")).ReturnsAsync(adminUsers);
        userManager.Setup(m => m.DeleteAsync(user)).ReturnsAsync(IdentityResult.Success);

        var service = CreateService(context, userManager.Object);
        var result = await service.DeleteUserAsync(userId, "admin-a", "cleanup");

        Assert.True(result.Success);
        Assert.Empty(await context.UserProfiles.Where(p => p.UserId == userId).ToListAsync());
        Assert.Empty(await context.ProcessedImages.Where(i => i.UserProfileId == profile.Id).ToListAsync());
    }

    [Fact]
    public async Task DeleteUser_PreventsLastAdminDeletion()
    {
        using var context = CreateContext();
        var user = new ApplicationUser { Id = "admin-last", UserName = "a", Email = "a@test.com" };
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var userManager = UserManagerMockFactory.Create();
        userManager.Setup(m => m.FindByIdAsync("admin-last")).ReturnsAsync(user);
        userManager.Setup(m => m.GetUsersInRoleAsync("Admin")).ReturnsAsync(new List<ApplicationUser> { user });

        var service = CreateService(context, userManager.Object);
        var result = await service.DeleteUserAsync("admin-last", "admin-other", "cleanup");

        Assert.False(result.Success);
        Assert.Contains("last admin", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task DeleteUser_RollsBackOnFailure()
    {
        using var context = CreateContext();
        var userId = await SeedUserWithProfileAsync(context, credits: 5);
        var user = await context.Users.FirstAsync(u => u.Id == userId);

        var userManager = UserManagerMockFactory.Create();
        userManager.Setup(m => m.FindByIdAsync(userId)).ReturnsAsync(user);
        userManager.Setup(m => m.GetUsersInRoleAsync("Admin")).ReturnsAsync(new List<ApplicationUser>
        {
            new() { Id = "admin-a", UserName = "a", Email = "a@test.com" },
            new() { Id = "admin-b", UserName = "b", Email = "b@test.com" }
        });
        userManager.Setup(m => m.DeleteAsync(user)).ReturnsAsync(IdentityResult.Failed(new IdentityError { Description = "boom" }));

        var service = CreateService(context, userManager.Object);
        var result = await service.DeleteUserAsync(userId, "admin-a", "cleanup");

        Assert.False(result.Success);
        Assert.Contains("failed", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(await context.AdminAuditLogs.Where(l => l.Action == "UserDeleted" && l.TargetUserId == userId).ToListAsync());
    }

    [Fact]
    public async Task GetUserDiagnosticsAsync_ReturnsNull_WhenUserDoesNotExist()
    {
        using var context = CreateContext();
        var userManager = UserManagerMockFactory.Create();
        var service = CreateService(context, userManager.Object);

        var result = await service.GetUserDiagnosticsAsync("missing");

        Assert.Null(result);
    }

    [Fact]
    public async Task GetUserDiagnosticsAsync_AggregatesTimelinePurchasesAndImages()
    {
        using var context = CreateContext();
        var userId = await SeedUserWithProfileAsync(context, credits: 0);
        var profile = await context.UserProfiles.FirstAsync(p => p.UserId == userId);
        var adminUser = new ApplicationUser
        {
            Id = "admin-1",
            UserName = "admin",
            Email = "admin@example.com"
        };

        context.Users.Add(adminUser);
        context.ProcessedImages.AddRange(
            new ProcessedImage
            {
                UserProfileId = profile.Id,
                OriginalImageUrl = "https://cdn.example.com/upload.jpg",
                ProcessedImageUrl = "https://cdn.example.com/upload-processed.jpg",
                Style = "source",
                IsOriginalUpload = true,
                CreatedAt = DateTime.UtcNow.AddHours(-5),
                ScheduledDeletionDate = DateTime.UtcNow.AddDays(30)
            },
            new ProcessedImage
            {
                UserProfileId = profile.Id,
                OriginalImageUrl = "https://cdn.example.com/input.jpg",
                ProcessedImageUrl = "https://cdn.example.com/generated.jpg",
                Style = "corporate",
                IsGenerated = true,
                CreatedAt = DateTime.UtcNow.AddHours(-2),
                ScheduledDeletionDate = DateTime.UtcNow.AddDays(30)
            });
        context.UsageLogs.Add(new UsageLog
        {
            UserId = userId,
            Action = "basic_generation",
            Details = "Generated 2 headshots",
            CreditsCost = 20,
            CreditsRemaining = 0,
            CreatedAt = DateTime.UtcNow.AddHours(-1)
        });
        context.ModelCreationRequests.Add(new ModelCreationRequest
        {
            UserId = userId,
            ModelName = "exec-v1",
            Status = ModelCreationStatus.Ready,
            CreatedAt = DateTime.UtcNow.AddHours(-7),
            CompletedAt = DateTime.UtcNow.AddHours(-6)
        });
        context.PendingGenerationRequests.Add(new PendingGenerationRequest
        {
            UserId = userId,
            TrainingRequestId = "train-123",
            Status = PendingGenerationStatus.Succeeded,
            CreatedAt = DateTime.UtcNow.AddHours(-4),
            CompletedAt = DateTime.UtcNow.AddHours(-3)
        });
        context.AdminAuditLogs.Add(new AdminAuditLog
        {
            AdminUserId = "admin-1",
            Action = "CreditsAdded",
            TargetUserId = userId,
            Details = "Courtesy adjustment",
            OldValue = "0",
            NewValue = "10",
            CreatedAt = DateTime.UtcNow.AddHours(-8)
        });
        await context.SaveChangesAsync();

        var purchaseHistory = new List<CreditPurchase>
        {
            new()
            {
                Id = 42,
                UserId = userId,
                PackageId = 3,
                Package = new CreditPackage { Id = 3, Name = "Starter Pack", Credits = 20, Price = 9.99m },
                CreditsAwarded = 20,
                AmountPaid = 9.99m,
                PaymentProvider = "stripe",
                Status = PaymentStatus.Completed,
                PurchaseDate = DateTime.UtcNow.AddHours(-9),
                CompletedAt = DateTime.UtcNow.AddHours(-9)
            }
        };

        var userManager = UserManagerMockFactory.Create();
        userManager.Setup(m => m.GetRolesAsync(It.IsAny<ApplicationUser>())).ReturnsAsync(new List<string> { "User" });

        var creditPackageService = new Mock<ICreditPackageService>();
        creditPackageService.Setup(service => service.GetUserPurchaseHistoryAsync(userId)).ReturnsAsync(purchaseHistory);

        var service = CreateService(context, userManager.Object, creditPackageService.Object);
        var diagnostics = await service.GetUserDiagnosticsAsync(userId);

        Assert.NotNull(diagnostics);
        Assert.Equal(0, diagnostics!.Metrics.CurrentCredits);
        Assert.Equal(20, diagnostics.Metrics.TotalCreditsPurchased);
        Assert.Equal(20, diagnostics.Metrics.TotalCreditsConsumed);
        Assert.True(diagnostics.Metrics.HasUsageHistory);
        Assert.Equal(2, diagnostics.RecentImages.Count);
        Assert.Single(diagnostics.RecentPurchases);
        Assert.Contains(diagnostics.ActivityHistory, entry => entry.EventType == "usage");
        Assert.Contains(diagnostics.ActivityHistory, entry => entry.EventType == "purchase");
        Assert.Contains(diagnostics.ActivityHistory, entry => entry.EventType == "upload");
        Assert.Contains(diagnostics.ActivityHistory, entry => entry.EventType == "generation");
        Assert.Contains(diagnostics.ActivityHistory, entry => entry.EventType == "admin");
    }

    [Fact]
    public async Task GetProductHealthAsync_ReportsPivotMetricsAndUnavailableSignals()
    {
        using var context = CreateContext();
        var userId = await SeedUserWithProfileAsync(context, credits: 0);
        var profile = await context.UserProfiles.FirstAsync(p => p.UserId == userId);
        var now = DateTime.UtcNow;

        var starterCreditPackage = new CreditPackage { Id = 101, Name = "Starter credits", Credits = 3, Price = 9m };
        var proCreditPackage = new CreditPackage { Id = 102, Name = "Pro credits", Credits = 9, Price = 19m };
        var starter = new OutcomePackageDefinition { Code = "starter", Name = "Starter Package", Price = 9m, Currency = "USD", IncludedCandidateCount = 3, InternalCreditPackageId = starterCreditPackage.Id };
        var pro = new OutcomePackageDefinition { Code = "pro", Name = "Pro Package", Price = 19m, Currency = "USD", IncludedCandidateCount = 9, InternalCreditPackageId = proCreditPackage.Id };
        context.CreditPackages.AddRange(starterCreditPackage, proCreditPackage);
        context.OutcomePackageDefinitions.AddRange(starter, pro);
        context.CreditPurchases.Add(new CreditPurchase
        {
            UserId = userId,
            PackageId = starterCreditPackage.Id,
            AmountPaid = starter.Price,
            CreditsAwarded = starterCreditPackage.TotalCredits,
            Status = PaymentStatus.Completed,
            PurchaseDate = now.AddDays(-1),
            CompletedAt = now.AddDays(-1)
        });
        await context.SaveChangesAsync();

        context.ProcessedImages.AddRange(
            new ProcessedImage { UserProfileId = profile.Id, OriginalImageUrl = "upload", ProcessedImageUrl = "upload", Style = "source", IsOriginalUpload = true, CreatedAt = now.AddDays(-1), ScheduledDeletionDate = now.AddDays(29) },
            new ProcessedImage { UserProfileId = profile.Id, OriginalImageUrl = "o", ProcessedImageUrl = "openai", Style = "linkedin", IsGenerated = true, Provider = "openai", ProviderModel = "gpt-image-2", GenerationMode = "instant_headshot", GenerationStatus = "succeeded", RawImageStoragePath = "protected/raw.png", CreatedAt = now.AddDays(-1), ScheduledDeletionDate = now.AddDays(29) },
            new ProcessedImage { UserProfileId = profile.Id, OriginalImageUrl = "o", ProcessedImageUrl = "failed", Style = "linkedin", IsGenerated = true, Provider = "openai", ProviderModel = "gpt-image-2", GenerationMode = "instant_headshot", GenerationStatus = "failed", FailureReason = "ProviderTimeout", CreatedAt = now.AddDays(-1), ScheduledDeletionDate = now.AddDays(29) },
            new ProcessedImage { UserProfileId = profile.Id, OriginalImageUrl = "o", ProcessedImageUrl = "replicate", Style = "executive", IsGenerated = true, Provider = "replicate", ProviderModel = "legacy", GenerationStatus = "succeeded", CreatedAt = now.AddDays(-1), ScheduledDeletionDate = now.AddDays(29) });
        context.UserPackageEntitlements.AddRange(
            new UserPackageEntitlement { UserId = userId, OutcomePackageDefinitionId = starter.Id, Status = PackageEntitlementStatus.Active, RemainingCandidates = 2, RemainingRefinements = 1, CreatedAt = now.AddDays(-1), UpdatedAt = now.AddDays(-1) },
            new UserPackageEntitlement { UserId = userId, OutcomePackageDefinitionId = pro.Id, Status = PackageEntitlementStatus.Consumed, RemainingCandidates = 0, RemainingRefinements = 0, CreatedAt = now.AddDays(-20), UpdatedAt = now.AddDays(-20) });
        context.ModelCreationRequests.Add(new ModelCreationRequest { UserId = userId, ModelName = "advanced", CreatedAt = now.AddDays(-1) });
        await context.SaveChangesAsync();

        var userManager = UserManagerMockFactory.Create();
        var service = CreateService(context, userManager.Object);

        var health = await service.GetProductHealthAsync("7d");

        Assert.Equal(1, health.Funnel.Uploads);
        Assert.Equal(1, health.Funnel.SuccessfulPreviewGenerations);
        Assert.Equal(1, health.Funnel.StarterPurchases);
        Assert.Equal(0, health.Funnel.ProPurchases);
        Assert.False(health.Funnel.PreviewGenerationSuccessRateAvailable);
        Assert.Null(health.Funnel.PreviewGenerationSuccessRate);
        Assert.True(health.Funnel.PreviewToPaidConversionRateAvailable);
        Assert.Equal(1m, health.Funnel.PreviewToPaidConversionRate);
        Assert.False(health.Funnel.ExportDownloadsAvailable);
        Assert.Null(health.Funnel.ExportDownloads);
        Assert.False(health.Funnel.GenerationLatencyAvailable);
        Assert.Equal(1, health.PackageFulfillment.ActiveEntitlements);
        Assert.Equal(0, health.PackageFulfillment.ConsumedEntitlements);
        Assert.Equal(1, health.FailureQueue.FailedGenerations);
        Assert.Equal(1, health.ReplicateRetirement.ReplicateGeneratedImages);
        Assert.Equal(1, health.ReplicateRetirement.AdvancedPhotoshootRequests);
        Assert.False(health.ReplicateRetirement.LatencySignalAvailable);
        Assert.False(health.ReplicateRetirement.QualityComplaintSignalAvailable);
    }

    [Fact]
    public async Task GrantPackageEntitlementAsync_ProvisionsDefinitionAllowancesAndCredits()
    {
        using var context = CreateContext();
        var userId = await SeedUserWithProfileAsync(context, credits: 2);
        var creditPackage = new CreditPackage { Id = 201, Name = "Starter credits", Credits = 3, BonusCredits = 1, Price = 9m };
        var definition = new OutcomePackageDefinition
        {
            Code = "starter_package",
            Name = "Starter Package",
            Price = 9m,
            InternalCreditPackageId = creditPackage.Id,
            IncludedCandidateCount = 3,
            IncludedRefinementCount = 2,
            IncludesPlatformExportKit = true
        };
        context.CreditPackages.Add(creditPackage);
        context.OutcomePackageDefinitions.Add(definition);
        await context.SaveChangesAsync();

        var service = CreateService(context, UserManagerMockFactory.Create().Object);
        var result = await service.GrantPackageEntitlementAsync(
            userId,
            new AdminGrantPackageEntitlementDto { PackageDefinitionId = definition.Id, Reason = "QA setup" },
            "admin-1");

        Assert.True(result.Success);
        Assert.Equal(6, result.CreditBalance);
        var entitlement = await context.UserPackageEntitlements.SingleAsync();
        Assert.Equal(3, entitlement.RemainingCandidates);
        Assert.Equal(2, entitlement.RemainingRefinements);
        Assert.True(entitlement.PlatformExportKitAvailable);
        Assert.Contains(context.AdminAuditLogs, log => log.Action == "PackageEntitlementGranted" && log.TargetUserId == userId);
    }

    [Fact]
    public async Task RevokePackageEntitlementAsync_PreservesCountersAndCredits()
    {
        using var context = CreateContext();
        var userId = await SeedUserWithProfileAsync(context, credits: 7);
        var definition = new OutcomePackageDefinition { Code = "starter_package", Name = "Starter Package", Price = 9m };
        context.OutcomePackageDefinitions.Add(definition);
        await context.SaveChangesAsync();
        var entitlement = new UserPackageEntitlement
        {
            UserId = userId,
            OutcomePackageDefinitionId = definition.Id,
            Status = PackageEntitlementStatus.Active,
            RemainingPackageUses = 1,
            RemainingCandidates = 2,
            RemainingRefinements = 1,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        context.UserPackageEntitlements.Add(entitlement);
        await context.SaveChangesAsync();

        var service = CreateService(context, UserManagerMockFactory.Create().Object);
        var result = await service.RevokePackageEntitlementAsync(userId, entitlement.Id, "Support correction", "admin-1");

        Assert.True(result.Success);
        var saved = await context.UserPackageEntitlements.SingleAsync();
        Assert.Equal(PackageEntitlementStatus.Revoked, saved.Status);
        Assert.Equal(2, saved.RemainingCandidates);
        Assert.Equal(7, (await context.UserProfiles.SingleAsync(p => p.UserId == userId)).Credits);
        Assert.Contains(context.AdminAuditLogs, log => log.Action == "PackageEntitlementRevoked" && log.TargetUserId == userId);
    }

    [Fact]
    public async Task GetUserDiagnosticsAsync_ResolvesRetentionActorLabel()
    {
        using var context = CreateContext();
        var userId = await SeedUserWithProfileAsync(context, credits: 0);
        context.AdminAuditLogs.Add(new AdminAuditLog
        {
            AdminUserId = "system:retention",
            TargetUserId = userId,
            Action = "ImageDeletedByRetention",
            Details = "Retention policy deleted upload image 25",
            OldValue = "/uploads/test-user/image.jpg",
            NewValue = "Deleted",
            CreatedAt = DateTime.UtcNow.AddMinutes(-5)
        });
        await context.SaveChangesAsync();

        var userManager = UserManagerMockFactory.Create();
        userManager.Setup(m => m.GetRolesAsync(It.IsAny<ApplicationUser>())).ReturnsAsync(new List<string> { "User" });

        var service = CreateService(context, userManager.Object);
        var diagnostics = await service.GetUserDiagnosticsAsync(userId);

        Assert.NotNull(diagnostics);
        Assert.Contains(diagnostics!.RecentAdminActions, action => action.AdminEmail == "System (Retention Policy)");
        Assert.Contains(diagnostics.ActivityHistory, entry => entry.Title == "Image Deleted By Retention");
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new ApplicationDbContext(options);
    }

    private static async Task<string> SeedUserWithProfileAsync(ApplicationDbContext context, int credits)
    {
        var userId = Guid.NewGuid().ToString();
        var user = new ApplicationUser
        {
            Id = userId,
            UserName = $"user_{userId}",
            Email = $"{userId}@example.com"
        };

        context.Users.Add(user);
        context.UserProfiles.Add(new UserProfile
        {
            UserId = userId,
            User = user,
            Credits = credits,
            SubscriptionTier = SubscriptionTier.Basic,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            LastCreditReset = DateTime.UtcNow
        });

        await context.SaveChangesAsync();
        return userId;
    }

    private static AdminService CreateService(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        ICreditPackageService? creditPackageService = null)
    {
        if (creditPackageService == null)
        {
            var creditPackageServiceMock = new Mock<ICreditPackageService>();
            creditPackageServiceMock
                .Setup(service => service.GetUserPurchaseHistoryAsync(It.IsAny<string>()))
                .ReturnsAsync(Array.Empty<CreditPurchase>());
            creditPackageService = creditPackageServiceMock.Object;
        }

        var storageMock = new Mock<IStorageService>();
        storageMock
            .Setup(s => s.GetImageUrl(It.IsAny<string>()))
            .Returns((string path) => "https://storage.example.com/" + path);

        return new AdminService(
            context,
            new UserProfileRepository(context),
            creditPackageService,
            userManager,
            storageMock.Object,
            NullLogger<AdminService>.Instance);
    }
}
