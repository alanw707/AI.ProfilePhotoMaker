using Microsoft.AspNetCore.Mvc;
using AI.ProfilePhotoMaker.API.Services;

namespace AI.ProfilePhotoMaker.API.Controllers;

/// <summary>
/// Public blog endpoints — no authentication required.
/// Posts are loaded from Azure Blob Storage (blog/*.md) with local fallback.
/// </summary>
[Route("api/[controller]")]
[ApiController]
public class BlogController : ControllerBase
{
    private readonly IBlogContentService _blogService;
    private readonly ILogger<BlogController> _logger;

    public BlogController(IBlogContentService blogService, ILogger<BlogController> logger)
    {
        _blogService = blogService;
        _logger = logger;
    }

    /// <summary>
    /// Returns all published blog posts (summary only, no body content).
    /// Ordered by publish date descending.
    /// </summary>
    [HttpGet]
    [ResponseCache(Duration = 300)] // 5 min edge cache
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPosts(CancellationToken cancellationToken)
    {
        try
        {
            var posts = await _blogService.GetPublishedPostsAsync(cancellationToken);
            return Ok(new { success = true, data = posts });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load blog posts");
            return StatusCode(500, new { success = false, error = new { code = "InternalError", message = "Failed to load blog posts." } });
        }
    }

    /// <summary>
    /// Returns a single published blog post by slug (includes full HTML + markdown body).
    /// </summary>
    [HttpGet("{slug}")]
    [ResponseCache(Duration = 300)]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPost(string slug, CancellationToken cancellationToken)
    {
        try
        {
            var post = await _blogService.GetPostBySlugAsync(slug, cancellationToken);
            if (post == null)
            {
                return NotFound(new { success = false, error = new { code = "NotFound", message = $"Blog post '{slug}' not found." } });
            }

            return Ok(new { success = true, data = post });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load blog post {Slug}", slug);
            return StatusCode(500, new { success = false, error = new { code = "InternalError", message = "Failed to load blog post." } });
        }
    }
}
