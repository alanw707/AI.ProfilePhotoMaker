using AI.ProfilePhotoMaker.API.Data;
using AI.ProfilePhotoMaker.API.Models;
using AI.ProfilePhotoMaker.API.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;

namespace AI.ProfilePhotoMaker.API.Tests.Services;

public class UserContextServiceTests
{
    [Fact]
    public async Task GetUserProfileAsync_DoesNotReturnStaleEntityThatCanOverwritePurchasedCredits()
    {
        var databaseName = Guid.NewGuid().ToString();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName)
            .Options;
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var userId = Guid.NewGuid().ToString();

        await using (var seed = new ApplicationDbContext(options))
        {
            var user = new ApplicationUser { Id = userId, UserName = "cache-test", Email = "cache-test@example.invalid" };
            seed.Users.Add(user);
            seed.UserProfiles.Add(new UserProfile { UserId = userId, User = user, Credits = 0 });
            await seed.SaveChangesAsync();
        }

        await using (var firstRequest = new ApplicationDbContext(options))
        {
            var service = CreateService(firstRequest, cache);
            Assert.Equal(0, (await service.GetUserProfileAsync(userId))!.Credits);
        }

        await using (var purchaseRequest = new ApplicationDbContext(options))
        {
            var profile = await purchaseRequest.UserProfiles.SingleAsync(p => p.UserId == userId);
            profile.Credits = 150;
            await purchaseRequest.SaveChangesAsync();
        }

        await using (var uploadRequest = new ApplicationDbContext(options))
        {
            var service = CreateService(uploadRequest, cache);
            var profile = (await service.GetUserProfileAsync(userId))!;
            profile.ProcessedImages.Add(new ProcessedImage
            {
                UserProfileId = profile.Id,
                OriginalImageUrl = "dev/uploads/source.jpg",
                ProcessedImageUrl = "dev/uploads/source.jpg",
                Style = "original",
                IsOriginalUpload = true
            });
            await new UserProfileRepository(uploadRequest).UpdateAsync(profile);
        }

        await using var verification = new ApplicationDbContext(options);
        Assert.Equal(150, (await verification.UserProfiles.SingleAsync(p => p.UserId == userId)).Credits);
    }

    private static UserContextService CreateService(ApplicationDbContext context, IMemoryCache cache) =>
        new(context, cache, new HttpContextAccessor(), NullLogger<UserContextService>.Instance);
}
