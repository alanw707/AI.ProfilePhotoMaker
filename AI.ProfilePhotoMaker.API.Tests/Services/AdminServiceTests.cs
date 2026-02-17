using AI.ProfilePhotoMaker.API.Data;
using AI.ProfilePhotoMaker.API.Models;
using AI.ProfilePhotoMaker.API.Models.DTOs;
using AI.ProfilePhotoMaker.API.Services;
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
        var service = new AdminService(context, userManager.Object, NullLogger<AdminService>.Instance);

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
        var service = new AdminService(context, userManager.Object, NullLogger<AdminService>.Instance);

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

        var service = new AdminService(context, userManager.Object, NullLogger<AdminService>.Instance);
        var ok = await service.DeactivateUserAsync("u1", "admin-1", "policy");

        Assert.True(ok);
        userManager.Verify(m => m.SetLockoutEndDateAsync(user, DateTimeOffset.MaxValue), Times.Once);
    }

    [Fact]
    public async Task DeactivateUser_RejectsSelfDeactivation()
    {
        using var context = CreateContext();
        var userManager = UserManagerMockFactory.Create();
        var service = new AdminService(context, userManager.Object, NullLogger<AdminService>.Instance);

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

        var service = new AdminService(context, userManager.Object, NullLogger<AdminService>.Instance);
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

        var service = new AdminService(context, userManager.Object, NullLogger<AdminService>.Instance);
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

        var service = new AdminService(context, userManager.Object, NullLogger<AdminService>.Instance);
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

        var service = new AdminService(context, userManager.Object, NullLogger<AdminService>.Instance);
        var result = await service.DeleteUserAsync(userId, "admin-a", "cleanup");

        Assert.False(result.Success);
        Assert.Contains("failed", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(await context.AdminAuditLogs.Where(l => l.Action == "UserDeleted" && l.TargetUserId == userId).ToListAsync());
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
}
