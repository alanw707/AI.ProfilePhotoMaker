using Xunit;
using Moq;
using AutoFixture;
using FluentAssertions;
using AI.ProfilePhotoMaker.API.Models.DTOs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Hosting;
using System.Security.Claims;
using System.Threading.Tasks;
using AI.ProfilePhotoMaker.API.Controllers;
using AI.ProfilePhotoMaker.API.Data;
using AI.ProfilePhotoMaker.API.Models;
using AI.ProfilePhotoMaker.API.Services.ImageProcessing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;
using System.Linq.Expressions;
using System;
using System.Linq;
using System.Collections.Generic;

namespace AI.ProfilePhotoMaker.API.Tests.Controllers
{
    public class ProfileControllerTests
    {
        private readonly IFixture _fixture;
        private readonly Mock<IUserProfileRepository> _mockUserProfileRepository;
        private readonly Mock<ApplicationDbContext> _mockContext;
        private readonly Mock<IWebHostEnvironment> _mockEnvironment;
        private readonly Mock<ILogger<ProfileController>> _mockLogger;
        private readonly Mock<IConfiguration> _mockConfiguration;
        private readonly Mock<IReplicateApiClient> _mockReplicateApiClient;
        private readonly ProfileController _controller;

        public ProfileControllerTests()
        {
            _fixture = new Fixture();
            _fixture.Behaviors.OfType<ThrowingRecursionBehavior>().ToList()
                .ForEach(b => _fixture.Behaviors.Remove(b));
            _fixture.Behaviors.Add(new OmitOnRecursionBehavior());
            _mockUserProfileRepository = new Mock<IUserProfileRepository>();
            
            // Mock ApplicationDbContext
            // Mock ApplicationDbContext
            // Instead of trying to instantiate with options, we'll mock its DbSet properties.
            // We need to mock the constructor that takes DbContextOptions<ApplicationDbContext>
            // For unit testing, we can pass null or default to the base constructor if it's not used.
            // However, a better approach is to mock the DbSets directly.
            _mockContext = new Mock<ApplicationDbContext>(new DbContextOptions<ApplicationDbContext>());

            _mockEnvironment = new Mock<IWebHostEnvironment>();
            _mockLogger = new Mock<ILogger<ProfileController>>();
            _mockConfiguration = new Mock<IConfiguration>();
            _mockReplicateApiClient = new Mock<IReplicateApiClient>();

            _controller = new ProfileController(
                _mockUserProfileRepository.Object,
                _mockContext.Object,
                _mockEnvironment.Object,
                _mockLogger.Object,
                _mockConfiguration.Object,
                _mockReplicateApiClient.Object
            );

            // Setup a default user for tests that require authentication
            var userId = _fixture.Create<string>();
            var claimsPrincipal = new ClaimsPrincipal(new ClaimsIdentity(new Claim[]
            {
                new Claim(ClaimTypes.NameIdentifier, userId)
            }, "mock"));

            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = claimsPrincipal }
            };

            // Setup mock DbSets for _mockContext
            // Use in-memory lists to back the mocked DbSets
            var modelCreationRequestsData = new List<ModelCreationRequest>();
            _mockContext.Setup(c => c.ModelCreationRequests).Returns(GetMockDbSet(modelCreationRequestsData).Object);

            var userProfilesData = new List<UserProfile>();
            _mockContext.Setup(c => c.UserProfiles).Returns(GetMockDbSet(userProfilesData).Object);

            var stylesData = new List<Style>();
            _mockContext.Setup(c => c.Styles).Returns(GetMockDbSet(stylesData).Object);

            var processedImagesData = new List<ProcessedImage>();
            _mockContext.Setup(c => c.ProcessedImages).Returns(GetMockDbSet(processedImagesData).Object);

