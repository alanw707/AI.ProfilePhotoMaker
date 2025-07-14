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
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace AI.ProfilePhotoMaker.API.Tests.Controllers
{
    public class ImageControllerReconciliationTests : IDisposable
    {
        private readonly IFixture _fixture;
        private readonly Mock<IUserProfileRepository> _mockUserProfileRepository;
        private readonly Mock<IWebHostEnvironment> _mockEnvironment;
        private readonly Mock<ILogger<ImageController>> _mockLogger;
        private readonly Mock<IConfiguration> _mockConfiguration;
        private readonly Mock<IUserContextService> _mockUserContextService;
        private readonly Mock<IBasicTierService> _mockBasicTierService;
        private readonly ApplicationDbContext _context;
        private readonly ImageController _controller;
        private readonly string _testContentRoot;
        private readonly string _testUploadsPath;
        private readonly string _testGeneratedPath;

        public ImageControllerReconciliationTests()
        {
            _fixture = new Fixture();
            _mockUserProfileRepository = new Mock<IUserProfileRepository>();
            _mockEnvironment = new Mock<IWebHostEnvironment>();
            _mockLogger = new Mock<ILogger<ImageController>>();
            _mockConfiguration = new Mock<IConfiguration>();
            _mockUserContextService = new Mock<IUserContextService>();
            _mockBasicTierService = new Mock<IBasicTierService>();

            // Create test directories
            _testContentRoot = Path.Combine(Path.GetTempPath(), "ImageControllerTests", Guid.NewGuid().ToString());
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
                _context
            );

            SetupAuthentication();
        }

        private void SetupAuthentication()
        {
            var userId = "test-user-123";
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
        public async Task ReconcileDatabase_DryRun_ReturnsOrphanedRecordsCount()
        {
            // Arrange
            var userId = "test-user-123";
            var userProfile = new UserProfile 
            { 
                Id = 1, 
                UserId = userId, 
                FirstName = "Test",
                LastName = "User"
            };

            // Create orphaned database record (no corresponding file)
            var orphanedImage = new ProcessedImage
            {
                Id = 1,
                UserProfileId = 1,
                OriginalImageUrl = "/uploads/test-user-123/orphaned-file.jpg",
                ProcessedImageUrl = "/uploads/test-user-123/orphaned-file.jpg",
                Style = "Original",
                IsOriginalUpload = true,
                IsGenerated = false,
                CreatedAt = DateTime.UtcNow
            };

            userProfile.ProcessedImages = new List<ProcessedImage> { orphanedImage };
            
            _context.UserProfiles.Add(userProfile);
            _context.ProcessedImages.Add(orphanedImage);
            await _context.SaveChangesAsync();

            // Act
            var result = await _controller.ReconcileDatabase(dryRun: true);

            // Assert
            result.Should().BeOfType<OkObjectResult>();
            var okResult = result as OkObjectResult;
            var response = JsonSerializer.Serialize(okResult?.Value);
            response.Should().Contain("OrphanedRecordsRemoved");
            response.Should().Contain("\"OrphanedRecordsRemoved\":1");
        }

        [Fact]
        public async Task ReconcileDatabase_WithOrphanedRecords_RemovesThemWhenNotDryRun()
        {
            // Arrange
            var userId = "test-user-123";
            var userProfile = new UserProfile 
            { 
                Id = 1, 
                UserId = userId, 
                FirstName = "Test",
                LastName = "User"
            };

            var orphanedImage = new ProcessedImage
            {
                Id = 1,
                UserProfileId = 1,
                OriginalImageUrl = "/uploads/test-user-123/orphaned-file.jpg",
                ProcessedImageUrl = "/uploads/test-user-123/orphaned-file.jpg",
                Style = "Original",
                IsOriginalUpload = true,
                IsGenerated = false,
                CreatedAt = DateTime.UtcNow,
                UserProfile = userProfile
            };

            userProfile.ProcessedImages = new List<ProcessedImage> { orphanedImage };
            
            _context.UserProfiles.Add(userProfile);
            _context.ProcessedImages.Add(orphanedImage);
            await _context.SaveChangesAsync();

            var initialCount = await _context.ProcessedImages.CountAsync();
            initialCount.Should().Be(1);

            // Act
            var result = await _controller.ReconcileDatabase(dryRun: false);

            // Assert
            result.Should().BeOfType<OkObjectResult>();
            
            // Verify record was actually removed from database
            var finalCount = await _context.ProcessedImages.CountAsync();
            finalCount.Should().Be(0);
        }

        [Fact]
        public async Task ReconcileDatabase_WithValidFiles_PreservesMatchingRecords()
        {
            // Arrange
            var userId = "test-user-123";
            var userUploadsDir = Path.Combine(_testUploadsPath, userId);
            Directory.CreateDirectory(userUploadsDir);

            // Create actual file
            var testFileName = "valid-file.jpg";
            var testFilePath = Path.Combine(userUploadsDir, testFileName);
            await File.WriteAllTextAsync(testFilePath, "fake image content");

            var userProfile = new UserProfile 
            { 
                Id = 1, 
                UserId = userId, 
                FirstName = "Test",
                LastName = "User"
            };

            var validImage = new ProcessedImage
            {
                Id = 1,
                UserProfileId = 1,
                OriginalImageUrl = $"/uploads/{userId}/{testFileName}",
                ProcessedImageUrl = $"/uploads/{userId}/{testFileName}",
                Style = "Original",
                IsOriginalUpload = true,
                IsGenerated = false,
                CreatedAt = DateTime.UtcNow,
                UserProfile = userProfile
            };

            userProfile.ProcessedImages = new List<ProcessedImage> { validImage };
            
            _context.UserProfiles.Add(userProfile);
            _context.ProcessedImages.Add(validImage);
            await _context.SaveChangesAsync();

            // Act
            var result = await _controller.ReconcileDatabase(dryRun: false);

            // Assert
            result.Should().BeOfType<OkObjectResult>();
            
            // Verify record was preserved
            var finalCount = await _context.ProcessedImages.CountAsync();
            finalCount.Should().Be(1);
        }

        [Fact]
        public async Task ReconcileDatabase_HandlesEmptyDatabase_Gracefully()
        {
            // Arrange - No users in database

            // Act
            var result = await _controller.ReconcileDatabase(dryRun: true);

            // Assert
            result.Should().BeOfType<OkObjectResult>();
            var okResult = result as OkObjectResult;
            var response = JsonSerializer.Serialize(okResult?.Value);
            response.Should().Contain("\"TotalUsers\":0");
            response.Should().Contain("\"OrphanedRecordsRemoved\":0");
        }

        [Fact]
        public async Task ReconcileDatabase_WithMixedOrphanedAndValid_OnlyRemovesOrphaned()
        {
            // Arrange
            var userId = "test-user-123";
            var userUploadsDir = Path.Combine(_testUploadsPath, userId);
            Directory.CreateDirectory(userUploadsDir);

            // Create one valid file
            var validFileName = "valid-file.jpg";
            var validFilePath = Path.Combine(userUploadsDir, validFileName);
            await File.WriteAllTextAsync(validFilePath, "fake image content");

            var userProfile = new UserProfile 
            { 
                Id = 1, 
                UserId = userId, 
                FirstName = "Test",
                LastName = "User"
            };

            var validImage = new ProcessedImage
            {
                Id = 1,
                UserProfileId = 1,
                OriginalImageUrl = $"/uploads/{userId}/{validFileName}",
                ProcessedImageUrl = $"/uploads/{userId}/{validFileName}",
                Style = "Original",
                IsOriginalUpload = true,
                IsGenerated = false,
                CreatedAt = DateTime.UtcNow,
                UserProfile = userProfile
            };

            var orphanedImage = new ProcessedImage
            {
                Id = 2,
                UserProfileId = 1,
                OriginalImageUrl = $"/uploads/{userId}/orphaned-file.jpg",
                ProcessedImageUrl = $"/uploads/{userId}/orphaned-file.jpg",
                Style = "Original",
                IsOriginalUpload = true,
                IsGenerated = false,
                CreatedAt = DateTime.UtcNow,
                UserProfile = userProfile
            };

            userProfile.ProcessedImages = new List<ProcessedImage> { validImage, orphanedImage };
            
            _context.UserProfiles.Add(userProfile);
            _context.ProcessedImages.AddRange(validImage, orphanedImage);
            await _context.SaveChangesAsync();

            var initialCount = await _context.ProcessedImages.CountAsync();
            initialCount.Should().Be(2);

            // Act
            var result = await _controller.ReconcileDatabase(dryRun: false);

            // Assert
            result.Should().BeOfType<OkObjectResult>();
            
            // Verify only orphaned record was removed
            var finalCount = await _context.ProcessedImages.CountAsync();
            finalCount.Should().Be(1);

            var remainingImage = await _context.ProcessedImages.FirstAsync();
            remainingImage.Id.Should().Be(1); // Valid image should remain
        }

        [Fact]
        public async Task ReconcileDatabase_WithGeneratedImages_HandlesCorrectly()
        {
            // Arrange
            var userId = "test-user-123";
            var userGeneratedDir = Path.Combine(_testGeneratedPath, userId);
            Directory.CreateDirectory(userGeneratedDir);

            // Create actual generated file
            var generatedFileName = "professional_1.png";
            var generatedFilePath = Path.Combine(userGeneratedDir, generatedFileName);
            await File.WriteAllTextAsync(generatedFilePath, "fake generated image content");

            var userProfile = new UserProfile 
            { 
                Id = 1, 
                UserId = userId, 
                FirstName = "Test",
                LastName = "User"
            };

            var validGeneratedImage = new ProcessedImage
            {
                Id = 1,
                UserProfileId = 1,
                OriginalImageUrl = $"/generated/{userId}/{generatedFileName}",
                ProcessedImageUrl = $"/generated/{userId}/{generatedFileName}",
                Style = "Professional",
                IsOriginalUpload = false,
                IsGenerated = true,
                CreatedAt = DateTime.UtcNow,
                UserProfile = userProfile
            };

            var orphanedGeneratedImage = new ProcessedImage
            {
                Id = 2,
                UserProfileId = 1,
                OriginalImageUrl = $"/generated/{userId}/missing_file.png",
                ProcessedImageUrl = $"/generated/{userId}/missing_file.png",
                Style = "Creative",
                IsOriginalUpload = false,
                IsGenerated = true,
                CreatedAt = DateTime.UtcNow,
                UserProfile = userProfile
            };

            userProfile.ProcessedImages = new List<ProcessedImage> { validGeneratedImage, orphanedGeneratedImage };
            
            _context.UserProfiles.Add(userProfile);
            _context.ProcessedImages.AddRange(validGeneratedImage, orphanedGeneratedImage);
            await _context.SaveChangesAsync();

            // Act
            var result = await _controller.ReconcileDatabase(dryRun: false);

            // Assert
            result.Should().BeOfType<OkObjectResult>();
            
            // Verify only orphaned generated image was removed
            var finalCount = await _context.ProcessedImages.CountAsync();
            finalCount.Should().Be(1);

            var remainingImage = await _context.ProcessedImages.FirstAsync();
            remainingImage.Id.Should().Be(1); // Valid generated image should remain
            remainingImage.Style.Should().Be("Professional");
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