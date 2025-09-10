using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using AI.ProfilePhotoMaker.API.Models.DTOs;
using AI.ProfilePhotoMaker.API.Services.Storage;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Formats.Png;

namespace AI.ProfilePhotoMaker.API.Services.ImageProcessing;

/// <summary>
/// OpenAI DALL-E implementation for creative image enhancement using HTTP client
/// </summary>
public class OpenAIImageGenerationService : IImageProcessingService
{
    private readonly HttpClient _openAiClient;
    private readonly HttpClient _downloadClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<OpenAIImageGenerationService> _logger;
    private readonly IStorageService _storageService;

    public OpenAIImageGenerationService(
        HttpClient httpClient,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<OpenAIImageGenerationService> logger,
        IStorageService storageService)
    {
        _openAiClient = httpClient;
        _downloadClient = httpClientFactory.CreateClient(); // plain client: no OpenAI Authorization
        _configuration = configuration;
        _logger = logger;
        _storageService = storageService;

        // Configure HTTP client for OpenAI API
        _openAiClient.BaseAddress = new Uri("https://api.openai.com/v1/");
        _openAiClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        
        // Load API key with robust precedence: env var OPENAI_API_KEY, then config OpenAI:ApiKey
        var apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY")
                     ?? _configuration["OpenAI:ApiKey"];
        if (string.IsNullOrEmpty(apiKey))
        {
            _logger.LogError("OpenAI API key not configured - OpenAI service cannot be initialized");
            throw new InvalidOperationException("OpenAI API key is required but not configured. Please set OpenAI:ApiKey in configuration.");
        }
        
        _openAiClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        _logger.LogInformation("OpenAI API configured successfully");
    }

    // Backwards-compatible constructor for tests and simple manual instantiation
    // Reuses the supplied HttpClient for BOTH OpenAI and download paths
    // so tests that mock HttpMessageHandler still work without hitting network.
    public OpenAIImageGenerationService(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<OpenAIImageGenerationService> logger,
        IStorageService storageService)
        : this(httpClient, new PassthroughHttpClientFactory(httpClient), configuration, logger, storageService)
    {
    }

    // IHttpClientFactory that returns the provided client (used for tests)
    private sealed class PassthroughHttpClientFactory : IHttpClientFactory
    {
        private readonly HttpClient _client;
        public PassthroughHttpClientFactory(HttpClient client) => _client = client;
        public HttpClient CreateClient(string name) => _client;
    }

    /// <summary>
    /// Enhances photo quality using OpenAI DALL-E image transformation and returns base64 data URL format
    /// </summary>
    /// <param name="request">Enhancement request with image URL and style preferences</param>
    /// <returns>Base64 data URL in format: data:image/png;base64,{base64data}</returns>
    public async Task<string> EnhancePhotoQualityAsync(EnhancePhotoRequestDto request)
    {
        var startTime = DateTime.UtcNow;
        
        try
        {
            _logger.LogInformation("Starting OpenAI photo transformation type={Type}, imageUrl={ImageUrl}", 
                request.EnhancementType, request.ImageUrl);

            // Step 1: Download and process the image
            var (imageBytes, maskBytes) = await PrepareImageAndMask(request.ImageUrl);
            _logger.LogInformation("Image processed - Original size: {Size} bytes", imageBytes.Length);
            
            // Step 2: Generate transformation prompt
            var prompt = GenerateTransformationPrompt(request.EnhancementType ?? "professional");
            _logger.LogInformation("Using transformation prompt: {Prompt}", prompt);
            
            // Step 3: Create multipart form data for image editing
            using var formData = new MultipartFormDataContent();
            
            // Add the image file (OpenAI edits expect field name image[])
            var imageContent = new ByteArrayContent(imageBytes);
            imageContent.Headers.ContentType = new MediaTypeHeaderValue("image/png");
            formData.Add(imageContent, "image[]", "image.png");
            
            // Add the mask file (optional). Some models/endpoints behave better without an explicit mask.
            // Skipping mask to allow full-image edits reliably.
            // formData.Add(new ByteArrayContent(maskBytes), "mask", "mask.png");
            
            // Specify model explicitly (required by OpenAI)
            formData.Add(new StringContent("gpt-image-1"), "model");

            // Add the prompt
            formData.Add(new StringContent(prompt), "prompt");
            
            // Add other parameters (keep minimal to avoid unknown-parameter errors)
            // Note: Some OpenAI deployments reject response_format on edits; omit it and accept url or b64_json in response
            formData.Add(new StringContent("1024x1024"), "size");
            
            // Step 4: Call OpenAI image edit endpoint
            _logger.LogInformation(
                "Posting to OpenAI images/edits: model=gpt-image-1, promptLen={PromptLen}, imageBytes={ImageBytes}",
                prompt?.Length ?? 0, imageBytes?.Length ?? 0);
            var response = await _openAiClient.PostAsync("images/edits", formData);
            
            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync();
                _logger.LogError("OpenAI API error {StatusCode}: {Error}", response.StatusCode, errorBody);
                // Surface auth problems distinctly so controller can map to 401
                if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized ||
                    response.StatusCode == System.Net.HttpStatusCode.Forbidden)
                {
                    throw new UnauthorizedAccessException($"OpenAI authentication failed: {(int)response.StatusCode}");
                }
                throw new InvalidOperationException($"OpenAI image transformation failed: {response.StatusCode} - {errorBody}");
            }

