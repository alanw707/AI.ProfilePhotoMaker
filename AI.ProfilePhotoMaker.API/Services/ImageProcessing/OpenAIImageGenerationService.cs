using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using AI.ProfilePhotoMaker.API.Infrastructure.Logging;
using AI.ProfilePhotoMaker.API.Models.DTOs;
using AI.ProfilePhotoMaker.API.Services.Storage;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Formats.Png;

namespace AI.ProfilePhotoMaker.API.Services.ImageProcessing;

/// <summary>
/// OpenAI GPT Image implementation for creative image enhancement using HTTP client
/// </summary>
public class OpenAIImageGenerationService : IImageProcessingService
{
    private readonly HttpClient _openAiClient;
    private readonly HttpClient _downloadClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<OpenAIImageGenerationService> _logger;
    private readonly IStorageService _storageService;

    private static string S(string? value) => LoggingSanitizer.Sanitize(value);
    private static string Sid(string? value) => LoggingSanitizer.SanitizeId(value);

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

        // Configure HTTP client for OpenAI API. Keep base URL configurable for Azure/private gateways/tests.
        var baseUrl = _configuration["OpenAI:BaseUrl"] ?? "https://api.openai.com/v1/";
        _openAiClient.BaseAddress = new Uri(baseUrl.EndsWith('/') ? baseUrl : $"{baseUrl}/");
        _openAiClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        // Load API key from canonical configuration path. Environment variables should use OpenAI__ApiKey.
        var apiKey = _configuration["OpenAI:ApiKey"];
        if (string.IsNullOrEmpty(apiKey))
        {
            _logger.LogError("OpenAI API key not configured - OpenAI service cannot be initialized");
            throw new InvalidOperationException("OpenAI API key is required but not configured. Please set OpenAI:ApiKey in configuration or OpenAI__ApiKey as an environment variable.");
        }

