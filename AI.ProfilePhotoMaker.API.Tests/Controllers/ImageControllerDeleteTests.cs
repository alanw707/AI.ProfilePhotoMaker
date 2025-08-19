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
using System.IO;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace AI.ProfilePhotoMaker.API.Tests.Controllers
{
    public class ImageControllerDeleteTests : IDisposable
    {
        private readonly IFixture _fixture;
        private readonly Mock<IUserProfileRepository> _mockUserProfileRepository;
        private readonly Mock<IWebHostEnvironment> _mockEnvironment;
        private readonly Mock<ILogger<ImageController>> _mockLogger;
        private readonly Mock<IConfiguration> _mockConfiguration;
        private readonly Mock<IUserContextService> _mockUserContextService;
        private readonly Mock<IBasicTierService> _mockBasicTierService;
        private readonly Mock<IAsyncFileService> _mockAsyncFileService;
        private readonly Mock<IAsyncZipService> _mockAsyncZipService;
        private readonly Mock<IStorageService> _mockStorageService;
        private readonly Mock<StoragePathResolver> _mockPathResolver;
        private readonly ApplicationDbContext _context;
        private readonly ImageController _controller;
        private readonly string _testContentRoot;
        private readonly string _testUploadsPath;
        private readonly string _testGeneratedPath;
        private readonly string _testUserId = "test-user-123";

        public ImageControllerDeleteTests()
        {
            _fixture = new Fixture();
            _mockUserProfileRepository = new Mock<IUserProfileRepository>();
            _mockEnvironment = new Mock<IWebHostEnvironment>();
            _mockLogger = new Mock<ILogger<ImageController>>();
            _mockConfiguration = new Mock<IConfiguration>();
            _mockUserContextService = new Mock<IUserContextService>();
            _mockBasicTierService = new Mock<IBasicTierService>();
            _mockAsyncFileService = new Mock<IAsyncFileService>();
            _mockAsyncZipService = new Mock<IAsyncZipService>();
            _mockStorageService = new Mock<IStorageService>();
            _mockPathResolver = new Mock<StoragePathResolver>();

            // Create test directories
            _testContentRoot = Path.Combine(Path.GetTempPath(), "ImageDeleteTests", Guid.NewGuid().ToString());
            _testUploadsPath = Path.Combine(_testContentRoot, "uploads");
            _testGeneratedPath = Path.Combine(_testContentRoot, "generated");
            Directory.CreateDirectory(_testUploadsPath);
            Directory.CreateDirectory(_testGeneratedPath);

            _mockEnvironment.Setup(e => e.ContentRootPath).Returns(_testContentRoot);

            // Setup in-memory database
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;
            _context = new ApplicationDbContext(options);

            _controller = new ImageController(
                _mockUserProfileRepository.Object,
                _mockEnvironment.Object,
                _mockConfiguration.Object,
                _mockUserContextService.Object,
                _mockBasicTierService.Object,
                _mockLogger.Object,
                _context,
                _mockAsyncFileService.Object,
                _mockAsyncZipService.Object,
                _mockStorageService.Object,
                _mockPathResolver.Object
            );

            SetupAuthentication();
            SetupMockUserProfile();
        }

        private void SetupAuthentication()
        {
            var claimsPrincipal = new ClaimsPrincipal(new ClaimsIdentity(new Claim[]
            {
                new Claim(ClaimTypes.NameIdentifier, _testUserId)
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

        private UserProfile SetupMockUserProfile()
        {
            var userProfile = new UserProfile
            {
                Id = 1,
                UserId = _testUserId,
                FirstName = "Test",
                LastName = "User",
                ProcessedImages = new List<ProcessedImage>()
            };

            _mockUserProfileRepository.Setup(r => r.GetByUserIdAsync(_testUserId))
                .ReturnsAsync(userProfile);

            return userProfile;
        }

        [Fact]
        public async Task DeleteImage_WithValidUploadedImage_DeletesBothFileAndRecord()
        {
            // Arrange
            var userUploadsDir = Path.Combine(_testUploadsPath, _testUserId);
            Directory.CreateDirectory(userUploadsDir);

            var fileName = "test-upload.jpg";
            var filePath = Path.Combine(userUploadsDir, fileName);
            await File.WriteAllTextAsync(filePath, "fake image content");

            var userProfile = SetupMockUserProfile();
            var uploadedImage = new ProcessedImage
            {
                Id = 1,
                UserProfileId = 1,
                OriginalImageUrl = $"/uploads/{_testUserId}/{fileName}",
                ProcessedImageUrl = $"/uploads/{_testUserId}/{fileName}",
                Style = "Original",
                IsOriginalUpload = true,
                IsGenerated = false,
                CreatedAt = DateTime.UtcNow,
                UserProfile = userProfile
            };

            userProfile.ProcessedImages.Add(uploadedImage);
            File.Exists(filePath).Should().BeTrue("File should exist before delete");

            // Act
            var result = await _controller.DeleteImage(1);

            // Assert
            result.Should().BeOfType<OkObjectResult>();
            var okResult = result as OkObjectResult;
            var response = JsonSerializer.Serialize(okResult?.Value);
            response.Should().Contain("\"success\":true");
            response.Should().Contain("\"PhysicalFileDeleted\":true");
            response.Should().Contain("\"DatabaseVerified\":true");

            // Verify file was deleted
            File.Exists(filePath).Should().BeFalse("File should be deleted");

            // Verify image was removed from profile
            userProfile.ProcessedImages.Should().BeEmpty();
        }

        [Fact]
        public async Task DeleteImage_WithValidGeneratedImage_DeletesBothFileAndRecord()
        {
            // Arrange
            var userGeneratedDir = Path.Combine(_testGeneratedPath, _testUserId);
            Directory.CreateDirectory(userGeneratedDir);

            var fileName = "professional_1.png";
            var filePath = Path.Combine(userGeneratedDir, fileName);
            await File.WriteAllTextAsync(filePath, "fake generated image content");

            var userProfile = SetupMockUserProfile();
            var generatedImage = new ProcessedImage
            {
                Id = 1,
                UserProfileId = 1,
                OriginalImageUrl = $"/generated/{_testUserId}/{fileName}",
                ProcessedImageUrl = $"/generated/{_testUserId}/{fileName}",
                Style = "Professional",
                IsOriginalUpload = false,
                IsGenerated = true,
                CreatedAt = DateTime.UtcNow,
                UserProfile = userProfile
            };

            userProfile.ProcessedImages.Add(generatedImage);
            File.Exists(filePath).Should().BeTrue("File should exist before delete");

            // Act
            var result = await _controller.DeleteImage(1);

            // Assert
            result.Should().BeOfType<OkObjectResult>();
            var okResult = result as OkObjectResult;
            var response = JsonSerializer.Serialize(okResult?.Value);
            response.Should().Contain("\"success\":true");
            response.Should().Contain("\"PhysicalFileDeleted\":true");

            // Verify file was deleted
            File.Exists(filePath).Should().BeFalse("File should be deleted");

            // Verify image was removed from profile
            userProfile.ProcessedImages.Should().BeEmpty();
        }

        [Fact]
        public async Task DeleteImage_WithMissingFile_StillRemovesDatabaseRecord()
        {
            // Arrange
            var userProfile = SetupMockUserProfile();
            var uploadedImage = new ProcessedImage
            {
                Id = 1,
                UserProfileId = 1,
                OriginalImageUrl = $"/uploads/{_testUserId}/missing-file.jpg",
                ProcessedImageUrl = $"/uploads/{_testUserId}/missing-file.jpg",
                Style = "Original",
                IsOriginalUpload = true,
                IsGenerated = false,
                CreatedAt = DateTime.UtcNow,
                UserProfile = userProfile
            };

            userProfile.ProcessedImages.Add(uploadedImage);

            // Act
            var result = await _controller.DeleteImage(1);

            // Assert
            result.Should().BeOfType<OkObjectResult>();
            var okResult = result as OkObjectResult;
            var response = JsonSerializer.Serialize(okResult?.Value);
            response.Should().Contain("\"success\":true");
            response.Should().Contain("\"PhysicalFileDeleted\":false"); // File didn't exist to delete

            // Verify image was still removed from profile (database cleanup)
            userProfile.ProcessedImages.Should().BeEmpty();
        }

        [Fact]
        public async Task DeleteImage_WithNonExistentImageId_ReturnsNotFound()
        {
            // Arrange
            var userProfile = SetupMockUserProfile();
            // No images in profile

            // Act
            var result = await _controller.DeleteImage(999);

            // Assert
            result.Should().BeOfType<NotFoundObjectResult>();
            var notFoundResult = result as NotFoundObjectResult;
            var response = JsonSerializer.Serialize(notFoundResult?.Value);
            response.Should().Contain("Image not found");
        }

        [Fact]
        public async Task DeleteImage_WithUnauthorizedUser_ReturnsUnauthorized()
        {
            // Arrange
            var httpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity()) // No claims
            };
            httpContext.Request.Scheme = "https";
            httpContext.Request.Host = new Microsoft.AspNetCore.Http.HostString("localhost:5000");

            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = httpContext
            };

            // Act
            var result = await _controller.DeleteImage(1);

            // Assert
            result.Should().BeOfType<UnauthorizedObjectResult>();
        }

        [Fact]
        public async Task DeleteImage_WithNonExistentProfile_ReturnsNotFound()
        {
            // Arrange
            _mockUserProfileRepository.Setup(r => r.GetByUserIdAsync(_testUserId))
                .ReturnsAsync((UserProfile?)null);

            // Act
            var result = await _controller.DeleteImage(1);

            // Assert
            result.Should().BeOfType<NotFoundObjectResult>();
            var notFoundResult = result as NotFoundObjectResult;
            var response = JsonSerializer.Serialize(notFoundResult?.Value);
            response.Should().Contain("Profile not found");
        }

        [Fact]
        public async Task DeleteImage_VerifiesTransactionIntegrity()
        {
            // Arrange
            var userProfile = SetupMockUserProfile();
            var uploadedImage = new ProcessedImage
            {
                Id = 1,
                UserProfileId = 1,
                OriginalImageUrl = $"/uploads/{_testUserId}/test-file.jpg",
                ProcessedImageUrl = $"/uploads/{_testUserId}/test-file.jpg",
                Style = "Original",
                IsOriginalUpload = true,
                IsGenerated = false,
                CreatedAt = DateTime.UtcNow,
                UserProfile = userProfile
            };

            userProfile.ProcessedImages.Add(uploadedImage);

            // Setup verification to return the profile again for verification step
            _mockUserProfileRepository.SetupSequence(r => r.GetByUserIdAsync(_testUserId))
                .ReturnsAsync(userProfile)  // First call for delete operation
                .ReturnsAsync(userProfile); // Second call for verification

            // Act
            var result = await _controller.DeleteImage(1);

            // Assert
            result.Should().BeOfType<OkObjectResult>();
            var okResult = result as OkObjectResult;
            var response = JsonSerializer.Serialize(okResult?.Value);
            response.Should().Contain("\"DatabaseVerified\":true");

            // Verify that the repository was called once for delete (verification uses direct DB context)
            _mockUserProfileRepository.Verify(r => r.GetByUserIdAsync(_testUserId), Times.Once);
        }

        [Fact]
        public async Task DeleteImage_WithMultipleImages_OnlyDeletesSpecifiedImage()
        {
            // Arrange
            var userUploadsDir = Path.Combine(_testUploadsPath, _testUserId);
            Directory.CreateDirectory(userUploadsDir);

            // Create two files
            var fileName1 = "image1.jpg";
            var fileName2 = "image2.jpg";
            var filePath1 = Path.Combine(userUploadsDir, fileName1);
            var filePath2 = Path.Combine(userUploadsDir, fileName2);
            await File.WriteAllTextAsync(filePath1, "fake image content 1");
            await File.WriteAllTextAsync(filePath2, "fake image content 2");

            var userProfile = SetupMockUserProfile();
            var image1 = new ProcessedImage
            {
                Id = 1,
                UserProfileId = 1,
                OriginalImageUrl = $"/uploads/{_testUserId}/{fileName1}",
                ProcessedImageUrl = $"/uploads/{_testUserId}/{fileName1}",
                Style = "Original",
                IsOriginalUpload = true,
                IsGenerated = false,
                CreatedAt = DateTime.UtcNow,
                UserProfile = userProfile
            };

            var image2 = new ProcessedImage
            {
                Id = 2,
                UserProfileId = 1,
                OriginalImageUrl = $"/uploads/{_testUserId}/{fileName2}",
                ProcessedImageUrl = $"/uploads/{_testUserId}/{fileName2}",
                Style = "Original",
                IsOriginalUpload = true,
                IsGenerated = false,
                CreatedAt = DateTime.UtcNow,
                UserProfile = userProfile
            };

            userProfile.ProcessedImages.AddRange(new[] { image1, image2 });

            // Act - Delete only image1
            var result = await _controller.DeleteImage(1);

            // Assert
            result.Should().BeOfType<OkObjectResult>();

            // Verify only image1 file was deleted
            File.Exists(filePath1).Should().BeFalse("Image1 file should be deleted");
            File.Exists(filePath2).Should().BeTrue("Image2 file should remain");

            // Verify only image1 was removed from profile
            userProfile.ProcessedImages.Should().HaveCount(1);
            userProfile.ProcessedImages.First().Id.Should().Be(2);
        }

        public void Dispose()
        {
            _context?.Dispose();

            // Clean up test directories
            if (Directory.Exists(_testContentRoot))
            {
                Directory.Delete(_testContentRoot, recursive: true);
            }
        }
    }
}