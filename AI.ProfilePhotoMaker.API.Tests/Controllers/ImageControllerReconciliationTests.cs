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
using AI.ProfilePhotoMaker.API.Models.DTOs;
using AI.ProfilePhotoMaker.API.Services;
using AI.ProfilePhotoMaker.API.Services.Storage;
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
        private readonly Mock<IAsyncFileService> _mockAsyncFileService;
        private readonly Mock<IAsyncZipService> _mockAsyncZipService;
        private readonly Mock<IStorageService> _mockStorageService;
        private readonly StoragePathResolver _pathResolver;
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
            _mockAsyncFileService = new Mock<IAsyncFileService>();
            _mockAsyncZipService = new Mock<IAsyncZipService>();
            _mockStorageService = new Mock<IStorageService>();
            // Use real StoragePathResolver instance
            var mockResolverLogger = new Mock<ILogger<StoragePathResolver>>();
            _mockEnvironment.Setup(e => e.EnvironmentName).Returns("Development");
            _pathResolver = new StoragePathResolver(_mockEnvironment.Object, _mockConfiguration.Object, mockResolverLogger.Object);

            // Create test directories
            _testContentRoot = Path.Combine(Path.GetTempPath(), "ImageControllerTests", Guid.NewGuid().ToString());
            _testUploadsPath = Path.Combine(_testContentRoot, "uploads");
            _testGeneratedPath = Path.Combine(_testContentRoot, "generated");
            Directory.CreateDirectory(_testUploadsPath);
            Directory.CreateDirectory(_testGeneratedPath);

            _mockEnvironment.Setup(e => e.ContentRootPath).Returns(_testContentRoot);
            _mockEnvironment.Setup(e => e.EnvironmentName).Returns("Development");

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
                _pathResolver
            );

            SetupAuthentication();

            // Repository should read from in-memory DbContext for this suite
            _mockUserProfileRepository
                .Setup(r => r.GetByUserIdAsync(It.IsAny<string>()))
                .ReturnsAsync((string uid) => _context.UserProfiles
                    .Include(u => u.ProcessedImages)
                    .FirstOrDefault(u => u.UserId == uid));
            _mockUserProfileRepository
                .Setup(r => r.GetByUserIdLightAsync(It.IsAny<string>()))
                .ReturnsAsync((string uid) => _context.UserProfiles
                    .FirstOrDefault(u => u.UserId == uid));

            // Map storage paths to temp disk for existence checks
            string MapToDisk(string storagePath)
            {
                if (storagePath.StartsWith("/uploads/"))
                {
                    var relative = storagePath.Substring("/uploads/".Length);
                    return Path.Combine(_testUploadsPath, relative);
                }
                if (storagePath.StartsWith("/generated/"))
                {
                    var relative = storagePath.Substring("/generated/".Length);
                    return Path.Combine(_testGeneratedPath, relative);
                }
                return Path.Combine(_testContentRoot, storagePath.TrimStart('/'));
            }

            _mockStorageService
                .Setup(s => s.SaveImageToPathAsync(It.IsAny<Stream>(), It.IsAny<string>()))
                .ReturnsAsync((Stream _, string sp) => sp);
            _mockStorageService
                .Setup(s => s.ExistsAsync(It.IsAny<string>()))
                .ReturnsAsync((string sp) => File.Exists(MapToDisk(sp)));
            _mockStorageService
                .Setup(s => s.GetImageUrl(It.IsAny<string>()))
                .Returns<string>(p => $"https://localhost:5000{p}");
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
            // Dry run reports counts without removal
            response.Should().Contain("\"OrphanedRecords\":1");
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

            // Ensure repository returns the profile and UpdateAsync persists changes
            _mockUserProfileRepository
                .Setup(r => r.GetByUserIdAsync(userId))
                .ReturnsAsync(() => _context.UserProfiles.Include(u => u.ProcessedImages).First(u => u.UserId == userId));
            _mockUserProfileRepository
                .Setup(r => r.UpdateAsync(It.IsAny<UserProfile>()))
                .Callback<UserProfile>(p => { _context.UserProfiles.Update(p); _context.SaveChanges(); })
                .Returns(Task.CompletedTask);

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

            _mockUserProfileRepository
                .Setup(r => r.UpdateAsync(It.IsAny<UserProfile>()))
                .Callback<UserProfile>(p => { _context.UserProfiles.Update(p); _context.SaveChanges(); })
                .Returns(Task.CompletedTask);

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
            // Arrange - Create empty user profile (no images)
            var userId = "test-user-123";
            var userProfile = new UserProfile
            {
                Id = 1,
                UserId = userId,
                FirstName = "Test",
                LastName = "User",
                ProcessedImages = new List<ProcessedImage>()
            };
            _context.UserProfiles.Add(userProfile);
            await _context.SaveChangesAsync();

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

            _mockUserProfileRepository
                .Setup(r => r.UpdateAsync(It.IsAny<UserProfile>()))
                .Callback<UserProfile>(p => { _context.UserProfiles.Update(p); _context.SaveChanges(); })
                .Returns(Task.CompletedTask);

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

        [Fact]
        public async Task SaveEnhancedImage_DoesNotModifyCredits()
        {
            var userId = "test-user-123";
            var profile = new UserProfile
            {
                UserId = userId,
                FirstName = "Test",
                LastName = "User",
                Credits = 3,
                PurchasedCredits = 2
            };

            _context.UserProfiles.Add(profile);
            await _context.SaveChangesAsync();

            var dto = new SaveEnhancedImageDto
            {
                Base64ImageData = "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR4nGNgYAAAAAMAASsJTYQAAAAASUVORK5CYII=",
                EnhancementType = "professional"
            };

            var result = await _controller.SaveEnhancedImage(dto);

            result.Should().BeOfType<OkObjectResult>();
            (await _context.ProcessedImages.CountAsync()).Should().Be(1);

            var refreshed = await _context.UserProfiles.FirstAsync(u => u.UserId == userId);
            refreshed.Credits.Should().Be(3);
            refreshed.PurchasedCredits.Should().Be(2);
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
