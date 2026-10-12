using System.Net;
using System.Net.Http.Headers;
using AI.ProfilePhotoMaker.API.Services;
using AI.ProfilePhotoMaker.API.Services.Deployment;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

namespace AI.ProfilePhotoMaker.API.Tests.Integration;

/// <summary>
/// The standard test host plus stand-ins for services it does not register, so the guarded
/// controllers can be constructed once a caller is let through.
/// </summary>
public sealed class EndpointSecurityFactory : CustomWebApplicationFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureTestServices(services =>
        {
            services.AddScoped(_ => Mock.Of<IDeploymentValidationService>());
            services.AddScoped(_ => Mock.Of<IDeploymentMonitoringService>());
            services.AddScoped(_ => Mock.Of<IAsyncFileService>());
            services.AddScoped(_ => Mock.Of<IAsyncZipService>());
        });
    }
}

/// <summary>
/// Endpoints that were reachable without signing in (found in the 2026-10 review): deployment
/// validation, the image diagnostic "userId" bypass, and the photo score accepting any image format.
/// </summary>
public class AnonymousEndpointSecurityTests : IClassFixture<EndpointSecurityFactory>
{
    private readonly EndpointSecurityFactory _factory;

    public AnonymousEndpointSecurityTests(EndpointSecurityFactory factory)
    {
        _factory = factory;
    }

    private HttpClient Client(string? userId = null, string? roles = null, bool anonymous = false)
    {
        var client = _factory.CreateAuthenticatedClient();
        if (anonymous)
        {
            client.DefaultRequestHeaders.Add("X-Test-Unauthenticated", "true");
        }
        if (userId != null)
        {
            client.DefaultRequestHeaders.Add("X-Test-UserId", userId);
        }
        if (roles != null)
        {
            client.DefaultRequestHeaders.Add("X-Test-Roles", roles);
        }
        return client;
    }

    public static TheoryData<string, string> DeploymentEndpoints => new()
    {
        { "POST", "/api/deployment/validate/pre-deployment" },
        { "POST", "/api/deployment/validate/post-deployment" },
        { "GET", "/api/deployment/readiness-score" },
        { "GET", "/api/deployment/validate/configuration" },
        { "GET", "/api/deployment/validate/performance" },
        { "GET", "/api/deployment/validate/security" },
        { "GET", "/api/deployment/validate/azure" },
        { "GET", "/api/deployment/validate/database" },
        { "POST", "/api/deployment/validate/regression-tests" },
        { "GET", "/api/deployment/health" },
        { "GET", "/api/deployment/monitor/configuration-drift" },
        { "GET", "/api/deployment/monitor/service-availability" },
        { "GET", "/api/deployment/monitor/performance-regression" },
        { "POST", "/api/deployment/monitor/reset-baseline" },
        { "GET", "/api/deployment/summary" },
    };

    [Theory]
    [MemberData(nameof(DeploymentEndpoints))]
    public async Task DeploymentEndpointsRejectAnonymousCallers(string method, string path)
    {
        var response = await Client(anonymous: true).SendAsync(new HttpRequestMessage(new HttpMethod(method), path));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [MemberData(nameof(DeploymentEndpoints))]
    public async Task DeploymentEndpointsRejectSignedInNonAdmins(string method, string path)
    {
        var response = await Client(userId: "plain-user").SendAsync(new HttpRequestMessage(new HttpMethod(method), path));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task DeploymentEndpointsAdmitAdmins()
    {
        var response = await Client(userId: "admin-user", roles: "Admin").GetAsync("/api/deployment/validate/configuration");

        response.StatusCode.Should().NotBe(HttpStatusCode.Unauthorized).And.NotBe(HttpStatusCode.Forbidden);
    }

    [Theory]
    [InlineData("/api/image/diagnostic")]
    [InlineData("/api/image/diagnostic?userId=someone-else")]
    public async Task ImageDiagnosticRejectsAnonymousCallersEvenWithAUserId(string path)
    {
        var response = await Client(anonymous: true).GetAsync(path);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ImageDiagnosticDoesNotLetAUserReadAnotherUsersRecords()
    {
        var response = await Client(userId: "diag-user-a").GetAsync("/api/image/diagnostic?userId=diag-user-b");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ImageDiagnosticStillAnswersForTheCallersOwnId()
    {
        var response = await Client(userId: "diag-user-a").GetAsync("/api/image/diagnostic?userId=diag-user-a");

        // No profile exists for this test user; the point is that the lookup ran for the caller.
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    public static TheoryData<string, byte[]> UnsupportedImages => new()
    {
        { "photo.tif", new byte[] { 0x49, 0x49, 0x2A, 0x00, 0x08, 0x00, 0x00, 0x00, 0x00, 0x00 } },   // TIFF (little endian)
        { "photo.jpg", new byte[] { 0x49, 0x49, 0x2B, 0x00, 0x08, 0x00, 0x00, 0x00, 0x10, 0x00 } },   // BigTIFF named .jpg
        { "photo.bmp", new byte[] { 0x42, 0x4D, 0x46, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 } },   // BMP
        { "photo.gif", "GIF89a\u0001\u0000\u0001\u0000"u8.ToArray() },
    };

    [Theory]
    [MemberData(nameof(UnsupportedImages))]
    public async Task PhotoScoreRejectsFormatsOtherThanJpegPngWebp(string fileName, byte[] bytes)
    {
        using var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
        form.Add(file, "image", fileName);

        var response = await Client(userId: "score-user").PostAsync("/api/ProfilePhotoWorkflow/score", form);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().Contain("UnsupportedImageFormat");
    }
}
