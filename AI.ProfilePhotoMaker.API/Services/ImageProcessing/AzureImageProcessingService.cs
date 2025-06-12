using AI.ProfilePhotoMaker.API.Models.DTOs;

namespace AI.ProfilePhotoMaker.API.Services.ImageProcessing;

public class AzureImageProcessingService : IImageProcessingService
{
    private readonly IConfiguration _configuration;
    private readonly IReplicateApiClient _replicateClient;

    public AzureImageProcessingService(IConfiguration configuration, IReplicateApiClient replicateClient)
    {
        _configuration = configuration;
        _replicateClient = replicateClient;
    }

    public async Task<string> ProcessImageAsync(IFormFile image, string userId, string styleOption)
    {
        // Implement AI processing using Azure Cognitive Services
        // Store the processed image and return its URL
        return await Task.FromResult("https://example.com/processed-image.jpg");
    }

    public async Task<IEnumerable<string>> GetAvailableStylesAsync()
    {
        // Return available style options
        return new List<string> { "Professional", "Casual", "Artistic", "Vintage", "Corporate", "Creative" };
    }

    public async Task<string> GenerateImageAsync(GenerateImagesRequestDto request)
    {
        // Use the Replicate client to generate images with the trained model
        var result = await _replicateClient.GenerateImagesAsync(request);
        return result;
    }
}