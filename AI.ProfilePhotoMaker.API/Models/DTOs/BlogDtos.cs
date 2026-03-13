namespace AI.ProfilePhotoMaker.API.Models.DTOs;

public class BlogPostSummaryDto
{
    public string Slug { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string DateIso { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public List<string> Tags { get; set; } = new();
}

public class BlogPostDto : BlogPostSummaryDto
{
    public string ContentHtml { get; set; } = string.Empty;
    public string ContentMarkdown { get; set; } = string.Empty;
}
