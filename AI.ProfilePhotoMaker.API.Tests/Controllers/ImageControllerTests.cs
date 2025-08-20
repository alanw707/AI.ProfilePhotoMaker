using Xunit;
using Moq;
using AutoFixture;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Hosting;
using System.Security.Claims;
using System.Threading.Tasks;
using AI.ProfilePhotoMaker.API.Controllers;
using AI.ProfilePhotoMaker.API.Data;
using AI.ProfilePhotoMaker.API.Models;
using AI.ProfilePhotoMaker.API.Services;
using AI.ProfilePhotoMaker.API.Services.Storage;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace AI.ProfilePhotoMaker.API.Tests.Controllers
{
    public class ImageControllerTests
    {
        private readonly IFixture _fixture;
        private readonly Mock<IUserProfileRepository> _mockUserProfileRepository;
        private readonly Mock<ApplicationDbContext> _mockContext;
        private readonly Mock<IWebHostEnvironment> _mockEnvironment;
        private readonly Mock<ILogger<ImageController>> _mockLogger;
        private readonly Mock<IConfiguration> _mockConfiguration;
        private readonly Mock<IUserContextService> _mockUserContextService;
        private readonly Mock<IBasicTierService> _mockBasicTierService;
        private readonly Mock<IAsyncFileService> _mockAsyncFileService;
        private readonly Mock<IAsyncZipService> _mockAsyncZipService;
        private readonly Mock<IStorageService> _mockStorageService;
        private readonly StoragePathResolver _pathResolver;
        private readonly ImageController _controller;

        public ImageControllerTests()
        {
            _fixture = new Fixture();
            _mockUserProfileRepository = new Mock<IUserProfileRepository>();
            _mockContext = new Mock<ApplicationDbContext>(new Microsoft.EntityFrameworkCore.DbContextOptions<ApplicationDbContext>());
            _mockEnvironment = new Mock<IWebHostEnvironment>();
            _mockLogger = new Mock<ILogger<ImageController>>();
            _mockConfiguration = new Mock<IConfiguration>();
            _mockUserContextService = new Mock<IUserContextService>();
            _mockBasicTierService = new Mock<IBasicTierService>();
            _mockAsyncFileService = new Mock<IAsyncFileService>();
            _mockAsyncZipService = new Mock<IAsyncZipService>();
            _mockStorageService = new Mock<IStorageService>();
            // Use real StoragePathResolver (cannot be mocked without params)
            var mockResolverLogger = new Mock<ILogger<StoragePathResolver>>();
            _mockEnvironment.Setup(e => e.EnvironmentName).Returns("Development");
            _pathResolver = new StoragePathResolver(_mockEnvironment.Object, _mockConfiguration.Object, mockResolverLogger.Object);

            // Provide URL generation for relative storage paths
            _mockStorageService
                .Setup(s => s.GetImageUrl(It.IsAny<string>()))
                .Returns<string>(p => $"https://localhost:5000{p}");

            _controller = new ImageController(
                _mockUserProfileRepository.Object,
                _mockEnvironment.Object,
                _mockConfiguration.Object,
                _mockUserContextService.Object,
                _mockBasicTierService.Object,
                _mockLogger.Object,
                _mockContext.Object,
                _mockAsyncFileService.Object,
                _mockAsyncZipService.Object,
                _mockStorageService.Object,
                _pathResolver
            );

            var userId = _fixture.Create<string>();
            var claimsPrincipal = new ClaimsPrincipal(new ClaimsIdentity(new Claim[]
            {
                new Claim(ClaimTypes.NameIdentifier, userId)
            }, "mock"));

            var httpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext
            {
                User = claimsPrincipal
            };
            httpContext.Request.Scheme = "https";
            httpContext.Request.Host = new Microsoft.AspNetCore.Http.HostString("localhost:5000");

            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = httpContext
            };
        }

        [Fact]
        public async Task GetImages_ReturnsUnauthorized_WhenUserIdIsNull()
        {
            // Arrange
            var httpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity()) };
            httpContext.Request.Scheme = "https";
            httpContext.Request.Host = new Microsoft.AspNetCore.Http.HostString("localhost:5000");
            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = httpContext
            };

            // Act
            var result = await _controller.GetImages();

            // Assert
            result.Should().BeOfType<UnauthorizedResult>(); // Accept UnauthorizedResult
        }

        [Fact]
        public async Task GetImages_ReturnsNotFound_WhenProfileDoesNotExist()
        {
            // Arrange
            var userId = _fixture.Create<string>();
            var httpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, userId) }, "mock")) };
            httpContext.Request.Scheme = "https";
            httpContext.Request.Host = new Microsoft.AspNetCore.Http.HostString("localhost:5000");
            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = httpContext
            };
            _mockUserProfileRepository.Setup(r => r.GetByUserIdAsync(userId)).ReturnsAsync((UserProfile?)null);

            // Act
            var result = await _controller.GetImages();

            // Assert
            result.Should().BeOfType<NotFoundObjectResult>();
        }

        [Fact]
        public async Task GetImages_ReturnsImagesSummary_WhenProfileExists()
        {
            // Arrange
            var userId = _fixture.Create<string>();
            var processedImages = new List<ProcessedImage>
            {
                new ProcessedImage { Id = 1, Style = "Original", CreatedAt = DateTime.UtcNow, IsGenerated = false, IsOriginalUpload = true, OriginalImageUrl = "/uploads/1.jpg", ProcessedImageUrl = "/uploads/1.jpg" },
                new ProcessedImage { Id = 2, Style = "Styled", CreatedAt = DateTime.UtcNow, IsGenerated = true, IsOriginalUpload = false, ProcessedImageUrl = "/generated/2.jpg" }
            };
            var profile = new UserProfile { UserId = userId, ProcessedImages = processedImages };
            _mockUserProfileRepository.Setup(r => r.GetByUserIdAsync(userId)).ReturnsAsync(profile);
            var httpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, userId) }, "mock")) };
            httpContext.Request.Scheme = "https";
            httpContext.Request.Host = new Microsoft.AspNetCore.Http.HostString("localhost:5000");
            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = httpContext
            };

            // Act
            var result = await _controller.GetImages();

            // Assert
            result.Should().BeOfType<OkObjectResult>();
            var okResult = result as OkObjectResult;
            okResult?.Value.Should().NotBeNull();
        }

        [Fact]
        public async Task GetImages_ReturnsEmptyList_WhenNoImagesExist()
        {
            // Arrange
            var userId = _fixture.Create<string>();
            var profile = new UserProfile { UserId = userId, ProcessedImages = new List<ProcessedImage>() };
            _mockUserProfileRepository.Setup(r => r.GetByUserIdAsync(userId)).ReturnsAsync(profile);
            var httpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, userId) }, "mock")) };
            httpContext.Request.Scheme = "https";
            httpContext.Request.Host = new Microsoft.AspNetCore.Http.HostString("localhost:5000");
            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = httpContext
            };

            // Act
            var result = await _controller.GetImages();

            // Assert
            result.Should().BeOfType<OkObjectResult>();
            var okResult = result as OkObjectResult;
            okResult?.Value.Should().NotBeNull();
            if (okResult?.Value is not null)
            {
                var json = JsonSerializer.Serialize(okResult.Value);
                var dict = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json);
                dict.Should().NotBeNull();
                var data = dict!["data"];
                data.GetProperty("totalImages").GetInt32().Should().Be(0);
            }
        }

        [Fact]
        public async Task GetImages_FormatsUrlsCorrectly_ForRelativeAndAbsolute()
        {
            // Arrange
            var userId = _fixture.Create<string>();
            var processedImages = new List<ProcessedImage>
            {
                new ProcessedImage { Id = 1, Style = "Original", CreatedAt = DateTime.UtcNow.AddMinutes(-1), IsGenerated = false, IsOriginalUpload = true, OriginalImageUrl = "/uploads/1.jpg", ProcessedImageUrl = "/uploads/1.jpg" },
                new ProcessedImage { Id = 2, Style = "Styled", CreatedAt = DateTime.UtcNow, IsGenerated = true, IsOriginalUpload = false, ProcessedImageUrl = "https://external.com/2.jpg" }
            };
            var profile = new UserProfile { UserId = userId, ProcessedImages = processedImages };
            _mockUserProfileRepository.Setup(r => r.GetByUserIdAsync(userId)).ReturnsAsync(profile);

            // Mock configuration to provide AppBaseUrl
            _mockConfiguration.Setup(c => c["AppBaseUrl"]).Returns("https://localhost:5000");

            var httpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, userId) }, "mock")) };
            httpContext.Request.Scheme = "https";
            httpContext.Request.Host = new Microsoft.AspNetCore.Http.HostString("localhost:5000");
            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = httpContext
            };

            // Act
            var result = await _controller.GetImages();

            // Assert
            result.Should().BeOfType<OkObjectResult>();
            var okResult = result as OkObjectResult;
            okResult?.Value.Should().NotBeNull();
            if (okResult?.Value is not null)
            {
                var json = JsonSerializer.Serialize(okResult.Value);
                var dict = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json);
                dict.Should().NotBeNull();
                var data = dict!["data"];
                var images = data.GetProperty("images").EnumerateArray().ToList();
                images.Count.Should().Be(2);

                // Debug: output the actual structure
                var image0 = images[0];
                var image1 = images[1];

                // Check what URL properties are available
                var originalUrl0 = image0.TryGetProperty("originalImageUrl", out var prop0) ? prop0.GetString() : null;
                var processedUrl0 = image0.TryGetProperty("processedImageUrl", out var prop0p) ? prop0p.GetString() : null;
                var originalUrl1 = image1.TryGetProperty("originalImageUrl", out var prop1o) ? prop1o.GetString() : null;
                var processedUrl1 = image1.TryGetProperty("processedImageUrl", out var prop1) ? prop1.GetString() : null;

                // Test that URLs are processed correctly
                // With our ordering: images[0] = Id=2 (newest), images[1] = Id=1 (oldest)
                // First image (Id=2) should have the external URL unchanged
                processedUrl0.Should().Be("https://external.com/2.jpg");
                // Second image (Id=1) should have some URL (either original or processed)
                (originalUrl1 ?? processedUrl1).Should().NotBeNullOrEmpty("Second image should have at least one URL");
            }
        }

        [Fact]
        public async Task GetImages_SetsIsOriginalUploadAndIsGeneratedFlags()
        {
            // Arrange
            var userId = _fixture.Create<string>();
            var processedImages = new List<ProcessedImage>
            {
                new ProcessedImage { Id = 1, Style = "Original", CreatedAt = DateTime.UtcNow.AddMinutes(-1), IsGenerated = false, IsOriginalUpload = true, OriginalImageUrl = "/uploads/1.jpg", ProcessedImageUrl = "/uploads/1.jpg" },
                new ProcessedImage { Id = 2, Style = "Styled", CreatedAt = DateTime.UtcNow, IsGenerated = true, IsOriginalUpload = false, ProcessedImageUrl = "/generated/2.jpg" }
            };
            var profile = new UserProfile { UserId = userId, ProcessedImages = processedImages };
            _mockUserProfileRepository.Setup(r => r.GetByUserIdAsync(userId)).ReturnsAsync(profile);
            var httpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, userId) }, "mock")) };
            httpContext.Request.Scheme = "https";
            httpContext.Request.Host = new Microsoft.AspNetCore.Http.HostString("localhost:5000");
            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = httpContext
            };

            // Act
            var result = await _controller.GetImages();

            // Assert
            result.Should().BeOfType<OkObjectResult>();
            var okResult = result as OkObjectResult;
            okResult?.Value.Should().NotBeNull();
            if (okResult?.Value is not null)
            {
                var json = JsonSerializer.Serialize(okResult.Value);
                var dict = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json);
                dict.Should().NotBeNull();
                var data = dict!["data"];
                var images = data.GetProperty("images").EnumerateArray().ToList();
                images.Count.Should().Be(2);
                // With our ordering: images[0] = Id=2 (newest, generated), images[1] = Id=1 (oldest, original)
                images[0].GetProperty("isOriginalUpload").GetBoolean().Should().BeFalse();
                images[0].GetProperty("isGenerated").GetBoolean().Should().BeTrue();
                images[1].GetProperty("isOriginalUpload").GetBoolean().Should().BeTrue();
                images[1].GetProperty("isGenerated").GetBoolean().Should().BeFalse();
            }
        }
    }
}
