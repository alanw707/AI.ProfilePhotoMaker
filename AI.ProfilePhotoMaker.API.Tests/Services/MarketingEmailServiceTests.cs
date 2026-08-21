using AI.ProfilePhotoMaker.API.Configuration;
using AI.ProfilePhotoMaker.API.Data;
using AI.ProfilePhotoMaker.API.Models;
using AI.ProfilePhotoMaker.API.Services.Marketing;
using AI.ProfilePhotoMaker.API.Services.Notifications;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace AI.ProfilePhotoMaker.API.Tests.Services;

public class MarketingEmailServiceTests
{
    [Fact]
    public async Task SendTestAsync_Throws_WhenDeliveryFails()
    {
        await using var db = CreateDbContext();
        var campaign = new MarketingCampaign
        {
            Name = "Campaign",
            Subject = "Subject",
            HtmlBody = "<p>Hello</p>",
            SegmentFilter = SegmentFilters.AllVerified,
            Status = CampaignStatus.Draft,
        };
        db.MarketingCampaigns.Add(campaign);
        await db.SaveChangesAsync();

        var emailService = new Mock<IEmailNotificationService>();
        emailService
            .Setup(x => x.SendMarketingEmailAsync("test", "test@example.com", "[TEST] Subject", "<p>Hello</p>", It.IsAny<string>()))
            .ReturnsAsync(new EmailSendResult(false));

        var segmentService = new Mock<IUserSegmentService>(MockBehavior.Strict);
        var service = CreateService(db, emailService.Object, segmentService.Object);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.SendTestAsync(campaign.Id, "test@example.com"));
    }

    [Fact]
    public async Task ExecuteCampaignAsync_RetriesExistingFailedLog_AndMarksItSent()
    {
        await using var db = CreateDbContext();
        var campaign = new MarketingCampaign
        {
            Name = "Campaign",
            Subject = "Subject",
            HtmlBody = "<p>Hello</p>",
            SegmentFilter = SegmentFilters.AllVerified,
            Status = CampaignStatus.Scheduled,
            ScheduledAt = DateTime.UtcNow.AddMinutes(-1),
        };
        db.MarketingCampaigns.Add(campaign);
        await db.SaveChangesAsync();

        db.MarketingEmailLogs.Add(new MarketingEmailLog
        {
            CampaignId = campaign.Id,
            UserId = "user-1",
            Email = "before@example.com",
            Status = MarketingEmailStatus.Failed,
            ErrorMessage = "Old failure",
        });
        await db.SaveChangesAsync();

        var emailService = new Mock<IEmailNotificationService>();
        emailService
            .Setup(x => x.SendMarketingEmailAsync("user-1", "after@example.com", "Subject", "<p>Hello</p>", It.IsAny<string>()))
            .ReturnsAsync(new EmailSendResult(true, "message-123"));

        var segmentService = new Mock<IUserSegmentService>();
        segmentService
            .Setup(x => x.GetSegmentUsersAsync(SegmentFilters.AllVerified, 1, 50))
            .ReturnsAsync(new[] { ("user-1", "after@example.com") });
        segmentService
            .Setup(x => x.GetSegmentUsersAsync(SegmentFilters.AllVerified, 2, 50))
            .ReturnsAsync(Array.Empty<(string, string)>());

        var service = CreateService(db, emailService.Object, segmentService.Object);

        await service.ExecuteCampaignAsync(campaign.Id);

        var updatedLog = await db.MarketingEmailLogs.SingleAsync();
        updatedLog.Status.Should().Be(MarketingEmailStatus.Sent);
        updatedLog.Email.Should().Be("after@example.com");
        updatedLog.PostmarkMessageId.Should().Be("message-123");
        updatedLog.ErrorMessage.Should().BeNull();

        var updatedCampaign = await db.MarketingCampaigns.SingleAsync();
        updatedCampaign.Status.Should().Be(CampaignStatus.Sent);
        updatedCampaign.SentCount.Should().Be(1);
        updatedCampaign.FailedCount.Should().Be(0);
    }

    [Fact]
    public async Task ExecuteCampaignAsync_ResetsCampaignToScheduled_WhenCancelled()
    {
        await using var db = CreateDbContext();
        var campaign = new MarketingCampaign
        {
            Name = "Campaign",
            Subject = "Subject",
            HtmlBody = "<p>Hello</p>",
            SegmentFilter = SegmentFilters.AllVerified,
            Status = CampaignStatus.Scheduled,
            ScheduledAt = DateTime.UtcNow.AddMinutes(-1),
        };
        db.MarketingCampaigns.Add(campaign);
        await db.SaveChangesAsync();

        var emailService = new Mock<IEmailNotificationService>();
        emailService
            .Setup(x => x.SendMarketingEmailAsync("user-1", "user@example.com", "Subject", "<p>Hello</p>", It.IsAny<string>()))
            .ThrowsAsync(new OperationCanceledException());

        var segmentService = new Mock<IUserSegmentService>();
        segmentService
            .Setup(x => x.GetSegmentUsersAsync(SegmentFilters.AllVerified, 1, 50))
            .ReturnsAsync(new[] { ("user-1", "user@example.com") });

        var service = CreateService(db, emailService.Object, segmentService.Object);

        await Assert.ThrowsAsync<OperationCanceledException>(() => service.ExecuteCampaignAsync(campaign.Id));

        var updatedCampaign = await db.MarketingCampaigns.SingleAsync();
        updatedCampaign.Status.Should().Be(CampaignStatus.Scheduled);
    }

    [Fact]
    public void MarketingUnsubscribeTokenService_RejectsTamperedTokens()
    {
        var token = MarketingUnsubscribeTokenService.CreateToken("user-1", Guid.NewGuid(), "secret-key");
        var separator = token.IndexOf('.');
        var signatureStart = separator + 1;
        var tamperedSignature = $"{(token[signatureStart] == 'A' ? 'B' : 'A')}{token[(signatureStart + 1)..]}";
        var tamperedToken = $"{token[..signatureStart]}{tamperedSignature}";

        var valid = MarketingUnsubscribeTokenService.TryReadUserId(tamperedToken, "secret-key", out _);

        valid.Should().BeFalse();
    }

    private static MarketingEmailService CreateService(
        ApplicationDbContext db,
        IEmailNotificationService emailService,
        IUserSegmentService segmentService)
    {
        return new MarketingEmailService(
            db,
            emailService,
            segmentService,
            Options.Create(new EmailOptions
            {
                FrontendBaseUrl = "https://aiprofilephotomaker.com",
                PostmarkWebhookSecret = "webhook-secret"
            }),
            Options.Create(new MarketingEmailOptions
            {
                BatchSize = 50,
                BatchDelayMs = 0
            }),
            NullLogger<MarketingEmailService>.Instance);
    }

    private static ApplicationDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new ApplicationDbContext(options);
    }
}
