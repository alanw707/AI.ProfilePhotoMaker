using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using AI.ProfilePhotoMaker.API.Controllers;
using AI.ProfilePhotoMaker.API.Models.DTOs;
using AI.ProfilePhotoMaker.API.Services;
using FluentAssertions;

namespace AI.ProfilePhotoMaker.API.Tests.Controllers;

public class BlogControllerTests
{
    private readonly Mock<IBlogContentService> _service = new();
    private readonly Mock<ILogger<BlogController>> _logger = new();

    private BlogController CreateController()
    {
        var ctrl = new BlogController(_service.Object, _logger.Object);
        // Provide a real HttpContext so Response.Headers works
        ctrl.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext(),
        };
        return ctrl;
    }

    // ────────────────────────────────────────────────────────────────────────
    // GET /api/blog
    // ────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetPosts_ServiceReturnsPosts_Returns200WithData()
    {
        var posts = new List<BlogPostSummaryDto>
        {
            new() { Slug = "post-1", Title = "Post 1", DateIso = "2026-01-01T00:00:00Z" },
            new() { Slug = "post-2", Title = "Post 2", DateIso = "2026-02-01T00:00:00Z" },
        };
        _service.Setup(s => s.GetPublishedPostsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(posts);

        var result = await CreateController().GetPosts(CancellationToken.None);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var value = ok.Value!;
        var json = System.Text.Json.JsonSerializer.Serialize(value);
        json.Should().Contain("post-1");
        json.Should().Contain("Post 1");
    }

    [Fact]
    public async Task GetPosts_ServiceReturnsEmpty_Returns200EmptyList()
    {
        _service.Setup(s => s.GetPublishedPostsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<BlogPostSummaryDto>());

        var result = await CreateController().GetPosts(CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task GetPosts_ServiceThrows_Returns500()
    {
        _service.Setup(s => s.GetPublishedPostsAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Storage down"));

        var result = await CreateController().GetPosts(CancellationToken.None);

        var status = result.Should().BeOfType<ObjectResult>().Subject;
        status.StatusCode.Should().Be(500);
    }

    [Fact]
    public async Task GetPosts_OnError_SetsNoCacheHeader()
    {
        _service.Setup(s => s.GetPublishedPostsAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Storage down"));

        var ctrl = CreateController();
        await ctrl.GetPosts(CancellationToken.None);

        ctrl.Response.Headers.CacheControl.ToString().Should().Contain("no-store");
    }

    [Fact]
    public async Task GetPosts_OnSuccess_SetsCacheHeader()
    {
        _service.Setup(s => s.GetPublishedPostsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<BlogPostSummaryDto>());

        var ctrl = CreateController();
        await ctrl.GetPosts(CancellationToken.None);

        ctrl.Response.Headers.CacheControl.ToString().Should().Contain("public");
        ctrl.Response.Headers.CacheControl.ToString().Should().Contain("max-age=300");
    }

    // ────────────────────────────────────────────────────────────────────────
    // GET /api/blog/{slug}
    // ────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetPost_PostExists_Returns200WithPost()
    {
        var post = new BlogPostDto
        {
            Slug = "my-post",
            Title = "My Post",
            ContentHtml = "<p>Hello</p>",
            ContentMarkdown = "Hello",
        };
        _service.Setup(s => s.GetPostBySlugAsync("my-post", It.IsAny<CancellationToken>()))
            .ReturnsAsync(post);

        var result = await CreateController().GetPost("my-post", CancellationToken.None);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var json = System.Text.Json.JsonSerializer.Serialize(ok.Value!);
        json.Should().Contain("my-post");
    }

    [Fact]
    public async Task GetPost_PostNotFound_Returns404()
    {
        _service.Setup(s => s.GetPostBySlugAsync("unknown", It.IsAny<CancellationToken>()))
            .ReturnsAsync((BlogPostDto?)null);

        var result = await CreateController().GetPost("unknown", CancellationToken.None);

        var notFound = result.Should().BeOfType<NotFoundObjectResult>().Subject;
        notFound.StatusCode.Should().Be(404);
    }

    [Fact]
    public async Task GetPost_NotFound_SetsNoCacheHeader()
    {
        _service.Setup(s => s.GetPostBySlugAsync("unknown", It.IsAny<CancellationToken>()))
            .ReturnsAsync((BlogPostDto?)null);

        var ctrl = CreateController();
        await ctrl.GetPost("unknown", CancellationToken.None);

        ctrl.Response.Headers.CacheControl.ToString().Should().Contain("no-store");
    }

    [Fact]
    public async Task GetPost_ServiceThrows_Returns500()
    {
        _service.Setup(s => s.GetPostBySlugAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Storage down"));

        var result = await CreateController().GetPost("my-post", CancellationToken.None);

        var status = result.Should().BeOfType<ObjectResult>().Subject;
        status.StatusCode.Should().Be(500);
    }
}
