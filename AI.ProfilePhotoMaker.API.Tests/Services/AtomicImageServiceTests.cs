using Xunit;
using AutoFixture;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using System.Threading.Tasks;
using AI.ProfilePhotoMaker.API.Data;
using AI.ProfilePhotoMaker.API.Models;
using AI.ProfilePhotoMaker.API.Services;
using AI.ProfilePhotoMaker.API.Services.Storage;
using AI.ProfilePhotoMaker.API.Constants;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace AI.ProfilePhotoMaker.API.Tests.Services
{
    /// <summary>
    /// Integration tests for AtomicImageService to verify database-storage consistency
    /// </summary>
    public class AtomicImageServiceTests : IDisposable
    {
        private readonly IFixture _fixture;
        private readonly ApplicationDbContext _context;
        private readonly Mock<IStorageService> _mockStorageService;
        private readonly Mock<ILogger<AtomicImageService>> _mockLogger;
        private readonly Mock<IWebHostEnvironment> _mockEnvironment;
        private readonly Mock<IConfiguration> _mockConfiguration;
        private readonly Mock<ILogger<StoragePathResolver>> _mockPathResolverLogger;
        private readonly StoragePathResolver _pathResolver;
        private readonly AtomicImageService _service;
        private readonly string _testUserId = "test-user-atomic-123";
        private readonly string _testContentRoot;
        private readonly UserProfile _testUserProfile;

        public AtomicImageServiceTests()
        {
            _fixture = new Fixture();
            
            // Setup in-memory database
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;
            _context = new ApplicationDbContext(options);
            
            // Setup test directories
            _testContentRoot = Path.Combine(Path.GetTempPath(), "AtomicImageServiceTests", Guid.NewGuid().ToString());
            Directory.CreateDirectory(_testContentRoot);
            
            // Setup mocks
            _mockStorageService = new Mock<IStorageService>();
            _mockLogger = new Mock<ILogger<AtomicImageService>>();
            _mockEnvironment = new Mock<IWebHostEnvironment>();
            _mockConfiguration = new Mock<IConfiguration>();
            _mockPathResolverLogger = new Mock<ILogger<StoragePathResolver>>();
            
            // Setup mock environment
            _mockEnvironment.Setup(e => e.ContentRootPath).Returns(_testContentRoot);
            _mockEnvironment.Setup(e => e.EnvironmentName).Returns("Development");
            
            // Setup path resolver
            _pathResolver = new StoragePathResolver(
                _mockEnvironment.Object,
                _mockConfiguration.Object,
                _mockPathResolverLogger.Object);
            
            // Create service
            _service = new AtomicImageService(
                _context,
                _mockStorageService.Object,
                _pathResolver,
                _mockLogger.Object
            );
            
            // Setup test user profile
            _testUserProfile = new UserProfile
            {
                Id = 1,
                UserId = _testUserId,
                FirstName = "Test",
                LastName = "User",
                ProcessedImages = new List<ProcessedImage>()
            };
            
            _context.UserProfiles.Add(_testUserProfile);
            _context.SaveChanges();
        }

        #region Atomic Upload Tests

        [Fact]
        public async Task AtomicUploadAsync_WithValidFiles_ShouldCreateBothStorageAndDatabaseRecords()
        {
            // Arrange
            var files = CreateMockFiles(new[] { "test1.jpg", "test2.png" });
            var storagePaths = new List<string>();
            
            _mockStorageService.Setup(s => s.SaveImageToPathAsync(It.IsAny<Stream>(), It.IsAny<string>()))
                .Callback<Stream, string>((stream, path) => storagePaths.Add(path))
                .ReturnsAsync((Stream stream, string path) => path);

            // Act
            var result = await _service.AtomicUploadAsync(
                _testUserId,
                files,
                StorageType.Upload,
                "selfie",
                createDatabaseRecords: true
            );

            // Assert
            result.Success.Should().BeTrue();
            result.ErrorMessage.Should().BeNull();
            result.FilesUploaded.Should().Be(2);
            result.DatabaseRecordsCreated.Should().Be(2);
            result.UploadedImages.Should().HaveCount(2);
            result.StoragePaths.Should().HaveCount(2);

            // Verify database records were created
            var profile = await _context.UserProfiles
                .Include(p => p.ProcessedImages)
                .FirstAsync(p => p.UserId == _testUserId);
            
            profile.ProcessedImages.Should().HaveCount(2);
            profile.ProcessedImages.All(i => i.IsOriginalUpload).Should().BeTrue();
            profile.ProcessedImages.All(i => !i.IsGenerated).Should().BeTrue();
            profile.ProcessedImages.All(i => i.Style == ImageConstants.OriginalStyle).Should().BeTrue();

            // Verify storage calls were made
            _mockStorageService.Verify(s => s.SaveImageToPathAsync(It.IsAny<Stream>(), It.IsAny<string>()), Times.Exactly(2));
        }

        [Fact]
        public async Task AtomicUploadAsync_WithStorageFailure_ShouldRollbackDatabaseAndCleanupFiles()
        {
            // Arrange
            var files = CreateMockFiles(new[] { "test1.jpg", "test2.png" });
            var savedPaths = new List<string>();
            
            // First file succeeds, second file fails
            var firstCall = true;
            _mockStorageService.Setup(s => s.SaveImageToPathAsync(It.IsAny<Stream>(), It.IsAny<string>()))
                .Returns<Stream, string>((stream, path) =>
                {
                    if (firstCall)
                    {
                        firstCall = false;
                        savedPaths.Add(path);
                        return Task.FromResult(path);  // First file succeeds
                    }
                    else
                    {
                        throw new IOException("Storage failure");  // Second file fails
                    }
                });

            // Setup cleanup to track what gets deleted
            var deletedPaths = new List<string>();
            _mockStorageService.Setup(s => s.DeleteImageAsync(It.IsAny<string>()))
                .Callback<string>(path => deletedPaths.Add(path))
                .ReturnsAsync(true);

            // Act
            var result = await _service.AtomicUploadAsync(
                _testUserId,
                files,
                StorageType.Upload,
                "selfie",
                createDatabaseRecords: true
            );

            // Assert
            result.Success.Should().BeFalse();
            result.ErrorMessage.Should().Contain("Storage failure");
            result.FilesUploaded.Should().Be(0);  // Operation rolled back
            result.DatabaseRecordsCreated.Should().Be(0);

            // Verify no database records were created (transaction rolled back)
            var profile = await _context.UserProfiles
                .Include(p => p.ProcessedImages)
                .FirstAsync(p => p.UserId == _testUserId);
            
            profile.ProcessedImages.Should().BeEmpty();

            // Verify cleanup was called for successfully uploaded file
            _mockStorageService.Verify(s => s.DeleteImageAsync(It.IsAny<string>()), Times.Once);
            deletedPaths.Should().HaveCount(1);
        }

        [Fact]
        public async Task AtomicUploadAsync_WithDatabaseFailure_ShouldCleanupStorageFiles()
        {
            // Arrange
            var files = CreateMockFiles(new[] { "test1.jpg" });
            
            _mockStorageService.Setup(s => s.SaveImageToPathAsync(It.IsAny<Stream>(), It.IsAny<string>()))
                .ReturnsAsync((Stream stream, string path) => path);

            // Setup cleanup tracking
            var deletedPaths = new List<string>();
            _mockStorageService.Setup(s => s.DeleteImageAsync(It.IsAny<string>()))
                .Callback<string>(path => deletedPaths.Add(path))
                .ReturnsAsync(true);

            // Cause database failure by disposing the context
            _context.Dispose();

            // Act
            var result = await _service.AtomicUploadAsync(
                _testUserId,
                files,
                StorageType.Upload,
                "selfie",
                createDatabaseRecords: true
            );

            // Assert
            result.Success.Should().BeFalse();
            result.ErrorMessage.Should().NotBeNullOrEmpty();
            result.FilesUploaded.Should().Be(0);  // Operation failed
            result.DatabaseRecordsCreated.Should().Be(0);

            // Verify storage cleanup was called
            _mockStorageService.Verify(s => s.DeleteImageAsync(It.IsAny<string>()), Times.Once);
            deletedPaths.Should().HaveCount(1);
        }

        [Fact]
        public async Task AtomicUploadAsync_WithoutDatabaseRecords_ShouldOnlyUploadToStorage()
        {
            // Arrange
            var files = CreateMockFiles(new[] { "test1.jpg" });
            
            _mockStorageService.Setup(s => s.SaveImageToPathAsync(It.IsAny<Stream>(), It.IsAny<string>()))
                .ReturnsAsync((Stream stream, string path) => path);

            // Act
            var result = await _service.AtomicUploadAsync(
                _testUserId,
                files,
                StorageType.Upload,
                "selfie",
                createDatabaseRecords: false
            );

            // Assert
            result.Success.Should().BeTrue();
            result.FilesUploaded.Should().Be(1);
            result.DatabaseRecordsCreated.Should().Be(0);
            result.UploadedImages.Should().BeEmpty();

            // Verify no database records were created
            var profile = await _context.UserProfiles
                .Include(p => p.ProcessedImages)
                .FirstAsync(p => p.UserId == _testUserId);
            
            profile.ProcessedImages.Should().BeEmpty();

            // Verify storage call was made
            _mockStorageService.Verify(s => s.SaveImageToPathAsync(It.IsAny<Stream>(), It.IsAny<string>()), Times.Once);
        }

        [Fact]
        public async Task AtomicUploadAsync_WithNonExistentUser_ShouldReturnError()
        {
            // Arrange
            var files = CreateMockFiles(new[] { "test1.jpg" });
            var nonExistentUserId = "non-existent-user";

            // Act
            var result = await _service.AtomicUploadAsync(
                nonExistentUserId,
                files,
                StorageType.Upload,
                "selfie",
                createDatabaseRecords: true
            );

            // Assert
            result.Success.Should().BeFalse();
            result.ErrorMessage.Should().Be("User profile not found");
            result.FilesUploaded.Should().Be(0);
            result.DatabaseRecordsCreated.Should().Be(0);

            // Verify no storage calls were made
            _mockStorageService.Verify(s => s.SaveImageToPathAsync(It.IsAny<Stream>(), It.IsAny<string>()), Times.Never);
        }

        #endregion

        #region Atomic Delete Tests

        [Fact]
        public async Task AtomicDeleteAsync_WithValidImage_ShouldRemoveBothDatabaseRecordAndStorageFile()
        {
            // Arrange
            var imageId = await SetupTestImage("test-image.jpg", "/uploads/test-user/test-image.jpg");

            _mockStorageService.Setup(s => s.DeleteImageAsync(It.IsAny<string>()))
                .ReturnsAsync(true);

            // Act
            var result = await _service.AtomicDeleteAsync(_testUserId, imageId);

            // Assert
            result.Success.Should().BeTrue();
            result.ErrorMessage.Should().BeNull();
            result.DatabaseRecordsDeleted.Should().Be(1);
            result.FilesDeleted.Should().Be(1);
            result.DeletedStoragePaths.Should().HaveCount(1);

            // Verify database record was removed
            var profile = await _context.UserProfiles
                .Include(p => p.ProcessedImages)
                .FirstAsync(p => p.UserId == _testUserId);
            
            profile.ProcessedImages.Should().BeEmpty();

            // Verify storage delete was called
            _mockStorageService.Verify(s => s.DeleteImageAsync(It.IsAny<string>()), Times.Once);
        }

        [Fact]
        public async Task AtomicBulkDeleteAsync_WithMultipleImages_ShouldRemoveAllRecordsAndFiles()
        {
            // Arrange
            var imageId1 = await SetupTestImage("image1.jpg", "/uploads/test-user/image1.jpg");
            var imageId2 = await SetupTestImage("image2.jpg", "/uploads/test-user/image2.jpg");
            var imageIds = new List<int> { imageId1, imageId2 };

            _mockStorageService.Setup(s => s.DeleteImageAsync(It.IsAny<string>()))
                .ReturnsAsync(true);

            // Act
            var result = await _service.AtomicBulkDeleteAsync(_testUserId, imageIds);

            // Assert
            result.Success.Should().BeTrue();
            result.ErrorMessage.Should().BeNull();
            result.DatabaseRecordsDeleted.Should().Be(2);
            result.FilesDeleted.Should().Be(2);
            result.DeletedStoragePaths.Should().HaveCount(2);

            // Verify all database records were removed
            var profile = await _context.UserProfiles
                .Include(p => p.ProcessedImages)
                .FirstAsync(p => p.UserId == _testUserId);
            
            profile.ProcessedImages.Should().BeEmpty();

            // Verify storage deletes were called
            _mockStorageService.Verify(s => s.DeleteImageAsync(It.IsAny<string>()), Times.Exactly(2));
        }

        [Fact]
        public async Task AtomicDeleteAsync_WithStorageFailure_ShouldRollbackDatabaseChanges()
        {
            // Arrange
            var imageId = await SetupTestImage("test-image.jpg", "/uploads/test-user/test-image.jpg");

            _mockStorageService.Setup(s => s.DeleteImageAsync(It.IsAny<string>()))
                .ThrowsAsync(new IOException("Storage delete failed"));

            // Act
            var result = await _service.AtomicDeleteAsync(_testUserId, imageId);

            // Assert
            result.Success.Should().BeFalse();
            result.ErrorMessage.Should().Contain("Storage delete failed");
            result.DatabaseRecordsDeleted.Should().Be(0);  // Transaction rolled back
            result.FilesDeleted.Should().Be(0);

            // Verify database record still exists (transaction was rolled back)
            var profile = await _context.UserProfiles
                .Include(p => p.ProcessedImages)
                .FirstAsync(p => p.UserId == _testUserId);
            
            profile.ProcessedImages.Should().HaveCount(1);
            profile.ProcessedImages.First().Id.Should().Be(imageId);
        }

        [Fact]
        public async Task AtomicDeleteAsync_WithMissingStorageFile_ShouldStillRemoveDatabaseRecord()
        {
            // Arrange
            var imageId = await SetupTestImage("missing-file.jpg", "/uploads/test-user/missing-file.jpg");

            // File doesn't exist but delete "succeeds" (returns false but doesn't throw)
            _mockStorageService.Setup(s => s.DeleteImageAsync(It.IsAny<string>()))
                .ReturnsAsync(false);

            // Act
            var result = await _service.AtomicDeleteAsync(_testUserId, imageId);

            // Assert
            result.Success.Should().BeTrue();
            result.ErrorMessage.Should().BeNull();
            result.DatabaseRecordsDeleted.Should().Be(1);
            result.FilesDeleted.Should().Be(0);  // File wasn't actually deleted (didn't exist)

            // Verify database record was removed despite missing file
            var profile = await _context.UserProfiles
                .Include(p => p.ProcessedImages)
                .FirstAsync(p => p.UserId == _testUserId);
            
            profile.ProcessedImages.Should().BeEmpty();
        }

        [Fact]
        public async Task AtomicDeleteAsync_WithNonExistentImage_ShouldReturnError()
        {
            // Arrange
            var nonExistentImageId = 999;

            // Act
            var result = await _service.AtomicDeleteAsync(_testUserId, nonExistentImageId);

            // Assert
            result.Success.Should().BeFalse();
            result.ErrorMessage.Should().Contain("Image 999 not found");
            result.DatabaseRecordsDeleted.Should().Be(0);
            result.FilesDeleted.Should().Be(0);

            // Verify no storage calls were made
            _mockStorageService.Verify(s => s.DeleteImageAsync(It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task AtomicDeleteAsync_WithImageHavingBothOriginalAndProcessedUrls_ShouldDeleteBothFiles()
        {
            // Arrange
            var imageId = await SetupTestImageWithBothUrls(
                "original.jpg", 
                "/uploads/test-user/original.jpg",
                "/processed/test-user/processed.jpg"
            );

            _mockStorageService.Setup(s => s.DeleteImageAsync(It.IsAny<string>()))
                .ReturnsAsync(true);

            // Act
            var result = await _service.AtomicDeleteAsync(_testUserId, imageId);

            // Assert
            result.Success.Should().BeTrue();
            result.FilesDeleted.Should().Be(2);  // Both original and processed files
            result.DeletedStoragePaths.Should().HaveCount(2);

            // Verify both storage deletes were called
            _mockStorageService.Verify(s => s.DeleteImageAsync("/uploads/test-user/original.jpg"), Times.Once);
            _mockStorageService.Verify(s => s.DeleteImageAsync("/processed/test-user/processed.jpg"), Times.Once);
        }

        [Fact]
        public async Task AtomicDeleteAsync_WithImageHavingSameOriginalAndProcessedUrls_ShouldDeleteOnlyOnce()
        {
            // Arrange
            var imageId = await SetupTestImage("same-file.jpg", "/uploads/test-user/same-file.jpg");

            _mockStorageService.Setup(s => s.DeleteImageAsync(It.IsAny<string>()))
                .ReturnsAsync(true);

            // Act
            var result = await _service.AtomicDeleteAsync(_testUserId, imageId);

            // Assert
            result.Success.Should().BeTrue();
            result.FilesDeleted.Should().Be(1);  // Only one unique file
            result.DeletedStoragePaths.Should().HaveCount(1);

            // Verify storage delete was called only once (deduplication)
            _mockStorageService.Verify(s => s.DeleteImageAsync("/uploads/test-user/same-file.jpg"), Times.Once);
        }

        #endregion

        #region Helper Methods

        private List<IFormFile> CreateMockFiles(string[] fileNames)
        {
            var files = new List<IFormFile>();
            
            foreach (var fileName in fileNames)
            {
                var content = "fake image content";
                var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(content));
                var file = new FormFile(stream, 0, stream.Length, "file", fileName)
                {
                    Headers = new HeaderDictionary(),
                    ContentType = fileName.EndsWith(".jpg") ? "image/jpeg" : "image/png"
                };
                files.Add(file);
            }
            
            return files;
        }

        private async Task<int> SetupTestImage(string fileName, string storagePath)
        {
            var image = new ProcessedImage
            {
                UserProfileId = _testUserProfile.Id,
                OriginalImageUrl = storagePath,
                ProcessedImageUrl = storagePath,
                Style = ImageConstants.OriginalStyle,
                IsOriginalUpload = true,
                IsGenerated = false,
                CreatedAt = DateTime.UtcNow
            };

            _testUserProfile.ProcessedImages.Add(image);
            await _context.SaveChangesAsync();
            
            return image.Id;
        }

        private async Task<int> SetupTestImageWithBothUrls(string fileName, string originalUrl, string processedUrl)
        {
            var image = new ProcessedImage
            {
                UserProfileId = _testUserProfile.Id,
                OriginalImageUrl = originalUrl,
                ProcessedImageUrl = processedUrl,
                Style = "Professional",
                IsOriginalUpload = false,
                IsGenerated = true,
                CreatedAt = DateTime.UtcNow
            };

            _testUserProfile.ProcessedImages.Add(image);
            await _context.SaveChangesAsync();
            
            return image.Id;
        }

        #endregion

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
