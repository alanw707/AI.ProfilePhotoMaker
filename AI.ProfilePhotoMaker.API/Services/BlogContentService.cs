using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using AI.ProfilePhotoMaker.API.Models.DTOs;
using AI.ProfilePhotoMaker.API.Services.Storage;
using Markdig;

namespace AI.ProfilePhotoMaker.API.Services;

public interface IBlogContentService
{
    Task<List<BlogPostSummaryDto>> GetPublishedPostsAsync(CancellationToken cancellationToken = default);
    Task<BlogPostDto?> GetPostBySlugAsync(string slug, CancellationToken cancellationToken = default);
}

public class BlogContentService : IBlogContentService
{
    private readonly IStorageService _storageService;
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<BlogContentService> _logger;
    private readonly MarkdownPipeline _markdownPipeline;

    private const string BlogPrefix = "blog/";
    private const string LocalBlogFolder = "content/blog";

    public BlogContentService(
        IStorageService storageService,
        IWebHostEnvironment environment,
        ILogger<BlogContentService> logger)
    {
        _storageService = storageService;
        _environment = environment;
        _logger = logger;
        _markdownPipeline = new MarkdownPipelineBuilder()
            .UseAdvancedExtensions()
            .Build();
    }

    public async Task<List<BlogPostSummaryDto>> GetPublishedPostsAsync(CancellationToken cancellationToken = default)
    {
        var posts = new List<BlogPostSummaryDto>();

        foreach (var source in await LoadAllPostSourcesAsync(cancellationToken))
        {
            var parsed = ParsePost(source.path, source.markdown);
            if (parsed == null || !parsed.Published)
            {
                continue;
            }

            posts.Add(ToSummaryDto(parsed));
        }

        return posts
            .OrderByDescending(p => p.DateIso)
            .ToList();
    }

    public async Task<BlogPostDto?> GetPostBySlugAsync(string slug, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(slug))
        {
            return null;
        }

        // Sanitize slug to prevent path traversal attacks
        slug = SanitizeSlug(slug);
        if (string.IsNullOrEmpty(slug))
        {
            return null;
        }

        var blogPath = $"{BlogPrefix}{slug}.md";
        var markdown = await LoadMarkdownByPathAsync(blogPath, cancellationToken)
            ?? await LoadLocalFallbackMarkdownAsync(slug, cancellationToken);

        if (string.IsNullOrWhiteSpace(markdown))
        {
            return null;
        }

        var parsed = ParsePost(blogPath, markdown);
        if (parsed == null || !parsed.Published)
        {
            return null;
        }