            var usageLogsData = new List<UsageLog>();
            _mockContext.Setup(c => c.UsageLogs).Returns(GetMockDbSet(usageLogsData).Object);
        }

        // Example Test: GetProfile returns Unauthorized if no user ID
        [Fact]
        public async Task GetProfile_ReturnsUnauthorized_WhenUserIdIsNull()
        {
            // Arrange
            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity()) } // No user ID claim
            };

            // Act
            var result = await _controller.GetProfile();

            // Assert
            result.Should().BeOfType<UnauthorizedResult>();
        }

        [Fact]
        public async Task GetProfile_ReturnsNotFound_WhenProfileDoesNotExist()
        {
            // Arrange
            var userId = _fixture.Create<string>();
            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(new Claim[] { new Claim(ClaimTypes.NameIdentifier, userId) }, "mock")) }
            };
            _mockUserProfileRepository.Setup(r => r.GetByUserIdAsync(userId)).ReturnsAsync((UserProfile?)null);

            // Act
            var result = await _controller.GetProfile();

            // Assert
            result.Should().BeOfType<NotFoundObjectResult>()
                  .Which.Value.Should().Be("Profile not found");
        }

        [Fact]
        public async Task GetProfile_ReturnsOkWithUserProfileDto_WhenProfileExists()
        {
            // Arrange
            var userId = _fixture.Create<string>();
            var userProfile = _fixture.Build<UserProfile>()
                                      .With(p => p.UserId, userId)
                                      .With(p => p.ProcessedImages, new List<ProcessedImage>()) // Ensure ProcessedImages is not null
                                      .Create();
            var modelCreationRequest = _fixture.Build<ModelCreationRequest>()
                                               .With(m => m.UserId, userId)
                                               .With(m => m.Status, ModelCreationStatus.Ready)
                                               .Create();

            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(new Claim[] { new Claim(ClaimTypes.NameIdentifier, userId) }, "mock")) }
            };
            _mockUserProfileRepository.Setup(r => r.GetByUserIdAsync(userId)).ReturnsAsync(userProfile);

            // Setup _mockContext.ModelCreationRequests to return the mock data
            var modelCreationRequestsData = new List<ModelCreationRequest> { modelCreationRequest };
            _mockContext.Setup(c => c.ModelCreationRequests).Returns(GetMockDbSet(modelCreationRequestsData).Object);


            // Act
            var result = await _controller.GetProfile();

            // Assert
            result.Should().BeOfType<OkObjectResult>();
            var profileDto = result.As<OkObjectResult>().Value.Should().BeOfType<UserProfileDto>().Subject;
            profileDto.Id.Should().Be(userProfile.Id);
            profileDto.FirstName.Should().Be(userProfile.FirstName);
            profileDto.LastName.Should().Be(userProfile.LastName);
            profileDto.TrainedModelId.Should().Be(modelCreationRequest.ReplicateModelId);
            profileDto.TrainedModelVersionId.Should().Be(modelCreationRequest.TrainedModelVersion);
            profileDto.ModelTrainedAt.Should().Be(modelCreationRequest.CompletedAt);
            profileDto.TotalProcessedImages.Should().Be(userProfile.ProcessedImages.Count);
        }

        // Tests for CreateProfile
        [Fact]
        public async Task CreateProfile_ReturnsUnauthorized_WhenUserIdIsNull()
        {
            // Arrange
            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity()) } // No user ID claim
            };
            var dto = _fixture.Create<CreateUserProfileDto>();

            // Act
            var result = await _controller.CreateProfile(dto);

            // Assert
            result.Should().BeOfType<UnauthorizedResult>();
        }

        [Fact]
        public async Task CreateProfile_ReturnsBadRequest_WhenProfileAlreadyExists()
        {
            // Arrange
            var userId = _fixture.Create<string>();
            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(new Claim[] { new Claim(ClaimTypes.NameIdentifier, userId) }, "mock")) }
            };
            var existingProfile = _fixture.Create<UserProfile>();
            _mockUserProfileRepository.Setup(r => r.GetByUserIdAsync(userId)).ReturnsAsync(existingProfile);
            var dto = _fixture.Create<CreateUserProfileDto>();

            // Act
            var result = await _controller.CreateProfile(dto);

            // Assert
            result.Should().BeOfType<BadRequestObjectResult>()
                  .Which.Value.Should().Be("Profile already exists");
        }

        [Fact]
        public async Task CreateProfile_ReturnsCreatedAtAction_WhenProfileIsCreatedSuccessfully()
        {
            // Arrange
            var userId = _fixture.Create<string>();
            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(new Claim[] { new Claim(ClaimTypes.NameIdentifier, userId) }, "mock")) }
            };
            _mockUserProfileRepository.Setup(r => r.GetByUserIdAsync(userId)).ReturnsAsync((UserProfile)null);
            _mockUserProfileRepository.Setup(r => r.AddAsync(It.IsAny<UserProfile>()))
                                      .Callback<UserProfile>(profile =>
                                      {
                                          // Simulate EF Core setting ID after AddAsync
                                          profile.Id = _fixture.Create<int>();
                                          profile.UserId = userId;
                                      })
                                      .Returns(Task.CompletedTask);
            var dto = _fixture.Create<CreateUserProfileDto>();

            // Act
            var result = await _controller.CreateProfile(dto);

            // Assert
            result.Should().BeOfType<CreatedAtActionResult>();
            var createdResult = result.As<CreatedAtActionResult>();
            createdResult.ActionName.Should().Be(nameof(ProfileController.GetProfile));
            var profileDto = createdResult.Value.Should().BeOfType<UserProfileDto>().Subject;
            profileDto.FirstName.Should().Be(dto.FirstName);
            profileDto.LastName.Should().Be(dto.LastName);
            profileDto.Gender.Should().Be(dto.Gender);
            profileDto.Ethnicity.Should().Be(dto.Ethnicity);
            profileDto.TrainedModelId.Should().BeNull();
            profileDto.TotalProcessedImages.Should().Be(0);

            _mockUserProfileRepository.Verify(r => r.AddAsync(It.IsAny<UserProfile>()), Times.Once);
        }

        // Tests for UpdateProfile
        [Fact]
        public async Task UpdateProfile_ReturnsUnauthorized_WhenUserIdIsNull()
        {
            // Arrange
            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity()) } // No user ID claim
            };
            var dto = _fixture.Create<UpdateUserProfileDto>();

            // Act
            var result = await _controller.UpdateProfile(dto);

            // Assert
            result.Should().BeOfType<UnauthorizedResult>();
        }

        [Fact]
        public async Task UpdateProfile_ReturnsNotFound_WhenProfileDoesNotExist()
        {
            // Arrange
            var userId = _fixture.Create<string>();
            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(new Claim[] { new Claim(ClaimTypes.NameIdentifier, userId) }, "mock")) }
            };
            // Setup _mockContext.UserProfiles to return null for FirstOrDefaultAsync
            var userProfilesData = new List<UserProfile>();
            _mockContext.Setup(c => c.UserProfiles).Returns(GetMockDbSet(userProfilesData).Object);

            var dto = _fixture.Create<UpdateUserProfileDto>();

            // Act
            var result = await _controller.UpdateProfile(dto);

            // Assert
            result.Should().BeOfType<NotFoundObjectResult>()
                  .Which.Value.Should().Be("Profile not found");
        }

        [Fact]
        public async Task UpdateProfile_ReturnsOkWithUpdatedProfileDto_WhenProfileExists()
        {
            // Arrange
            var userId = _fixture.Create<string>();
            var existingProfile = _fixture.Build<UserProfile>()
                                         .With(p => p.UserId, userId)
                                         .With(p => p.ProcessedImages, new List<ProcessedImage>()) // Ensure ProcessedImages is not null
                                         .Create();
            var modelCreationRequest = _fixture.Build<ModelCreationRequest>()
                                               .With(m => m.UserId, userId)
                                               .With(m => m.Status, ModelCreationStatus.Ready)
                                               .Create();

            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(new Claim[] { new Claim(ClaimTypes.NameIdentifier, userId) }, "mock")) }
            };

            // Setup _mockContext.UserProfiles to return the existing profile
            var userProfilesData = new List<UserProfile> { existingProfile };
            _mockContext.Setup(c => c.UserProfiles).Returns(GetMockDbSet(userProfilesData).Object);

            // Setup _mockContext.ModelCreationRequests to return the mock data
            var modelCreationRequestsData = new List<ModelCreationRequest> { modelCreationRequest };
            _mockContext.Setup(c => c.ModelCreationRequests).Returns(GetMockDbSet(modelCreationRequestsData).Object);

            _mockContext.Setup(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()))
                        .ReturnsAsync(1); // Simulate 1 change saved

            var dto = _fixture.Create<UpdateUserProfileDto>();

            // Act
            var result = await _controller.UpdateProfile(dto);

            // Assert
            result.Should().BeOfType<OkObjectResult>();
            var profileDto = result.As<OkObjectResult>().Value.Should().BeOfType<UserProfileDto>().Subject;
            profileDto.FirstName.Should().Be(dto.FirstName);
            profileDto.LastName.Should().Be(dto.LastName);
            profileDto.Gender.Should().Be(dto.Gender);
            profileDto.Ethnicity.Should().Be(dto.Ethnicity);
            profileDto.TrainedModelId.Should().Be(modelCreationRequest.ReplicateModelId);
            profileDto.TrainedModelVersionId.Should().Be(modelCreationRequest.TrainedModelVersion);
            profileDto.ModelTrainedAt.Should().Be(modelCreationRequest.CompletedAt);
            profileDto.TotalProcessedImages.Should().Be(existingProfile.ProcessedImages.Count);

            _mockContext.Verify(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        // Tests for DeleteProfile
        [Fact]
        public async Task DeleteProfile_ReturnsUnauthorized_WhenUserIdIsNull()
        {
            // Arrange
            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity()) } // No user ID claim
            };

            // Act
            var result = await _controller.DeleteProfile();

            // Assert
            result.Should().BeOfType<UnauthorizedResult>();
        }

        [Fact]
        public async Task DeleteProfile_ReturnsNotFound_WhenProfileDoesNotExist()
        {
            // Arrange
            var userId = _fixture.Create<string>();
            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(new Claim[] { new Claim(ClaimTypes.NameIdentifier, userId) }, "mock")) }
            };
            _mockUserProfileRepository.Setup(r => r.GetByUserIdAsync(userId)).ReturnsAsync((UserProfile?)null);

            // Act
            var result = await _controller.DeleteProfile();

            // Assert
            result.Should().BeOfType<NotFoundObjectResult>()
                  .Which.Value.Should().Be("Profile not found");
        }

        [Fact]
        public async Task DeleteProfile_ReturnsOk_WhenProfileIsDeletedSuccessfully()
        {
            // Arrange
            var userId = _fixture.Create<string>();
            var userProfile = _fixture.Build<UserProfile>()
                                      .With(p => p.UserId, userId)
                                      .Create();

            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(new Claim[] { new Claim(ClaimTypes.NameIdentifier, userId) }, "mock")) }
            };
            _mockUserProfileRepository.Setup(r => r.GetByUserIdAsync(userId)).ReturnsAsync(userProfile);
            _mockUserProfileRepository.Setup(r => r.DeleteAsync(userProfile)).Returns(Task.CompletedTask);

            // Act
            var result = await _controller.DeleteProfile();

            // Assert
            result.Should().BeOfType<OkObjectResult>();
            var okResult = result.As<OkObjectResult>();
            okResult.Value.Should().BeEquivalentTo(new { success = true, message = "Profile deleted" });
        }

        // Tests for UploadImages
        [Fact]
        public async Task UploadImages_ReturnsUnauthorized_WhenUserIdIsNull()
        {
            // Arrange
            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity()) } // No user ID claim
            };
            // Use a mock for UploadImagesDto to avoid AutoFixture IFormFile error
            var dto = new UploadImagesDto
            {
                Images = new List<IFormFile> { new Mock<IFormFile>().Object }, // Ensure at least one file so userId check is hit
                ForTraining = false
            };

            // Act
            var result = await _controller.UploadImages(dto);

            // Assert
            result.Should().BeOfType<UnauthorizedResult>();
        }

        [Fact]
        public async Task UploadImages_ReturnsBadRequest_WhenNoImagesProvided()
        {
            // Arrange
            var userId = _fixture.Create<string>();
            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(new Claim[] { new Claim(ClaimTypes.NameIdentifier, userId) }, "mock")) }
            };
            var dto = _fixture.Build<UploadImagesDto>()
                             .With(d => d.Images, (List<IFormFile>?)null) // No images, allow null
                             .Create();

            // Act
            var result = await _controller.UploadImages(dto);

            // Assert
            result.Should().BeAssignableTo<BadRequestObjectResult>()
                  .Which.Value.Should().Be("No images provided");
        }

        [Fact]
        public async Task UploadImages_ReturnsBadRequest_WhenTooManyImagesProvided()
        {
            // Arrange
            var userId = _fixture.Create<string>();
            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(new Claim[] { new Claim(ClaimTypes.NameIdentifier, userId) }, "mock")) }
            };
            // Use mocks instead of AutoFixture for IFormFile
            var images = Enumerable.Range(0, 21).Select(_ => new Mock<IFormFile>().Object).ToList(); // 21 images
            var dto = _fixture.Build<UploadImagesDto>()
                             .With(d => d.Images, images)
                             .Create();

            // Act
            var result = await _controller.UploadImages(dto);

            // Assert
            result.Should().BeAssignableTo<BadRequestObjectResult>()
                  .Which.Value.Should().Be("Maximum 20 images allowed");
        }

        [Fact]
        public async Task UploadImages_ReturnsBadRequest_WhenInvalidImageFileProvided()
        {
            // Arrange
            var userId = _fixture.Create<string>();
            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(new Claim[] { new Claim(ClaimTypes.NameIdentifier, userId) }, "mock")) }
            };

            var mockFile = new Mock<IFormFile>();
            mockFile.Setup(f => f.FileName).Returns("test.txt"); // Invalid extension
            mockFile.Setup(f => f.Length).Returns(100);
            mockFile.Setup(f => f.OpenReadStream()).Returns(new MemoryStream(new byte[] { 0x00, 0x00, 0x00, 0x00 })); // Invalid signature

            var dto = _fixture.Build<UploadImagesDto>()
                             .With(d => d.Images, new List<IFormFile> { mockFile.Object })
                             .Create();

            // Act
            var result = await _controller.UploadImages(dto);

            // Assert
            result.Should().BeAssignableTo<ObjectResult>()
                  .Which.StatusCode.Should().Be(500);
        }

        [Fact]
        public async Task UploadImages_CreatesNewProfileAndUploadsImages_WhenNoExistingProfile()
        {
            // Arrange
            var userId = _fixture.Create<string>();
            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(new Claim[] { new Claim(ClaimTypes.NameIdentifier, userId) }, "mock")) }
            };
            _mockUserProfileRepository.Setup(r => r.GetByUserIdAsync(userId)).ReturnsAsync((UserProfile?)null);
            _mockUserProfileRepository.Setup(r => r.AddAsync(It.IsAny<UserProfile>()))
                                      .Callback<UserProfile>(profile => profile.Id = _fixture.Create<int>());
            _mockUserProfileRepository.Setup(r => r.UpdateAsync(It.IsAny<UserProfile>())).Returns(Task.CompletedTask);

            var tempRoot = Path.Combine(Path.GetTempPath(), "aiprofilemaker-test", Guid.NewGuid().ToString());
            _mockEnvironment.Setup(e => e.ContentRootPath).Returns(tempRoot);
            _mockConfiguration.Setup(c => c["AppBaseUrl"]).Returns("http://localhost");
            Directory.CreateDirectory(Path.Combine(tempRoot, "uploads", userId)); // Ensure upload dir exists for test

            var mockFile = new Mock<IFormFile>();
            mockFile.Setup(f => f.FileName).Returns("test.jpg");
            mockFile.Setup(f => f.Length).Returns(100);
            mockFile.Setup(f => f.OpenReadStream()).Returns(new MemoryStream(new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 })); // Valid JPG signature
            mockFile.Setup(f => f.CopyToAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
                    .Returns(Task.CompletedTask);

            var dto = _fixture.Build<UploadImagesDto>()
                             .With(d => d.Images, new List<IFormFile> { mockFile.Object })
                             .With(d => d.ForTraining, false)
                             .Create();

            // Act
            var result = await _controller.UploadImages(dto);

            // Assert
            result.Should().BeAssignableTo<ObjectResult>()
                  .Which.StatusCode.Should().Be(200);
            var okResult = result.As<ObjectResult>();
            okResult.Value.Should().NotBeNull();
            var dict = okResult.Value.GetType().GetProperties().ToDictionary(p => p.Name, p => p.GetValue(okResult.Value));
            dict["ZipCreated"].Should().NotBeNull();
            ((bool?)dict["ZipCreated"]).Should().Be(false);
            dict["Message"].Should().NotBeNull();
            (dict["Message"] as string).Should().Be("Images uploaded successfully.");

            _mockUserProfileRepository.Verify(r => r.AddAsync(It.IsAny<UserProfile>()), Times.Once);
            _mockUserProfileRepository.Verify(r => r.UpdateAsync(It.IsAny<UserProfile>()), Times.Once);
            mockFile.Verify(f => f.CopyToAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task UploadImages_UploadsImagesToExistingProfile()
        {
            // Arrange
            var userId = "test-user-id";
            var existingProfile = _fixture.Build<UserProfile>()
                                         .With(p => p.UserId, userId)
                                         .With(p => p.ProcessedImages, new List<ProcessedImage>()) // Ensure collection is not null
                                         .Create();

            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(new Claim[] { new Claim(ClaimTypes.NameIdentifier, userId) }, "mock")) }
            };
            _mockUserProfileRepository.Setup(r => r.GetByUserIdAsync(userId)).ReturnsAsync(existingProfile);
            // Setup _mockContext.UserProfiles to return the existing profile
            var userProfilesData = new List<UserProfile> { existingProfile };
            _mockContext.Setup(c => c.UserProfiles).Returns(GetMockDbSet(userProfilesData).Object);

            var tempRoot = Path.Combine(Path.GetTempPath(), "aiprofilemaker-test", Guid.NewGuid().ToString());
            _mockEnvironment.Setup(e => e.ContentRootPath).Returns(tempRoot);
            _mockConfiguration.Setup(c => c["AppBaseUrl"]).Returns("http://localhost");

            var mockFile = new Mock<IFormFile>();
            mockFile.Setup(f => f.FileName).Returns("test2.png");
            mockFile.Setup(f => f.Length).Returns(200);
            mockFile.Setup(f => f.OpenReadStream()).Returns(new MemoryStream(new byte[] { 0x89, 0x50, 0x4E, 0x47 })); // Valid PNG signature
            mockFile.Setup(f => f.CopyToAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
                    .Returns(Task.CompletedTask);

            var dto = _fixture.Build<UploadImagesDto>()
                             .With(d => d.Images, new List<IFormFile> { mockFile.Object })
                             .With(d => d.ForTraining, false)
                             .Create();

            // Act
            var result = await _controller.UploadImages(dto);

            // Assert
            result.Should().BeAssignableTo<ObjectResult>()
                  .Which.StatusCode.Should().Be(200);
            var okResult = result.As<ObjectResult>();
            okResult.Value.Should().NotBeNull();
            var dict = okResult.Value.GetType().GetProperties().ToDictionary(p => p.Name, p => p.GetValue(okResult.Value));
            dict["ZipCreated"].Should().NotBeNull();
            ((bool?)dict["ZipCreated"]).Should().Be(false);
            dict["Message"].Should().NotBeNull();
            (dict["Message"] as string).Should().Be("Images uploaded successfully.");

            _mockUserProfileRepository.Verify(r => r.AddAsync(It.IsAny<UserProfile>()), Times.Never); // Should not add new profile
            _mockUserProfileRepository.Verify(r => r.UpdateAsync(It.IsAny<UserProfile>()), Times.Once);
            mockFile.Verify(f => f.CopyToAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()), Times.Once);
            existingProfile.ProcessedImages.Should().HaveCount(1); // Verify image added to profile
        }

        [Fact]
        public async Task UploadImages_CreatesTrainingZip_WhenForTrainingIsTrue()
        {
            // Arrange
            var userId = _fixture.Create<string>();
            var existingProfile = _fixture.Build<UserProfile>()
                                         .With(p => p.UserId, userId)
                                         .With(p => p.ProcessedImages, new List<ProcessedImage>()) // Ensure collection is not null
                                         .Create();

            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(new Claim[] { new Claim(ClaimTypes.NameIdentifier, userId) }, "mock")) }
            };
            _mockUserProfileRepository.Setup(r => r.GetByUserIdAsync(userId)).ReturnsAsync(existingProfile);
            _mockUserProfileRepository.Setup(r => r.UpdateAsync(It.IsAny<UserProfile>())).Returns(Task.CompletedTask);

            var tempRoot = Path.Combine(Path.GetTempPath(), "aiprofilemaker-test", Guid.NewGuid().ToString());
            _mockEnvironment.Setup(e => e.ContentRootPath).Returns(tempRoot);
            _mockConfiguration.Setup(c => c["AppBaseUrl"]).Returns("http://localhost");

            // Use mocks for IFormFile
            var mockFileMocks = new List<Mock<IFormFile>>();
            var mockFiles = new List<IFormFile>();
            for (int i = 0; i < 10; i++)
            {
                var mf = new Mock<IFormFile>();
                mf.Setup(f => f.FileName).Returns($"train_{i}.jpg");
                mf.Setup(f => f.Length).Returns(300);
                mf.Setup(f => f.OpenReadStream()).Returns(new MemoryStream(new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 }));
                mf.Setup(f => f.CopyToAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
                  .Returns(Task.CompletedTask);
                mockFileMocks.Add(mf);
                mockFiles.Add(mf.Object);
            }
            var dto = _fixture.Build<UploadImagesDto>()
                             .With(d => d.Images, mockFiles)
                             .With(d => d.ForTraining, true)
                             .Create();

            // Act
            var result = await _controller.UploadImages(dto);

            // Assert
            result.Should().BeAssignableTo<ObjectResult>()
                  .Which.StatusCode.Should().Be(200);
            var okResult = result.As<ObjectResult>();
            okResult.Value.Should().NotBeNull();
            var dict = okResult.Value.GetType().GetProperties().ToDictionary(p => p.Name, p => p.GetValue(okResult.Value));
            ((bool)dict["ZipCreated"]).Should().BeTrue();
            (dict["Message"] as string).Should().Be("Images uploaded and zipped for training.");
            dict["ZipPath"].Should().NotBeNull();
            dict["ZipPath"].ToString().Should().Contain($"/training-zips/{userId}.zip");

            _mockUserProfileRepository.Verify(r => r.UpdateAsync(It.IsAny<UserProfile>()), Times.Once);
            // Verify CopyToAsync was called for each file
            foreach (var mf in mockFileMocks)
            {
                mf.Verify(f => f.CopyToAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()), Times.Once);
            }
        }

        [Fact]
        public async Task UploadImages_ReturnsStatusCode500_OnException()
        {
            // Arrange
            var userId = _fixture.Create<string>();
            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(new Claim[] { new Claim(ClaimTypes.NameIdentifier, userId) }, "mock")) }
            };
            _mockUserProfileRepository.Setup(r => r.GetByUserIdAsync(userId)).ThrowsAsync(new Exception("Simulated DB error"));

            var mockFile = new Mock<IFormFile>();
            mockFile.Setup(f => f.FileName).Returns("test.jpg");
            mockFile.Setup(f => f.Length).Returns(100);
            mockFile.Setup(f => f.OpenReadStream()).Returns(new MemoryStream(new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 }));

            var dto = _fixture.Build<UploadImagesDto>()
                             .With(d => d.Images, new List<IFormFile> { mockFile.Object })
                             .Create();

            // Act
            var result = await _controller.UploadImages(dto);

            // Assert
            result.Should().BeAssignableTo<ObjectResult>()
                  .Which.StatusCode.Should().Be(500);
            // _mockLogger.Verify(l => l.LogError(
            //     It.IsAny<EventId>(),
            //     It.IsAny<Exception>(),
            //     It.IsAny<string>(),
            //     It.IsAny<object[]>()
            // ), Times.Once);
        }

        // Tests for GetStyles
        [Fact]
        public async Task GetStyles_ReturnsOkWithActiveStyles()
        {
            // Arrange
            var activeStyles = _fixture.Build<Style>()
                                       .With(s => s.IsActive, true)
                                       .CreateMany(3).ToList();
            var inactiveStyle = _fixture.Build<Style>()
                                        .With(s => s.IsActive, false)
                                        .Create();
            var allStyles = new List<Style>();
            allStyles.AddRange(activeStyles);
            allStyles.Add(inactiveStyle);

            var stylesData = allStyles;
            _mockContext.Setup(c => c.Styles).Returns(GetMockDbSet(stylesData).Object);

            // Act
            var result = await _controller.GetStyles();

            // Assert
            result.Should().BeOfType<OkObjectResult>();
            var okResult = result.As<OkObjectResult>();
            var returnedStyles = (IEnumerable<string>)okResult.Value!;
            returnedStyles.Should().HaveCount(activeStyles.Count);
            returnedStyles.Should().Contain(activeStyles.Select(s => s.Name));
            returnedStyles.Should().NotContain(inactiveStyle.Name);
        }

        [Fact]
        public async Task GetStyles_ReturnsOkWithEmptyList_WhenNoActiveStyles()
        {
            // Arrange
            var inactiveStyle = _fixture.Build<Style>()
                                        .With(s => s.IsActive, false)
                                        .Create();
            var stylesData = new List<Style> { inactiveStyle };
            _mockContext.Setup(c => c.Styles).Returns(GetMockDbSet(stylesData).Object);

            // Act
            var result = await _controller.GetStyles();

            // Assert
            result.Should().BeOfType<OkObjectResult>();
            var okResult = result.As<OkObjectResult>();
            var returnedStyles = (IEnumerable<string>)okResult.Value!;
            returnedStyles.Should().BeEmpty();
        }
    // Helper method to mock DbSet for in-memory collections
        private static Mock<DbSet<T>> GetMockDbSet<T>(List<T> list) where T : class
        {
            var queryable = list.AsQueryable();
            var mockDbSet = new Mock<DbSet<T>>();

            mockDbSet.As<IQueryable<T>>().Setup(m => m.Provider).Returns(queryable.Provider);
            mockDbSet.As<IQueryable<T>>().Setup(m => m.Expression).Returns(queryable.Expression);
            mockDbSet.As<IQueryable<T>>().Setup(m => m.ElementType).Returns(queryable.ElementType);
            mockDbSet.As<IQueryable<T>>().Setup(m => m.GetEnumerator()).Returns(queryable.GetEnumerator());

            // For async operations
            mockDbSet.As<IAsyncEnumerable<T>>()
                .Setup(m => m.GetAsyncEnumerator(It.IsAny<CancellationToken>()))
                .Returns(new TestAsyncEnumerator<T>(queryable.GetEnumerator()));

            mockDbSet.As<IQueryable<T>>()
                .Setup(m => m.Provider)
                .Returns(new TestAsyncQueryProvider<T>(queryable.Provider));

            mockDbSet.Setup(d => d.Add(It.IsAny<T>())).Callback<T>((s) => list.Add(s));
            mockDbSet.Setup(d => d.AddRange(It.IsAny<IEnumerable<T>>())).Callback<IEnumerable<T>>((s) => list.AddRange(s));
            mockDbSet.Setup(d => d.Remove(It.IsAny<T>())).Callback<T>((s) => list.Remove(s));
            mockDbSet.Setup(d => d.RemoveRange(It.IsAny<IEnumerable<T>>())).Callback<IEnumerable<T>>((s) =>
            {
                foreach (var entity in s)
                {
                    list.Remove(entity);
                }
            });

            return mockDbSet;
        }

        // Helper classes for IAsyncEnumerable and IAsyncQueryProvider
        private class TestAsyncEnumerator<T> : IAsyncEnumerator<T>
        {
            private readonly IEnumerator<T> _inner;
            public TestAsyncEnumerator(IEnumerator<T> inner) => _inner = inner;
            public T Current => _inner.Current;
            public ValueTask<bool> MoveNextAsync() => new ValueTask<bool>(_inner.MoveNext());
            public ValueTask DisposeAsync() => new ValueTask();
        }

        private class TestAsyncQueryProvider<TEntity> : IAsyncQueryProvider
        {
            private readonly IQueryProvider _inner;

            internal TestAsyncQueryProvider(IQueryProvider inner) => _inner = inner;

            public IQueryable CreateQuery(Expression expression) => new TestAsyncEnumerable<TEntity>(expression);
            public IQueryable<TElement> CreateQuery<TElement>(Expression expression) => new TestAsyncEnumerable<TElement>(expression);
            public object? Execute(Expression expression) => _inner.Execute(expression);
            public TResult Execute<TResult>(Expression expression) => _inner.Execute<TResult>(expression);

            public TResult ExecuteAsync<TResult>(Expression expression, CancellationToken cancellationToken = default)
            {
                var expectedResultType = typeof(TResult).GetGenericArguments()[0];
                var executionResult = _inner.Execute(expression);
                var fromResultMethod = typeof(Task).GetMethod(nameof(Task.FromResult));
                if (fromResultMethod == null)
                    throw new InvalidOperationException("Task.FromResult method not found.");
                var genericMethod = fromResultMethod.MakeGenericMethod(expectedResultType);
                var task = genericMethod.Invoke(null, new[] { executionResult });
                return (TResult)task!;
            }
        }

        private class TestAsyncEnumerable<T> : EnumerableQuery<T>, IAsyncEnumerable<T>, IQueryable<T>
        {
            public TestAsyncEnumerable(IEnumerable<T> enumerable) : base(enumerable) { }
            public TestAsyncEnumerable(Expression expression) : base(expression) { }

            public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default)
            {
                return new TestAsyncEnumerator<T>(this.AsEnumerable().GetEnumerator());
            }

            IQueryProvider IQueryable.Provider => new TestAsyncQueryProvider<T>(this);
        }
    }
}
