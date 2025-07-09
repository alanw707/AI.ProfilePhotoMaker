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

                return (TResult)typeof(Task).GetMethod(nameof(Task.FromResult))
                    .MakeGenericMethod(expectedResultType)
                    .Invoke(null, new[] { executionResult });
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
