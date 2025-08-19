using Azure.Storage.Sas;

namespace AI.ProfilePhotoMaker.API.Services.Storage;

/// <summary>
/// Abstraction layer for storage operations that can be implemented for local storage or cloud storage (Azure Blob)
/// </summary>
public interface IStorageService
{
    /// <summary>
    /// Saves an image stream to storage with the specified filename and user context
    /// </summary>
    /// <param name="imageStream">The image data stream</param>
    /// <param name="fileName">The filename (without path)</param>
    /// <param name="userId">The user ID for folder organization</param>
    /// <param name="folderType">The folder type (e.g., "enhanced", "uploads", "generated") for organizing different image types</param>
    /// <returns>The storage path/URL for the saved image</returns>
    Task<string> SaveImageAsync(Stream imageStream, string fileName, string userId, string folderType = "generated");

    /// <summary>
    /// Saves an image stream to storage with a specific storage path
    /// </summary>
    /// <param name="imageStream">The image data stream</param>
    /// <param name="storagePath">The full storage path where the image should be saved</param>
    /// <returns>The storage path of the saved image</returns>
    Task<string> SaveImageToPathAsync(Stream imageStream, string storagePath);

    /// <summary>
    /// Retrieves an image stream from storage
    /// </summary>
    /// <param name="storagePath">The storage path/URL</param>
    /// <returns>Image stream if found, null if not found</returns>
    Task<Stream?> GetImageAsync(string storagePath);

    /// <summary>
    /// Deletes an image from storage
    /// </summary>
    /// <param name="storagePath">The storage path/URL</param>
    /// <returns>True if deleted successfully, false otherwise</returns>
    Task<bool> DeleteImageAsync(string storagePath);

    /// <summary>
    /// Checks if an image exists in storage
    /// </summary>
    /// <param name="storagePath">The storage path/URL</param>
    /// <returns>True if exists, false otherwise</returns>
    Task<bool> ExistsAsync(string storagePath);

    /// <summary>
    /// Gets the public URL for accessing an image
    /// Each implementation provides environment-appropriate URLs
    /// </summary>
    /// <param name="storagePath">The storage path</param>
    /// <returns>Public URL for the image</returns>
    string GetImageUrl(string storagePath);

    /// <summary>
    /// Lists all images for a specific user
    /// </summary>
    /// <param name="userId">The user ID</param>
    /// <returns>List of storage paths for the user's images</returns>
    Task<List<string>> ListUserImagesAsync(string userId);

    /// <summary>
    /// Gets the file information for a stored image
    /// </summary>
    /// <param name="storagePath">The storage path</param>
    /// <returns>File information including size, creation date, etc.</returns>
    Task<StorageFileInfo?> GetFileInfoAsync(string storagePath);

    /// <summary>
    /// Generates a SAS URL for external API access (like Replicate)
    /// </summary>
    /// <param name="storagePath">The storage path</param>
    /// <param name="expiry">How long the SAS URL should be valid</param>
    /// <param name="permissions">Permissions for the SAS URL (default: Read)</param>
    /// <returns>SAS URL for external access</returns>
    Task<string> GenerateSasUrlAsync(string storagePath, TimeSpan expiry, Azure.Storage.Sas.BlobSasPermissions permissions = Azure.Storage.Sas.BlobSasPermissions.Read);

    /// <summary>
    /// Saves a ZIP file stream to storage
    /// </summary>
    /// <param name="zipStream">The ZIP file stream</param>
    /// <param name="storagePath">The storage path for the ZIP file</param>
    /// <returns>The storage path of the saved ZIP file</returns>
    Task<string> SaveZipAsync(Stream zipStream, string storagePath);

    /// <summary>
    /// Deletes all files in a directory/prefix
    /// </summary>
    /// <param name="directoryPath">The directory path/prefix to delete</param>
    /// <returns>True if deletion was successful</returns>
    Task<bool> DeleteDirectoryAsync(string directoryPath);

    /// <summary>
    /// Lists all files with a given prefix
    /// </summary>
    /// <param name="prefix">The path prefix to search for</param>
    /// <returns>List of file paths matching the prefix</returns>
    Task<List<string>> ListFilesAsync(string prefix);
}

/// <summary>
/// Information about a file in storage
/// </summary>
public class StorageFileInfo
{
    public string FileName { get; set; } = string.Empty;
    public long Size { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime ModifiedAt { get; set; }
    public string ContentType { get; set; } = string.Empty;
}