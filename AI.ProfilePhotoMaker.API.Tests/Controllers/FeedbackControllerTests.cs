using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using AI.ProfilePhotoMaker.API.Controllers;
using AI.ProfilePhotoMaker.API.Data;
using AI.ProfilePhotoMaker.API.Models;
using AI.ProfilePhotoMaker.API.Models.DTOs;
using AI.ProfilePhotoMaker.API.Services.Notifications;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace AI.ProfilePhotoMaker.API.Tests.Controllers
{
    public class FeedbackControllerTests
    {
        private readonly Mock<ApplicationDbContext> _mockContext;
        private readonly Mock<ILogger<FeedbackController>> _mockLogger;
        private readonly Mock<IEmailNotificationService> _mockEmailNotifications;
        private readonly FeedbackController _controller;
        private readonly List<FeedbackSubmission> _feedbackSubmissions;

        public FeedbackControllerTests()
        {
            _mockContext = new Mock<ApplicationDbContext>(new DbContextOptions<ApplicationDbContext>());
            _mockLogger = new Mock<ILogger<FeedbackController>>();
            _mockEmailNotifications = new Mock<IEmailNotificationService>();
            _controller = new FeedbackController(_mockLogger.Object, _mockContext.Object, _mockEmailNotifications.Object);

            _feedbackSubmissions = new List<FeedbackSubmission>();
            _mockContext.Setup(c => c.FeedbackSubmissions).Returns(GetMockDbSet(_feedbackSubmissions).Object);
            _mockContext.Setup(c => c.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
            _mockContext.Setup(c => c.Users).Returns(GetMockDbSet(new List<ApplicationUser>()).Object);

            var userId = Guid.NewGuid().ToString("N");
            var claimsPrincipal = new ClaimsPrincipal(new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, userId),
            }, "mock"));

            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = claimsPrincipal }
            };
        }

        [Fact]
        public async Task SubmitFeedback_ReturnsUnauthorized_WhenUserIdMissing()
        {
            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity()) }
            };

            var result = await _controller.SubmitFeedback(new SubmitFeedbackRequestDto
            {
                Category = "Bug",
                Message = "Something broke in the training flow.",
            });

            result.Should().BeOfType<UnauthorizedObjectResult>();
        }

        [Fact]
        public async Task SubmitFeedback_PersistsSubmission_AndReturnsSuccess()
        {
            var result = await _controller.SubmitFeedback(new SubmitFeedbackRequestDto
            {
                Category = "Bug",
                Message = "When I try to generate images, it fails intermittently.",
                PageUrl = "https://example.test/app/enhance",
                UserAgent = "TestAgent/1.0",
            });

            result.Should().BeOfType<OkObjectResult>();
            _feedbackSubmissions.Should().HaveCount(1);

            var ok = (OkObjectResult)result;
            ok.Value.Should().NotBeNull();

            var success = GetPropertyValue<bool>(ok.Value!, "success");
            success.Should().BeTrue();

            var data = GetPropertyValue<object>(ok.Value!, "data");
            data.Should().NotBeNull();

            var id = GetPropertyValue<Guid>(data!, "id");
            id.Should().NotBe(Guid.Empty);
            _feedbackSubmissions[0].Id.Should().Be(id);

            _mockEmailNotifications.Verify(
                s => s.SendSupportFeedbackReceivedAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<FeedbackSubmission>()),
                Times.Once);
        }

        private static T GetPropertyValue<T>(object obj, string propertyName)
        {
            var prop = obj.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
            prop.Should().NotBeNull($"Expected property '{propertyName}' on type '{obj.GetType().Name}'.");
            return (T)prop!.GetValue(obj)!;
        }

        private static Mock<DbSet<T>> GetMockDbSet<T>(List<T> list) where T : class
        {
            var queryable = list.AsQueryable();
            var mockDbSet = new Mock<DbSet<T>>();

            mockDbSet.As<IQueryable<T>>().Setup(m => m.Provider).Returns(queryable.Provider);
            mockDbSet.As<IQueryable<T>>().Setup(m => m.Expression).Returns(queryable.Expression);
            mockDbSet.As<IQueryable<T>>().Setup(m => m.ElementType).Returns(queryable.ElementType);
            mockDbSet.As<IQueryable<T>>().Setup(m => m.GetEnumerator()).Returns(queryable.GetEnumerator());

            mockDbSet.As<IAsyncEnumerable<T>>()
                .Setup(m => m.GetAsyncEnumerator(It.IsAny<CancellationToken>()))
                .Returns(new TestAsyncEnumerator<T>(queryable.GetEnumerator()));

            mockDbSet.As<IQueryable<T>>()
                .Setup(m => m.Provider)
                .Returns(new TestAsyncQueryProvider<T>(queryable.Provider));

            mockDbSet.Setup(d => d.Add(It.IsAny<T>())).Callback<T>(s => list.Add(s));
            mockDbSet.Setup(d => d.AddRange(It.IsAny<IEnumerable<T>>())).Callback<IEnumerable<T>>(s => list.AddRange(s));
            mockDbSet.Setup(d => d.Remove(It.IsAny<T>())).Callback<T>(s => list.Remove(s));
            mockDbSet.Setup(d => d.RemoveRange(It.IsAny<IEnumerable<T>>())).Callback<IEnumerable<T>>(s =>
            {
                foreach (var entity in s)
                {
                    list.Remove(entity);
                }
            });

            return mockDbSet;
        }

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
                => new TestAsyncEnumerator<T>(this.AsEnumerable().GetEnumerator());
            IQueryProvider IQueryable.Provider => new TestAsyncQueryProvider<T>(this);
        }
    }
}
