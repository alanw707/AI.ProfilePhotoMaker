using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using AI.ProfilePhotoMaker.API.Data;
using AI.ProfilePhotoMaker.API.Models;
using AI.ProfilePhotoMaker.API.Models.DTOs;
using AI.ProfilePhotoMaker.API.Services.Storage;
using Microsoft.EntityFrameworkCore;

namespace AI.ProfilePhotoMaker.API.Services.ImageProcessing;

/// <summary>
/// OpenAI DALL-E 3 implementation for creative image enhancement and anime styles
/// </summary>
public class OpenAIImageGenerationService : IImageGenerationService
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<OpenAIImageGenerationService> _logger;
    private readonly ApplicationDbContext _context;
    private readonly IStorageService _storageService;

    public string Provider => "OpenAI";
    public bool SupportsWebhooks => false; // OpenAI uses synchronous responses
    public bool SupportsCustomModels => false; // OpenAI doesn't support custom model training

    public OpenAIImageGenerationService(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<OpenAIImageGenerationService> logger,
        ApplicationDbContext context,
        IStorageService storageService)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _logger = logger;
        _context = context;
        _storageService = storageService;

        // Configure HTTP client for OpenAI API
        _httpClient.BaseAddress = new Uri("https://api.openai.com/v1/");
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        
        var apiKey = _configuration["OpenAI:ApiKey"];
        if (!string.IsNullOrEmpty(apiKey))
        {
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        }
    }

    public async Task<ImageGenerationResult> EnhancePhotoAsync(EnhancePhotoRequest request)
    {
        var startTime = DateTime.UtcNow;
        
        try
        {
            _logger.LogInformation("Starting OpenAI enhancement for user {UserId} with type {EnhancementType}",
                request.UserId, request.EnhancementType);

            // Generate style-specific prompt
            var prompt = GenerateEnhancementPrompt(request.EnhancementType, request.ImageUrl);
            
            // Download the original image to modify
            using var originalImageStream = await DownloadImageStreamAsync(request.ImageUrl);
            
            // Prepare form data for images/edits endpoint
            using var formData = new MultipartFormDataContent();
            formData.Add(new StreamContent(originalImageStream), "image", "original.png");
            formData.Add(new StringContent(prompt), "prompt");
            formData.Add(new StringContent("1024x1024"), "size");
            formData.Add(new StringContent("1"), "n");
            formData.Add(new StringContent("url"), "response_format");

            var response = await _httpClient.PostAsync("images/edits", formData);
            response.EnsureSuccessStatusCode();

            var responseJson = await response.Content.ReadAsStringAsync();
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };
            var openAIResponse = JsonSerializer.Deserialize<OpenAIImageResponse>(responseJson, options);

            if (openAIResponse?.Data?.FirstOrDefault()?.Url == null)
            {
                throw new InvalidOperationException("No image URL returned from OpenAI");
            }

            var imageUrl = openAIResponse.Data.First().Url;
            
            // Download and store the image
            var storedImageUrl = await DownloadAndStoreImageAsync(imageUrl, request.UserId, request.EnhancementType);
            
            // Save to database
            await SaveEnhancedImageToDatabase(request.UserId, request.ImageUrl, storedImageUrl, request.EnhancementType);

            var processingTime = DateTime.UtcNow - startTime;

            _logger.LogInformation("OpenAI enhancement completed for user {UserId} in {ProcessingTime}ms",
                request.UserId, processingTime.TotalMilliseconds);

            return new ImageGenerationResult
            {
                Success = true,
                ImageUrls = new[] { storedImageUrl },
                Provider = Provider,
                EnhancementType = request.EnhancementType,
                ProcessingTime = processingTime,
                Metadata = new Dictionary<string, object>
                {
                    { "openai_revised_prompt", openAIResponse.Data.First().RevisedPrompt ?? prompt }
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to enhance image with OpenAI for user {UserId}", request.UserId);
            return new ImageGenerationResult
            {
                Success = false,
                ErrorMessage = ex.Message,
                Provider = Provider,
                EnhancementType = request.EnhancementType,
                ProcessingTime = DateTime.UtcNow - startTime
            };
        }
    }

    public async Task<ImageGenerationResult> GenerateStyledImageAsync(StyledGenerationRequest request)
    {
        // OpenAI doesn't support custom model training, so this method is not implemented
        throw new NotSupportedException("OpenAI service does not support custom model training. Use Replicate for styled generation with trained models.");
    }

    public async Task<IEnumerable<EnhancementType>> GetAvailableEnhancementTypesAsync()
    {
        return new[]
        {
            // Japanese Animation Styles
            new EnhancementType
            {
                Id = "chibi",
                Name = "Chibi Style",
                Description = "Super cute anime style with oversized head and tiny body",
                Category = "Japanese Animation",
                CreditCost = 2
            },
            new EnhancementType
            {
                Id = "studio_ghibli",
                Name = "Studio Ghibli",
                Description = "Miyazaki-inspired dreamy watercolor aesthetic",
                Category = "Japanese Animation", 
                CreditCost = 2
            },
            new EnhancementType
            {
                Id = "kawaii",
                Name = "Kawaii",
                Description = "Ultra-cute style with pastels and sparkles",
                Category = "Japanese Animation",
                CreditCost = 2
            },
            new EnhancementType
            {
                Id = "shoujo_manga",
                Name = "Shoujo Manga",
                Description = "Classic manga style with expressive eyes",
                Category = "Japanese Animation",
                CreditCost = 2
            },
            new EnhancementType
            {
                Id = "retro_90s_anime",
                Name = "90s Retro Anime",
                Description = "Nostalgic Sailor Moon era aesthetic",
                Category = "Japanese Animation",
                CreditCost = 2
            },
            
            // 3D Styles
            new EnhancementType
            {
                Id = "pixar_3d",
                Name = "Pixar 3D",
                Description = "Professional 3D animation style",
                Category = "3D Styles",
                CreditCost = 2
            },
            new EnhancementType
            {
                Id = "low_poly",
                Name = "Low Poly",
                Description = "Geometric low-polygon 3D style",
                Category = "3D Styles",
                CreditCost = 2
            },
            new EnhancementType
            {
                Id = "clay_animation",
                Name = "Clay Animation",
                Description = "Stop-motion clay figure look",
                Category = "3D Styles",
                CreditCost = 2
            },
            new EnhancementType
            {
                Id = "voxel_art",
                Name = "Voxel Art",
                Description = "Minecraft-style block art",
                Category = "3D Styles",
                CreditCost = 2
            }
        };
    }

    private string GenerateEnhancementPrompt(string enhancementType, string originalImageUrl)
    {
        // For images/edits endpoint - describe the desired transformation
        var basePrompt = "Transform into ";
        
        return enhancementType.ToLower() switch
        {
            "chibi" => basePrompt + "chibi anime style with oversized head, huge sparkling eyes, tiny body, extremely cute, soft pastel colors, kawaii aesthetic",
            
            "studio_ghibli" => basePrompt + "Studio Ghibli animation style, Hayao Miyazaki art aesthetic, soft watercolor painting effect, dreamy atmosphere with gentle features, whimsical and nostalgic feeling, hand-drawn animation quality with natural lighting",
            
            "kawaii" => basePrompt + "kawaii anime style illustration, ultra cute aesthetic with pastel pink and soft colors, sparkly large eyes with highlights, blushing cheeks, decorative hearts and stars, magical girl anime influence, adorable expression",
            
            "shoujo_manga" => basePrompt + "shoujo manga art style, dramatic expressive eyes with detailed iris patterns, flowing hair with highlights, soft facial features, romantic aesthetic, screen tone shading effects, classic manga illustration style",
            
            "retro_90s_anime" => basePrompt + "90s retro anime style, Sailor Moon and Dragon Ball Z era aesthetic, bold line art, vibrant saturated colors, cel-shaded animation look, nostalgic anime character design with classic proportions",
            
            "pixar_3d" => basePrompt + "Pixar-quality 3D animation style, professional computer graphics with soft lighting, detailed textures, expressive cartoon features, high-quality rendering with depth and dimension",
            
            "low_poly" => basePrompt + "low poly 3D art style, geometric faceted design with angular features, minimalist polygon aesthetic, clean geometric shapes, modern digital art style with flat colors",
            
            "clay_animation" => basePrompt + "clay animation style, stop-motion figure made of modeling clay, tactile handcrafted appearance, slightly imperfect organic shapes, Wallace and Gromit aesthetic",
            
            "voxel_art" => basePrompt + "voxel art style, Minecraft-inspired blocky 3D design, pixelated cube-based construction, retro video game aesthetic with distinct block patterns",
            
            _ => basePrompt + "professional enhanced portrait with improved lighting, clarity, and artistic quality"
        };
    }

    private async Task<Stream> DownloadImageStreamAsync(string imageUrl)
    {
        var imageResponse = await _httpClient.GetAsync(imageUrl);
        imageResponse.EnsureSuccessStatusCode();
        return await imageResponse.Content.ReadAsStreamAsync();
    }

    private async Task<string> DownloadAndStoreImageAsync(string imageUrl, string userId, string enhancementType)
    {
        // Download image from OpenAI
        var imageResponse = await _httpClient.GetAsync(imageUrl);
        imageResponse.EnsureSuccessStatusCode();

        using var imageStream = await imageResponse.Content.ReadAsStreamAsync();
        
        // Generate storage path
        var fileName = $"openai_{enhancementType}_{Guid.NewGuid()}.png";
        var storagePath = $"users/{userId}/enhanced/{fileName}";
        
        // Store in blob storage
        await _storageService.SaveImageToPathAsync(imageStream, storagePath);
        
        // Return public URL
        return _storageService.GetImageUrl(storagePath);
    }

    private async Task SaveEnhancedImageToDatabase(string userId, string originalUrl, string enhancedUrl, string enhancementType)
    {
        var userProfile = await _context.UserProfiles.FirstOrDefaultAsync(u => u.UserId == userId);
        if (userProfile == null) return;

        var processedImage = new ProcessedImage
        {
            OriginalImageUrl = originalUrl,
            ProcessedImageUrl = enhancedUrl,
            Style = enhancementType,
            UserProfileId = userProfile.Id,
            CreatedAt = DateTime.UtcNow,
            IsOriginalUpload = false,
            IsGenerated = true,
            IsEnhanced = true,
            Provider = Provider,
            EnhancementType = enhancementType
        };

        processedImage.SetScheduledDeletionDate();

        _context.ProcessedImages.Add(processedImage);
        await _context.SaveChangesAsync();
    }

    // OpenAI API response models
    private class OpenAIImageResponse
    {
        public OpenAIImageData[]? Data { get; set; }
    }

    private class OpenAIImageData
    {
        public string? Url { get; set; }
        public string? RevisedPrompt { get; set; }
    }
}