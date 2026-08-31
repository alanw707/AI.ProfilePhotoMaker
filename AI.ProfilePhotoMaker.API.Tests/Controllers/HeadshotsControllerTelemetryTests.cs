using System.Security.Claims;
using AI.ProfilePhotoMaker.API.Controllers;
using AI.ProfilePhotoMaker.API.Models;
using AI.ProfilePhotoMaker.API.Models.DTOs;
using AI.ProfilePhotoMaker.API.Services;
using AI.ProfilePhotoMaker.API.Services.ImageProcessing;
using AI.ProfilePhotoMaker.API.Services.Security;
using AI.ProfilePhotoMaker.API.Tests.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace AI.ProfilePhotoMaker.API.Tests.Controllers;

public class HeadshotsControllerTelemetryTests
{
    [Fact]
    public async Task Generate_CapturesOneSafeFailureCorrelationWhenTemporaryTelemetryIsEnabled()
    {
        const string userId = "telemetry-user";
        var generator = new Mock<IHeadshotGenerationService>();
        generator.Setup(service => service.GenerateHeadshotAsync(
                It.IsAny<HeadshotGenerationRequestDto>(), userId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HeadshotGenerationException("InsufficientCredits", "missing credits"));
        var users = UserManagerMockFactory.Create();
        users.Setup(manager => manager.FindByIdAsync(userId))
            .ReturnsAsync(new ApplicationUser { Id = userId, EmailConfirmed = true });
        var turnstile = new Mock<ITurnstileVerificationService>();
        turnstile.Setup(service => service.VerifyAsync(It.IsAny<string?>(), It.IsAny<string?>())).ReturnsAsync(true);
        var packages = new Mock<IOutcomePackageService>();
        packages.Setup(service => service.GetActiveEntitlementAsync(userId, "starter_package", It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserPackageEntitlement?)null);
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Features:OpenAIHeadshotMvp"] = "true",
            ["Diagnostics:CaptureHeadshotFailure"] = "true"
        }).Build();
        var environment = new Mock<IWebHostEnvironment>();
        var controller = new HeadshotsController(
            generator.Object,
            users.Object,
            turnstile.Object,
            packages.Object,
            configuration,
            environment.Object,
            NullLogger<HeadshotsController>.Instance)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                        [new Claim(ClaimTypes.NameIdentifier, userId)], "test"))
                }
            }
        };

        var result = await controller.Generate(new HeadshotGenerationRequestDto
        {
            ImageStoragePath = "not-logged",
            PackageCode = "starter_package",
            NumOutputs = 1
        }) as ObjectResult;

        Assert.NotNull(result);
        Assert.Equal(StatusCodes.Status400BadRequest, result!.StatusCode);
        Assert.True(controller.Response.Headers.TryGetValue("X-Headshot-Failure-Correlation", out var correlation));
        Assert.Matches("^[a-f0-9]{32}$", correlation.ToString());
        packages.Verify(service => service.GetActiveEntitlementAsync(userId, "starter_package", It.IsAny<CancellationToken>()), Times.Once);
    }
}