        return new BlogPostDto
        {
            Slug = parsed.Slug,
            Title = parsed.Title,
            Description = parsed.Description,
            DateIso = parsed.PublishedAt.ToString("o"),
            Author = parsed.Author,
            Tags = parsed.Tags,
            ContentMarkdown = parsed.Body,
            ContentHtml = Markdown.ToHtml(parsed.Body, _markdownPipeline),
        };
    }

    private async Task<List<(string path, string markdown)>> LoadAllPostSourcesAsync(CancellationToken cancellationToken)
    {
        var items = new List<(string path, string markdown)>();
        var seenSlugs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var blobFiles = await _storageService.ListFilesAsync(BlogPrefix);
        foreach (var file in blobFiles.Where(f => f.EndsWith(".md", StringComparison.OrdinalIgnoreCase)))
        {
            var markdown = await LoadMarkdownByPathAsync(file, cancellationToken);
            if (string.IsNullOrWhiteSpace(markdown))
            {
                continue;
            }

            var slug = Path.GetFileNameWithoutExtension(file);
            seenSlugs.Add(slug);
            items.Add((file, markdown));
        }

        var localDir = Path.Combine(_environment.ContentRootPath, LocalBlogFolder);
        if (Directory.Exists(localDir))
        {
            foreach (var file in Directory.GetFiles(localDir, "*.md", SearchOption.TopDirectoryOnly))
            {
                var slug = Path.GetFileNameWithoutExtension(file);
                if (seenSlugs.Contains(slug))
                {
                    continue;
                }

                var markdown = await File.ReadAllTextAsync(file, cancellationToken);
                items.Add(($"{BlogPrefix}{slug}.md", markdown));
            }
        }

        return items;
    }

    private async Task<string?> LoadMarkdownByPathAsync(string path, CancellationToken cancellationToken)
    {
        try
        {
            await using var stream = await _storageService.GetImageAsync(path);
            if (stream == null)
            {
                return null;
            }

            using var reader = new StreamReader(stream, Encoding.UTF8);
            return await reader.ReadToEndAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load blog markdown from storage path {Path}", path);
            return null;
        }
    }

    private async Task<string?> LoadLocalFallbackMarkdownAsync(string slug, CancellationToken cancellationToken)
    {
        var file = Path.Combine(_environment.ContentRootPath, LocalBlogFolder, $"{slug}.md");
        if (!File.Exists(file))
        {
            return null;
        }

        return await File.ReadAllTextAsync(file, cancellationToken);
    }

    private BlogPostParsed? ParsePost(string path, string markdown)
    {
        if (string.IsNullOrWhiteSpace(markdown))
        {
            return null;
        }

        var slug = Path.GetFileNameWithoutExtension(path);
        var body = markdown.Trim();
        var meta = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var tags = new List<string>();

        if (body.StartsWith("---\n") || body.StartsWith("---\r\n"))
        {
            var delimiter = body.IndexOf("\n---", 4, StringComparison.Ordinal);
            if (delimiter > -1)
            {
                var frontmatter = body.Substring(4, delimiter - 4).Trim();
                body = body[(delimiter + 4)..].Trim();
                ParseFrontmatter(frontmatter, meta, tags);
            }
        }

        var title = meta.TryGetValue("title", out var titleValue) && !string.IsNullOrWhiteSpace(titleValue)
            ? titleValue.Trim()
            : slug;
        var description = meta.TryGetValue("description", out var descriptionValue)
            ? descriptionValue.Trim()
            : string.Empty;
        var author = meta.TryGetValue("author", out var authorValue) && !string.IsNullOrWhiteSpace(authorValue)
            ? authorValue.Trim()
            : "AI Profile Photo Maker";
        var published = !meta.TryGetValue("published", out var publishedValue)
            || !publishedValue.Equals("false", StringComparison.OrdinalIgnoreCase);
        var publishedAt = meta.TryGetValue("publishedAt", out var dateValue) && DateTime.TryParse(dateValue, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedDate)
            ? DateTime.SpecifyKind(parsedDate, DateTimeKind.Utc)
            : DateTime.UtcNow;
        if (meta.TryGetValue("slug", out var slugValue) && !string.IsNullOrWhiteSpace(slugValue))
        {
            slug = slugValue.Trim();
        }

        return new BlogPostParsed
        {
            Slug = slug,
            Title = title,
            Description = description,
            Author = author,
            Published = published,
            PublishedAt = publishedAt,
            Tags = tags,
            Body = body,
        };
    }

    private static void ParseFrontmatter(string frontmatter, Dictionary<string, string> meta, List<string> tags)
    {
        string? activeListKey = null;

        foreach (var rawLine in frontmatter.Split('\n'))
        {
            var line = rawLine.TrimEnd('\r');
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            if (line.StartsWith("- ") && string.Equals(activeListKey, "tags", StringComparison.OrdinalIgnoreCase))
            {
                var tag = line[2..].Trim();
                if (!string.IsNullOrWhiteSpace(tag))
                {
                    tags.Add(tag);
                }
                continue;
            }

            var separator = line.IndexOf(':');
            if (separator <= 0)
            {
                continue;
            }

            var key = line[..separator].Trim();
            // Use remainder after first colon; quoted values may contain colons (e.g. title: "Foo: Bar")
            var rawValue = line[(separator + 1)..].Trim();
            // If value is quoted, extract full quoted string to preserve inner colons
            string value;
            if (rawValue.StartsWith("\"") && rawValue.EndsWith("\"") && rawValue.Length >= 2)
            {
                value = rawValue[1..^1];
            }
            else if (rawValue.StartsWith("'") && rawValue.EndsWith("'") && rawValue.Length >= 2)
            {
                value = rawValue[1..^1];
            }
            else
            {
                value = rawValue;
            }
            activeListKey = key;

            if (string.Equals(key, "tags", StringComparison.OrdinalIgnoreCase))
            {
                if (!string.IsNullOrWhiteSpace(value) && value != "[]")
                {
                    foreach (var part in value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                    {
                        tags.Add(part.Trim());
                    }
                }
                continue;
            }

            meta[key] = value;
        }
    }

    /// <summary>
    /// Strips path separators and directory traversal sequences from a slug.
    /// Returns only the filename portion, lowercase, alphanumeric + hyphens.
    /// </summary>
    private static string SanitizeSlug(string slug)
    {
        // Take only the filename component (strips ../ etc.)
        slug = Path.GetFileNameWithoutExtension(slug);

        // Allow only alphanumeric, hyphens, underscores
        slug = Regex.Replace(slug, @"[^a-zA-Z0-9\-_]", string.Empty);

        return slug.ToLowerInvariant();
    }

    private static BlogPostSummaryDto ToSummaryDto(BlogPostParsed parsed) => new()
    {
        Slug = parsed.Slug,
        Title = parsed.Title,
        Description = parsed.Description,
        DateIso = parsed.PublishedAt.ToString("o"),
        Author = parsed.Author,
        Tags = parsed.Tags,
    };

    private sealed class BlogPostParsed
    {
        public string Slug { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public DateTime PublishedAt { get; set; }
        public string Author { get; set; } = string.Empty;
        public List<string> Tags { get; set; } = new();
        public string Body { get; set; } = string.Empty;
        public bool Published { get; set; } = true;
    }
}
