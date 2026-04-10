using System.IO;
using System.Text;
using AI.ProfilePhotoMaker.API.Configuration;
using AI.ProfilePhotoMaker.API.Controllers;
using AI.ProfilePhotoMaker.API.Data;
using AI.ProfilePhotoMaker.API.Models;
using AI.ProfilePhotoMaker.API.Services.Marketing;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace AI.ProfilePhotoMaker.API.Tests.Controllers;

public class PostmarkWebhookControllerTests
{
    [Fact]
    public async Task Receive_BounceUpdatesOnlyMatchingLogByPostmarkMessageId()
    {
        await using var db = CreateDbContext();
        var user = new ApplicationUser
        {
            Id = "user-1",
            Email = "user@example.com",
            UserName = "user@example.com"
        };
        var campaign = new MarketingCampaign
        {
            Name = "Campaign",
            Subject = "Subject",
            HtmlBody = "<p>Hello</p>",
            SegmentFilter = SegmentFilters.AllVerified,
            Status = CampaignStatus.Sent
        };

        db.Users.Add(user);
        db.MarketingCampaigns.Add(campaign);
        db.MarketingEmailLogs.AddRange(
            new MarketingEmailLog
            {
                Campaign = campaign,
                UserId = user.Id,
                Email = user.Email!,
                Status = MarketingEmailStatus.Sent,
                PostmarkMessageId = "message-1"
            },
            new MarketingEmailLog
            {
                Campaign = campaign,
                UserId = "user-2",
                Email = user.Email!,
                Status = MarketingEmailStatus.Sent,
                PostmarkMessageId = "message-2"
            });
        await db.SaveChangesAsync();

        var controller = new PostmarkWebhookController(
            db,
            Options.Create(new EmailOptions { PostmarkWebhookSecret = "secret" }),
            NullLogger<PostmarkWebhookController>.Instance);

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = BuildHttpContext(
                """
                {
                  "RecordType": "Bounce",
                  "Email": "user@example.com",
                  "MessageID": "message-1",
                  "Type": "SoftBounce"
                }
                """,
                "secret")
        };

        var result = await controller.Receive(CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>();

        var logs = await db.MarketingEmailLogs
            .OrderBy(log => log.PostmarkMessageId)
            .ToListAsync();

        logs[0].Status.Should().Be(MarketingEmailStatus.Bounced);
        logs[1].Status.Should().Be(MarketingEmailStatus.Sent);
    }

    [Fact]
    public async Task Receive_BounceWithUnknownMessageId_DoesNotFallBackToRecipientEmail()
    {
        await using var db = CreateDbContext();
        var campaign = new MarketingCampaign
        {
            Name = "Campaign",
            Subject = "Subject",
            HtmlBody = "<p>Hello</p>",
            SegmentFilter = SegmentFilters.AllVerified,
            Status = CampaignStatus.Sent
        };

        db.MarketingCampaigns.Add(campaign);
        db.MarketingEmailLogs.Add(new MarketingEmailLog
        {
            Campaign = campaign,
            UserId = "user-1",
            Email = "user@example.com",
            Status = MarketingEmailStatus.Sent,
            PostmarkMessageId = "known-message"
        });
        await db.SaveChangesAsync();

        var controller = new PostmarkWebhookController(
            db,
            Options.Create(new EmailOptions { PostmarkWebhookSecret = "secret" }),
            NullLogger<PostmarkWebhookController>.Instance);

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = BuildHttpContext(
                """
                {
                  "RecordType": "Bounce",
                  "Email": "user@example.com",
                  "MessageID": "unknown-message",
                  "Type": "SoftBounce"
                }
                """,
                "secret")
        };

        await controller.Receive(CancellationToken.None);

        var log = await db.MarketingEmailLogs.SingleAsync();
        log.Status.Should().Be(MarketingEmailStatus.Sent);
    }

    private static DefaultHttpContext BuildHttpContext(string jsonBody, string secret)
    {
        var context = new DefaultHttpContext();
        context.Request.Headers.Authorization = $"Bearer {secret}";
        context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(jsonBody));
        context.Request.ContentType = "application/json";
        return context;
    }

    private static ApplicationDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new ApplicationDbContext(options);
    }
}
