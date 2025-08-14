namespace AI.ProfilePhotoMaker.API.Services.ImageProcessing;

/// <summary>
/// Result of image download operation
/// </summary>
public class ImageDownloadResult
{
    public string StoragePath { get; set; } = string.Empty; // Changed from LocalPath to StoragePath for cloud storage
    public string LocalPath { get; set; } = string.Empty; // Kept for backward compatibility
    public string FileName { get; set; } = string.Empty;
    public bool Success { get; set; }
}

/// <summary>
/// Service for downloading and storing images from external URLs
/// </summary>
public interface IImageDownloadService
{
    /// <summary>
    /// Downloads images from URLs and stores them locally with filename tracking
    /// </summary>
    /// <param name="imageUrls">List of image URLs to download</param>
    /// <param name="userId">User ID for directory organization</param>
    /// <param name="style">Style name for file organization</param>
    /// <returns>List of download results containing paths and filenames</returns>
    Task<List<ImageDownloadResult>> DownloadImagesWithDetailsAsync(List<string> imageUrls, string userId, string style);

    /// <summary>
    /// Downloads a single image from URL and stores it locally
    /// </summary>
    /// <param name="imageUrl">Image URL to download</param>
    /// <param name="userId">User ID for directory organization</param>
    /// <param name="style">Style name for file organization</param>
    /// <param name="fileName">Optional custom filename (without extension)</param>
    /// <returns>Local file path if successful, null if failed</returns>
    Task<string?> DownloadImageAsync(string imageUrl, string userId, string style, string? fileName = null);

    /// <summary>
    /// Ensures the generated images directory exists for a user
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <returns>Full path to the user's generated images directory</returns>
    string EnsureGeneratedImagesDirectory(string userId);

    /// <summary>
    /// Validates if an image URL is accessible and downloadable
    /// </summary>
    /// <param name="imageUrl">Image URL to validate</param>
    /// <returns>True if URL is accessible, false otherwise</returns>
    Task<bool> ValidateImageUrlAsync(string imageUrl);
}