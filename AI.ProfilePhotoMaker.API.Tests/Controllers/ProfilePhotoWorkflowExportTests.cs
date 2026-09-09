using System.Security.Claims;
using AI.ProfilePhotoMaker.API.Controllers;
using AI.ProfilePhotoMaker.API.Data;
using AI.ProfilePhotoMaker.API.Models;
using AI.ProfilePhotoMaker.API.Models.DTOs;
using AI.ProfilePhotoMaker.API.Services;
using AI.ProfilePhotoMaker.API.Services.ImageProcessing;
using AI.ProfilePhotoMaker.API.Services.Storage;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AI.ProfilePhotoMaker.API.Tests.Controllers;

public class ProfilePhotoWorkflowExportTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Export_DoesNotSpendAllowanceUntilZipIsReady(bool exportFails)
    {
        using var context = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var image = new ProcessedImage
        {
            UserProfile = new UserProfile { UserId = "export-user" },
            ProcessedImageUrl = "generated/test.png"
        };
        context.ProcessedImages.Add(image);
        await context.SaveChangesAsync();
        var packages = new Mock<IOutcomePackageService>();
        var exports = new Mock<IPlatformExportService>();
        var storage = new Mock<IStorageService>();
        storage.Setup(s => s.GetImageAsync(image.ProcessedImageUrl)).ReturnsAsync(new MemoryStream([1]));
        packages.Setup(s => s.ConsumeExportKitAsync("export-user", It.IsAny<CancellationToken>())).ReturnsAsync(true);
        exports.Setup(s => s.CreateExportPackageAsync(It.IsAny<Stream>(), It.IsAny<string>(),
                It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<PlatformExportAdjustmentOptions>(), It.IsAny<CancellationToken>()))
            .Returns(() => exportFails ? Task.FromException<byte[]>(new IOException("Export failed")) : Task.FromResult(new byte[] { 1, 2 }));
        var controller = new ProfilePhotoWorkflowController(packages.Object, Mock.Of<IProfilePhotoScoreService>(),
            exports.Object, storage.Object, context, NullLogger<ProfilePhotoWorkflowController>.Instance)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "export-user")], "test"))
                }
            }
        };
        var request = new CreatePlatformExportPackageRequestDto { ProcessedImageId = image.Id, ExportCodes = ["linkedin"] };
        if (exportFails)
            await Assert.ThrowsAsync<IOException>(() => controller.CreateExportPackage(request, default));
        else
            Assert.IsType<FileContentResult>(await controller.CreateExportPackage(request, default));

        packages.Verify(s => s.ConsumeExportKitAsync("export-user", It.IsAny<CancellationToken>()), exportFails ? Times.Never() : Times.Once());
    }
}
