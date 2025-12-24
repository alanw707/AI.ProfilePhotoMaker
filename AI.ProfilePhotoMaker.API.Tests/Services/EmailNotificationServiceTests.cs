using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AI.ProfilePhotoMaker.API.Configuration;
using AI.ProfilePhotoMaker.API.Services.Notifications;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;

namespace AI.ProfilePhotoMaker.API.Tests.Services;

public class EmailNotificationServiceTests
{
    [Fact]
    public async Task SendWelcomeAsync_UsesPostmarkApiWithTextBody()
    {
        var handler = new CapturingHandler();
        var httpClient = new HttpClient(handler);
        var options = Options.Create(new EmailOptions
        {
            Enabled = true,
            UseApi = true,
            PostmarkServerToken = "test-token",
            PostmarkMessageStream = "outbound",
            FromEmail = "no-reply@aiprofilephotomaker.com",
            FromName = "AI Profile Photo Maker",
            FrontendBaseUrl = "https://app.aiprofilephotomaker.com",
            SandboxMode = false
        });
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new[]
            {
                new KeyValuePair<string, string?>("ExternalApiBaseUrl", "https://api.aiprofilephotomaker.com")
            })
            .Build();
        var logger = new LoggerFactory().CreateLogger<EmailNotificationService>();
        var service = new EmailNotificationService(options, logger, httpClient, configuration);

        await service.SendWelcomeAsync("user-1", "test@example.com", "Alan");

        Assert.NotNull(handler.LastRequest);
        Assert.Equal("https://api.postmarkapp.com/email", handler.LastRequest!.RequestUri?.ToString());
        Assert.True(handler.LastRequest.Headers.Contains("X-Postmark-Server-Token"));

        using var document = JsonDocument.Parse(handler.LastBody ?? string.Empty);
        var root = document.RootElement;
        Assert.Equal("AI Profile Photo Maker <no-reply@aiprofilephotomaker.com>", root.GetProperty("From").GetString());
        Assert.Equal("outbound", root.GetProperty("MessageStream").GetString());
        Assert.True(root.TryGetProperty("TextBody", out var textBody));
        Assert.False(string.IsNullOrWhiteSpace(textBody.GetString()));
    }

    [Fact]
    public async Task SendWelcomeAsync_SkipsPostmarkWhenTokenIsPlaceholder()
    {
        var handler = new CapturingHandler();
        var httpClient = new HttpClient(handler);
        var options = Options.Create(new EmailOptions
        {
            Enabled = true,
            UseApi = true,
            PostmarkServerToken = "REPLACE_WITH_POSTMARK_SERVER_TOKEN",
            FromEmail = "no-reply@aiprofilephotomaker.com",
            FromName = "AI Profile Photo Maker",
            FrontendBaseUrl = "https://app.aiprofilephotomaker.com",
            SandboxMode = false
        });
        var configuration = new ConfigurationBuilder().Build();
        var logger = new LoggerFactory().CreateLogger<EmailNotificationService>();
        var service = new EmailNotificationService(options, logger, httpClient, configuration);

        await service.SendWelcomeAsync("user-1", "test@example.com", "Alan");

        Assert.Null(handler.LastRequest);
    }

    [Fact]
    public async Task SendRetentionDeletionWarningAsync_SendsFourteenDayTemplate()
    {
        var handler = new CapturingHandler();
        var httpClient = new HttpClient(handler);
        var options = Options.Create(new EmailOptions
        {
            Enabled = true,
            UseApi = true,
            PostmarkServerToken = "test-token",
            PostmarkMessageStream = "outbound",
            FromEmail = "no-reply@aiprofilephotomaker.com",
            FromName = "AI Profile Photo Maker",
            FrontendBaseUrl = "https://app.aiprofilephotomaker.com",
            SandboxMode = false
        });
        var configuration = new ConfigurationBuilder().Build();
        var logger = new LoggerFactory().CreateLogger<EmailNotificationService>();
        var service = new EmailNotificationService(options, logger, httpClient, configuration);

        var deletionDate = new DateTime(2030, 1, 15, 0, 0, 0, DateTimeKind.Utc);
        await service.SendRetentionDeletionWarningAsync("user-1", "test@example.com", 3, deletionDate, 14);

        using var document = JsonDocument.Parse(handler.LastBody ?? string.Empty);
        var root = document.RootElement;
        Assert.Equal("Reminder: Your photos will be deleted in 14 days", root.GetProperty("Subject").GetString());
        Assert.Equal("retention-deletion-warning-14d", root.GetProperty("Tag").GetString());
        Assert.Contains("January 15, 2030", root.GetProperty("HtmlBody").GetString());
    }

    [Fact]
    public async Task SendRetentionDeletionWarningAsync_SendsSevenDayTemplate()
    {
        var handler = new CapturingHandler();
        var httpClient = new HttpClient(handler);
        var options = Options.Create(new EmailOptions
        {
            Enabled = true,
            UseApi = true,
            PostmarkServerToken = "test-token",
            PostmarkMessageStream = "outbound",
            FromEmail = "no-reply@aiprofilephotomaker.com",
            FromName = "AI Profile Photo Maker",
            FrontendBaseUrl = "https://app.aiprofilephotomaker.com",
            SandboxMode = false
        });
        var configuration = new ConfigurationBuilder().Build();
        var logger = new LoggerFactory().CreateLogger<EmailNotificationService>();
        var service = new EmailNotificationService(options, logger, httpClient, configuration);

        var deletionDate = new DateTime(2030, 2, 1, 0, 0, 0, DateTimeKind.Utc);
        await service.SendRetentionDeletionWarningAsync("user-1", "test@example.com", 1, deletionDate, 7);

        using var document = JsonDocument.Parse(handler.LastBody ?? string.Empty);
        var root = document.RootElement;
        Assert.Equal("Reminder: Your photos will be deleted in 7 days", root.GetProperty("Subject").GetString());
        Assert.Equal("retention-deletion-warning-7d", root.GetProperty("Tag").GetString());
        Assert.Contains("February 1, 2030", root.GetProperty("HtmlBody").GetString());
    }

    private sealed class CapturingHandler : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }
        public string? LastBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            if (request.Content != null)
            {
                LastBody = await request.Content.ReadAsStringAsync(cancellationToken);
            }

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"MessageID\":\"test-message-id\"}")
            };
        }
    }
}
