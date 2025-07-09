using Xunit;
using Moq;
using AutoFixture;
using FluentAssertions;
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
            _mockUserProfileRepository = new Mock<IUserProfileRepository>();
            
            // Mock ApplicationDbContext
            // This requires a bit more setup as DbContext is not easily mockable directly.
            // For now, we'll mock the DbSet properties it exposes.
            // A common pattern is to use an in-memory database for DbContext testing,
            // but for unit tests focusing on the controller logic, mocking the repository
            // and DbSet is often sufficient.
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options();
            _mockContext = new Mock<ApplicationDbContext>(options);

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
    }
}