            var responseJson = await response.Content.ReadAsStringAsync();
            var preview = responseJson.Length > 500 ? responseJson.Substring(0, 500) + "..." : responseJson;
            _logger.LogWarning("OpenAI raw response preview: {Response}", preview);
            
            var openAIResponse = JsonSerializer.Deserialize<OpenAIImageResponse>(responseJson, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            _logger.LogInformation("Deserialized response - Data count: {Count}", openAIResponse?.Data?.Length ?? 0);
            
            var imageData = openAIResponse?.Data?.FirstOrDefault();
            if (imageData == null)
            {
                _logger.LogWarning("OpenAI returned no data. Raw preview: {Preview}", preview);
                throw new InvalidOperationException("OpenAI returned no data");
            }

            string dataUrl;
            if (!string.IsNullOrEmpty(imageData.B64Json))
            {
                // Base64 response
                dataUrl = $"data:image/png;base64,{imageData.B64Json}";
            }
            else if (!string.IsNullOrEmpty(imageData.Url))
            {
                // URL response fallback when response_format is not supported
                dataUrl = imageData.Url;
            }
            else
            {
                _logger.LogWarning("OpenAI returned neither url nor b64_json");
                throw new InvalidOperationException("OpenAI returned neither url nor b64_json");
            }
            
            var processingTime = DateTime.UtcNow - startTime;
            _logger.LogInformation("OpenAI photo transformation completed in {Time}ms, returning base64 data URL", 
                processingTime.TotalMilliseconds);

            return dataUrl;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "OpenAI photo transformation failed: {Message}", ex.Message);
            throw;
        }
    }

    public Task<string> ProcessImageAsync(IFormFile image, string userId, string styleOption)
    {
        throw new NotSupportedException("OpenAI service is for photo enhancement only");
    }

    public Task<IEnumerable<string>> GetAvailableStylesAsync()
    {
        var styles = new[]
        {
            "chibi",
            "pixar_3d", 
            "studio_ghibli",
            "kawaii",
            "shoujo_manga",
            "retro_90s_anime",
            "low_poly",
            "clay_animation",
            "voxel_art"
        };
        return Task.FromResult<IEnumerable<string>>(styles);
    }

    public Task<string> GenerateImageAsync(GenerateImagesRequestDto request)
    {
        throw new NotSupportedException("OpenAI service focuses on photo enhancement");
    }

    /// <summary>
    /// Downloads the image from URL, converts to PNG (square up to 1024), and creates transparent mask
    /// </summary>
    private async Task<(byte[] imageBytes, byte[] maskBytes)> PrepareImageAndMask(string imageUrl)
    {
        try
        {
            _logger.LogInformation("Downloading image from URL: {ImageUrl}", imageUrl);
            // Add debug context for troubleshooting (host/scheme/SAS)
            try
            {
                var u = new Uri(imageUrl);
                var hasSas = !string.IsNullOrEmpty(u.Query) && u.Query.Contains("sig=", StringComparison.OrdinalIgnoreCase);
                _logger.LogDebug("Download client context: host={Host}, scheme={Scheme}, hasSas={HasSas}", u.Host, u.Scheme, hasSas);
            }
            catch { /* ignore parse issues */ }

            // Download the original image via plain client (no Authorization header)
            var imageResponse = await _downloadClient.GetAsync(imageUrl);
            if (!imageResponse.IsSuccessStatusCode)
            {
                var status = (int)imageResponse.StatusCode;
                var reason = imageResponse.ReasonPhrase;
                _logger.LogError("Source image fetch failed: {Status} {Reason}", status, reason);
                throw new HttpRequestException($"Source image fetch failed: {status} {reason}");
            }
            
            var originalImageBytes = await imageResponse.Content.ReadAsByteArrayAsync();
            _logger.LogInformation("Downloaded image - Size: {Size} bytes", originalImageBytes.Length);
            
            // Process image and create mask using ImageSharp (cross-platform)
            using var original = SixLabors.ImageSharp.Image.Load<Rgba32>(originalImageBytes);

            // Determine target square size (up to 1024)
            var targetSize = Math.Min(1024, Math.Max(original.Width, original.Height));

            // Create square canvas with white background
            using var squareImage = new SixLabors.ImageSharp.Image<Rgba32>(targetSize, targetSize, new Rgba32(255, 255, 255, 255));

            // Resize down if needed (do not upscale)
            int drawWidth = original.Width;
            int drawHeight = original.Height;
            if (original.Width > targetSize || original.Height > targetSize)
            {
                var scale = Math.Min((double)targetSize / original.Width, (double)targetSize / original.Height);
                drawWidth = (int)Math.Round(original.Width * scale);
                drawHeight = (int)Math.Round(original.Height * scale);
            }

            using var resized = original.Clone(ctx => ctx.Resize(new ResizeOptions
            {
                Size = new Size(drawWidth, drawHeight),
                Mode = ResizeMode.Stretch
            }));

            // Center the (possibly resized) original image onto the white square canvas
            var offsetX = (targetSize - drawWidth) / 2;
            var offsetY = (targetSize - drawHeight) / 2;
            squareImage.Mutate(ctx => ctx.DrawImage(resized, new Point(offsetX, offsetY), 1f));

            // Encode to PNG bytes
            var pngEncoder = new PngEncoder { ColorType = PngColorType.RgbWithAlpha };
            using var imageStream = new MemoryStream();
            await squareImage.SaveAsync(imageStream, pngEncoder);
            var processedImageBytes = imageStream.ToArray();

            // Create fully transparent mask (edit entire image)
            using var maskImage = new SixLabors.ImageSharp.Image<Rgba32>(targetSize, targetSize, new Rgba32(255, 255, 255, 0));
            using var maskStream = new MemoryStream();
            await maskImage.SaveAsync(maskStream, pngEncoder);
            var maskBytes = maskStream.ToArray();

            _logger.LogInformation("Image processed to {Size}x{Size} PNG - Image: {ImageSize} bytes, Mask: {MaskSize} bytes", 
                targetSize, targetSize, processedImageBytes.Length, maskBytes.Length);
            
            return (processedImageBytes, maskBytes);
        }
        catch (HttpRequestException ex)
        {
            // Surface download errors to controller (maps to 502 instead of 503)
            _logger.LogError(ex, "Failed to prepare image and mask from URL: {ImageUrl}", imageUrl);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to prepare image and mask from URL: {ImageUrl}", imageUrl);
            throw new InvalidOperationException($"Failed to process image: {ex.Message}", ex);
        }
    }

    private static string GenerateTransformationPrompt(string enhancementType)
    {
        var basePrompt = "Transform this portrait into ";
        
        return enhancementType.ToLower() switch
        {
            "chibi" => basePrompt + "japan chibi anime style with oversized head, huge sparkling eyes, tiny body, extremely cute, soft pastel colors, maintaining facial features",
            "studio_ghibli" => basePrompt + "japan Studio Ghibli animation style with soft watercolor painting effect, dreamy atmosphere, whimsical feeling, preserving the person's likeness",
            "kawaii" => basePrompt + "japan kawaii anime style with ultra cute aesthetic, pastel colors, sparkly large eyes, blushing cheeks, keeping facial structure",
            "shoujo_manga" => basePrompt + "shoujo manga art style with dramatic expressive eyes, flowing hair, romantic aesthetic, maintaining person's features",
            "retro_90s_anime" => basePrompt + "90s retro anime style with bold line art, vibrant colors, cel-shaded animation look, preserving facial characteristics",
            "pixar_3d" => basePrompt + "Pixar-quality 3D animation style with professional computer graphics, soft lighting, keeping the person recognizable",
            "low_poly" => basePrompt + "low poly 3D art style with geometric faceted design, angular features, maintaining facial structure",
            "clay_animation" => basePrompt + "clay animation style like stop-motion figure made of modeling clay, preserving the person's likeness",
            "voxel_art" => basePrompt + "voxel art style with Minecraft-inspired blocky 3D design, keeping facial features recognizable",
            _ => basePrompt + "professional anime style with improved lighting, clarity, and artistic quality while maintaining the person's appearance"
        };
    }

    // OpenAI API response models
    private class OpenAIImageResponse
    {
        [JsonPropertyName("data")]
        public OpenAIImageData[]? Data { get; set; }
    }

    private class OpenAIImageData
    {
        [JsonPropertyName("url")]
        public string? Url { get; set; }
        
        [JsonPropertyName("revised_prompt")]
        public string? RevisedPrompt { get; set; }
        
        [JsonPropertyName("b64_json")]
        public string? B64Json { get; set; }
    }
}
