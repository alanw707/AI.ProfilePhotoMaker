using System;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using AI.ProfilePhotoMaker.API.Data;
using AI.ProfilePhotoMaker.API.Models;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AI.ProfilePhotoMaker.API.Tests.Integration;

public class CreditStatusIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private const string UserId = "test-user-1";
    private readonly CustomWebApplicationFactory _factory;

    public CreditStatusIntegrationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task CreditStatus_TopsUp_WhenResetExpired_AndBalanceBelowWeeklyFloor()
    {
        var expiredReset = DateTime.UtcNow.AddDays(-8);
        await SeedUserAsync(credits: 2, lastCreditReset: expiredReset);
        var client = _factory.CreateAuthenticatedClient();

        var response = await client.GetAsync("/api/credit/status");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var root = await ReadJsonAsync(response);
        root.GetProperty("success").GetBoolean().Should().BeTrue();

        var data = root.GetProperty("data");
        data.GetProperty("credits").GetInt32().Should().Be(5);

        var lastCreditReset = data.GetProperty("lastCreditReset").GetDateTime();
        lastCreditReset.Should().BeAfter(expiredReset);

        var nextReset = data.GetProperty("nextResetDate").GetDateTime();
        nextReset.Should().BeCloseTo(lastCreditReset.AddDays(7), TimeSpan.FromMinutes(1));
    }

    private async Task SeedUserAsync(int credits, DateTime lastCreditReset)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == UserId);
        if (user == null)
        {
            user = new ApplicationUser
            {
                Id = UserId,
                UserName = "test-user",
                NormalizedUserName = "TEST-USER",
                Email = "test-user@example.com",
                NormalizedEmail = "TEST-USER@EXAMPLE.COM",
                EmailConfirmed = true
            };
            db.Users.Add(user);
        }
        else
        {
            user.EmailConfirmed = true;
        }

        var profile = await db.UserProfiles.FirstOrDefaultAsync(p => p.UserId == UserId);
        if (profile == null)
        {
            profile = new UserProfile
            {
                UserId = UserId,
                User = user,
                SubscriptionTier = SubscriptionTier.Basic,
                Credits = credits,
                LastCreditReset = lastCreditReset,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            db.UserProfiles.Add(profile);
        }
        else
        {
            profile.User = user;
            profile.SubscriptionTier = SubscriptionTier.Basic;
            profile.Credits = credits;
            profile.LastCreditReset = lastCreditReset;
            profile.UpdatedAt = DateTime.UtcNow;
        }

        await db.SaveChangesAsync();
    }

    private static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response)
    {
        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.Clone();
    }
}
