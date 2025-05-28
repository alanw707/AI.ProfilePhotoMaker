namespace AI.ProfilePhotoMaker.API.Services.ImageProcessing;

public interface IImageProcessingService
{
    Task<string> ProcessImageAsync(IFormFile image, string userId, string styleOption);
    Task<IEnumerable<string>> GetAvailableStylesAsync();
}