        _openAiClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        _logger.LogInformation("OpenAI API configured successfully");
    }

    // Note: tests should supply an IHttpClientFactory that returns the same mocked HttpClient
    // to capture both the OpenAI POST and the source image GET. Keeping a single DI constructor
    // avoids ambiguity and simplifies runtime behavior.

    /// <summary>
    /// Enhances photo quality using OpenAI GPT Image transformation and returns base64 data URL format
    /// </summary>
    /// <param name="request">Enhancement request with image URL and style preferences</param>
    /// <returns>Base64 data URL in format: data:image/png;base64,{base64data}</returns>
    public async Task<string> EnhancePhotoQualityAsync(EnhancePhotoRequestDto request)
    {
        var startTime = DateTime.UtcNow;

        try
        {
            _logger.LogInformation("Starting OpenAI photo transformation type={Type}, imageUrl={ImageUrl}",
                S(request.EnhancementType),
                S(request.ImageUrl));

            // Step 1: Load and process the image. New enhancement uploads pass a storage path
            // so local/container runs do not need to re-download the source through Azurite URLs.
            (byte[] imageBytes, byte[] maskBytes) imageAndMask;
            if (!string.IsNullOrWhiteSpace(request.ImageStoragePath))
            {
                imageAndMask = await PrepareImageAndMaskFromStorageAsync(request.ImageStoragePath);
            }
            else
            {
                // Legacy fallback for older clients that only send a URL.
                var normalizedUrl = await NormalizeImageUrlForServerAccessAsync(request.ImageUrl ?? string.Empty);
                imageAndMask = await PrepareImageAndMaskFromUrlAsync(normalizedUrl);
            }

            var (imageBytes, _) = imageAndMask;
            _logger.LogInformation("Image processed - Original size: {Size} bytes", imageBytes.Length);

            var prompt = !string.IsNullOrWhiteSpace(request.CustomPrompt)
                ? BuildCustomTransformationPrompt(request.CustomPrompt)
                : GenerateTransformationPrompt(request.EnhancementType ?? "professional");
            _logger.LogInformation("Using transformation prompt: {Prompt}", S(prompt));

            using var formData = new MultipartFormDataContent();

            var imageContent = new ByteArrayContent(imageBytes);
            imageContent.Headers.ContentType = new MediaTypeHeaderValue("image/png");
            formData.Add(imageContent, "image", "image.png");

            var imageModel = _configuration["OpenAI:ImageModel"] ?? "gpt-image-2";
            formData.Add(new StringContent(imageModel), "model");
            formData.Add(new StringContent(prompt), "prompt");
            formData.Add(new StringContent("1024x1024"), "size");

            _logger.LogInformation(
                "Posting to OpenAI images/edits: model={Model}, promptLen={PromptLen}, imageBytes={ImageBytes}",
                S(imageModel), prompt?.Length ?? 0, imageBytes?.Length ?? 0);
            var editEndpoint = _configuration["OpenAI:ImageEditEndpoint"] ?? "images/edits";
            var response = await _openAiClient.PostAsync(editEndpoint, formData);

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync();
                _logger.LogError("OpenAI API error {StatusCode}: {Error}", response.StatusCode, S(errorBody));
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
            _logger.LogWarning("OpenAI raw response preview: {Response}", S(preview));

            var openAIResponse = JsonSerializer.Deserialize<OpenAIImageResponse>(responseJson, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            _logger.LogInformation("Deserialized response - Data count: {Count}", openAIResponse?.Data?.Length ?? 0);

            var imageData = openAIResponse?.Data?.FirstOrDefault();
            if (imageData == null)
            {
                _logger.LogWarning("OpenAI returned no data. Raw preview: {Preview}", S(preview));
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
            _logger.LogError(ex, "OpenAI photo transformation failed: {Message}", S(ex.Message));
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
            "background",
            "social",
            "cartoon",
            "professional",
            "relighting",
            "professional_polish",
            "outfit_upgrade",
            "background_upgrade",
            "chibi",
            "pixar_3d",
            "studio_ghibli",
            "kawaii",
            "shoujo_manga",
            "retro_90s_anime",
            "low_poly",
            "clay_animation",
            "voxel_art",
            "headshot",
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
    private async Task<(byte[] imageBytes, byte[] maskBytes)> PrepareImageAndMaskFromStorageAsync(string storagePath)
    {
        try
        {
            _logger.LogInformation("Loading enhancement source image from storage path: {StoragePath}", S(storagePath));
            await using var imageStream = await _storageService.GetImageAsync(storagePath);
            if (imageStream == null)
            {
                throw new FileNotFoundException("Enhancement source image was not found in storage.", storagePath);
            }

            using var memoryStream = new MemoryStream();
            await imageStream.CopyToAsync(memoryStream);
            var originalImageBytes = memoryStream.ToArray();
            _logger.LogInformation("Loaded storage image - Size: {Size} bytes", originalImageBytes.Length);

            return await PrepareImageAndMaskFromBytesAsync(originalImageBytes, "storage");
        }
        catch (Exception ex) when (ex is not FileNotFoundException)
        {
            _logger.LogError(ex, "Failed to prepare image and mask from storage path: {StoragePath}", S(storagePath));
            throw new InvalidOperationException($"Failed to process image from storage: {ex.Message}", ex);
        }
    }

    private async Task<(byte[] imageBytes, byte[] maskBytes)> PrepareImageAndMaskFromUrlAsync(string imageUrl)
    {
        try
        {
            _logger.LogInformation("Downloading image from URL: {ImageUrl}", S(imageUrl));
            // Add debug context for troubleshooting (host/scheme/SAS)
            try
            {
                var u = new Uri(imageUrl);
                var hasSas = !string.IsNullOrEmpty(u.Query) && u.Query.Contains("sig=", StringComparison.OrdinalIgnoreCase);
                _logger.LogDebug(
                    "Download client context: host={Host}, scheme={Scheme}, hasSas={HasSas}",
                    S(u.Host),
                    S(u.Scheme),
                    hasSas);
            }
            catch { /* ignore parse issues */ }

            // Download the original image via plain client (no Authorization header)
            var imageResponse = await _downloadClient.GetAsync(imageUrl);
            if (!imageResponse.IsSuccessStatusCode)
            {
                var status = (int)imageResponse.StatusCode;
                var reason = imageResponse.ReasonPhrase;
                _logger.LogError("Source image fetch failed: {Status} {Reason}", status, S(reason));
                throw new HttpRequestException($"Source image fetch failed: {status} {reason}");
            }

            var originalImageBytes = await imageResponse.Content.ReadAsByteArrayAsync();
            _logger.LogInformation("Downloaded image - Size: {Size} bytes", originalImageBytes.Length);

            return await PrepareImageAndMaskFromBytesAsync(originalImageBytes, "url");
        }
        catch (HttpRequestException ex)
        {
            // Surface download errors to controller (maps to 502 instead of 503)
            _logger.LogError(ex, "Failed to prepare image and mask from URL: {ImageUrl}", S(imageUrl));
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to prepare image and mask from URL: {ImageUrl}", S(imageUrl));
            throw new InvalidOperationException($"Failed to process image: {ex.Message}", ex);
        }
    }

    private async Task<(byte[] imageBytes, byte[] maskBytes)> PrepareImageAndMaskFromBytesAsync(byte[] originalImageBytes, string source)
    {
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

        _logger.LogInformation("Image processed from {Source} to {Size}x{Size} PNG - Image: {ImageSize} bytes, Mask: {MaskSize} bytes",
            S(source), targetSize, targetSize, processedImageBytes.Length, maskBytes.Length);

        return (processedImageBytes, maskBytes);
    }

    /// <summary>
    /// Ensures the provided image URL is accessible from the API server.
    /// - For Azure Blob without SAS: generate a short-lived SAS.
    /// - For proxied paths like /profile-images/*: convert to SAS URL using default container.
    /// - Leaves other URLs unchanged.
    /// </summary>
    private async Task<string> NormalizeImageUrlForServerAccessAsync(string originalUrl)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(originalUrl)) return originalUrl;

            if (!Uri.TryCreate(originalUrl, UriKind.Absolute, out var uri))
            {
                return originalUrl;
            }

            // Azure Blob without SAS -> generate SAS
            var isAzureBlob = uri.Host.Contains(".blob.core.windows.net", StringComparison.OrdinalIgnoreCase);
            var hasSas = !string.IsNullOrEmpty(uri.Query) && uri.Query.Contains("sig=", StringComparison.OrdinalIgnoreCase);
            if (isAzureBlob && !hasSas)
            {
                var path = uri.AbsolutePath.Trim('/');
                var segments = path.Split('/', 2, StringSplitOptions.RemoveEmptyEntries);
                if (segments.Length >= 2)
                {
                    var container = segments[0];
                    var blobPath = segments[1];
                    var containerAndPath = $"{container}/{blobPath}";
                    try
                    {
                        var sasUrl = await _storageService.GenerateSasUrlAsync(containerAndPath, TimeSpan.FromMinutes(10));
                        if (!string.IsNullOrEmpty(sasUrl))
                        {
                            _logger.LogDebug(
                                "Generated SAS URL for Azure image (container={Container}, path={Path})",
                                S(container),
                                S(blobPath));
                            return sasUrl;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(
                            ex,
                            "Failed to generate SAS for Azure image (container={Container}, path={Path})",
                            S(container),
                            S(blobPath));
                    }
                }
            }

            // API proxy path /profile-images/* -> translate to SAS using default container
            if (uri.AbsolutePath.StartsWith("/profile-images/", StringComparison.OrdinalIgnoreCase))
            {
                var suffix = uri.AbsolutePath.Substring("/profile-images/".Length).TrimStart('/');
                var containerAndPath = $"profile-images/{suffix}"; // default container for this route
                try
                {
                    var sasUrl = await _storageService.GenerateSasUrlAsync(containerAndPath, TimeSpan.FromMinutes(10));
                    if (!string.IsNullOrEmpty(sasUrl))
                    {
                        _logger.LogDebug("Translated proxy path to SAS URL (path={Path})", S(suffix));
                        return sasUrl;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to translate proxy image path to SAS (path={Path})", S(suffix));
                }
            }

            return originalUrl;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to normalize image URL: {Url}", S(originalUrl));
            return originalUrl;
        }
    }

    private static string BuildCustomTransformationPrompt(string customPrompt)
    {
        var preserveIdentity =
            "Preserve the person's identity, age, facial structure, skin tone, expression, hairstyle, and recognizable features. ";
        return preserveIdentity + customPrompt.Trim();
    }

    private static string GenerateTransformationPrompt(string enhancementType)
    {
        var preserveIdentity =
            "Preserve the person's identity, age, facial structure, skin tone, expression, hairstyle, and clothing unless specifically requested. ";
        var basePrompt = preserveIdentity + "Transform this portrait into ";

        return enhancementType.ToLower() switch
        {
            "background" => preserveIdentity + "Create a polished professional headshot. Replace distracting or messy backgrounds with a clean neutral studio backdrop, improve lighting and contrast naturally, keep realistic skin texture, and avoid changing facial features.",
            "social" => preserveIdentity + "Create a bright, engaging social media profile photo. Improve lighting, sharpness, color balance, and background cleanliness while keeping a natural realistic look and avoiding over-smoothed skin.",
            "cartoon" => basePrompt + "a high-quality friendly cartoon portrait with clean lines, expressive but recognizable facial features, natural proportions, polished lighting, and a simple uncluttered background",
            "professional" => preserveIdentity + "Create a realistic professional profile photo with clean lighting, subtle background cleanup, natural color correction, crisp focus, and realistic skin texture.",
            "headshot" => preserveIdentity + "Create a natural professional headshot suitable for LinkedIn and work profiles. Use studio-quality lighting, a clean neutral background, realistic skin texture, subtle retouching, natural color correction, and crisp focus. Avoid waxy smoothing, plastic skin, exaggerated facial changes, beauty-filter effects, and over-retouching.",
            "relighting" => preserveIdentity + "Relight this profile photo with professional studio-style lighting. Soften harsh shadows, balance exposure, keep skin texture realistic, and do not change the person's identity, facial structure, clothing, or background content beyond natural lighting improvements.",
            "professional_polish" => preserveIdentity + "Apply subtle professional photo polish. Reduce shine and minor distractions, improve clarity and color balance, lightly soften under-eye shadows, preserve age and natural skin texture, and avoid beauty-filter or plastic-skin effects.",
            "outfit_upgrade" => preserveIdentity + "Upgrade visible clothing to neutral role-appropriate professional attire such as a blazer, collared shirt, or polished business-casual top. Preserve body shape, face, age, and identity. Avoid credential-specific uniforms, sexualized clothing, logos, luxury/status deception, or drastic body changes.",
            "background_upgrade" => preserveIdentity + "Replace or improve the background with a clean professional setting such as a neutral studio backdrop, tasteful office, or warm uncluttered interior. Preserve the person exactly and keep lighting natural.",
            "headshot_linkedin" => preserveIdentity + "Create a realistic LinkedIn-ready professional headshot with head-and-shoulders framing, clean neutral background, confident approachable expression, crisp focus, natural color correction, and subtle realistic retouching. Avoid changing facial features or creating plastic skin.",
            "headshot_creator" => preserveIdentity + "Create a polished creator/founder profile headshot with warm natural lighting, clean modern background, approachable expression, realistic skin texture, and professional social-profile framing. Avoid exaggerated edits, beauty-filter effects, and identity drift.",
            "headshot_office" => preserveIdentity + "Create a realistic professional headshot with tasteful office-style background, balanced lighting, head-and-shoulders framing, crisp focus, and subtle retouching. Keep the person recognizable and avoid over-smoothing.",
            "headshot_studio" => preserveIdentity + "Create a studio-quality professional headshot with a clean studio backdrop, flattering but natural lighting, head-and-shoulders framing, realistic skin texture, and conservative retouching. Do not alter identity-defining facial features.",
            "chibi" => basePrompt + "japan chibi anime style with oversized head, huge sparkling eyes, tiny body, extremely cute, soft pastel colors, maintaining facial features",
            "studio_ghibli" => basePrompt + "japan Studio Ghibli animation style with soft watercolor painting effect, dreamy atmosphere, whimsical feeling, preserving the person's likeness",
            "kawaii" => basePrompt + "japan kawaii anime style with ultra cute aesthetic, pastel colors, sparkly large eyes, blushing cheeks, keeping facial structure",
            "shoujo_manga" => basePrompt + "shoujo manga art style with dramatic expressive eyes, flowing hair, romantic aesthetic, maintaining person's features",
            "retro_90s_anime" => basePrompt + "90s retro anime style with bold line art, vibrant colors, cel-shaded animation look, preserving facial characteristics",
            "pixar_3d" => basePrompt + "Pixar-quality 3D animation style with professional computer graphics, soft lighting, keeping the person recognizable",
            "low_poly" => basePrompt + "low poly 3D art style with geometric faceted design, angular features, maintaining facial structure",
            "clay_animation" => basePrompt + "clay animation style like stop-motion figure made of modeling clay, preserving the person's likeness",
            "voxel_art" => basePrompt + "voxel art style with Minecraft-inspired blocky 3D design, keeping facial features recognizable",
            _ => preserveIdentity + "Create a polished profile photo with improved lighting, clarity, background cleanliness, and natural realistic quality while maintaining the person's appearance."
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
