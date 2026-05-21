using AI.ProfilePhotoMaker.API.Models.DTOs;

namespace AI.ProfilePhotoMaker.API.Services.ImageProcessing;

public class OpenAIHeadshotGenerationProvider : IHeadshotGenerationProvider
{
    private const string PromptVersion = "openai-headshot-v1";
    private readonly OpenAIImageGenerationService _openAiImageGenerationService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<OpenAIHeadshotGenerationProvider> _logger;

    public OpenAIHeadshotGenerationProvider(
        OpenAIImageGenerationService openAiImageGenerationService,
        IConfiguration configuration,
        ILogger<OpenAIHeadshotGenerationProvider> logger)
    {
        _openAiImageGenerationService = openAiImageGenerationService;
        _configuration = configuration;
        _logger = logger;
    }

    public string ProviderName => "openai";

    public string ModelName => _configuration["OpenAI:ImageModel"] ?? "gpt-image-2";

    public async Task<HeadshotGenerationResult> GenerateAsync(HeadshotGenerationRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            var enhancementRequest = new EnhancePhotoRequestDto
            {
                ImageStoragePath = request.ImageStoragePath,
                EnhancementType = BuildEnhancementType(request.Style, request.Background),
                CustomPrompt = request.PromptTemplate
            };

            var output = await _openAiImageGenerationService.EnhancePhotoQualityAsync(enhancementRequest);

            return new HeadshotGenerationResult
            {
                Success = true,
                DataUrlOrUrl = output,
                Provider = ProviderName,
                Model = ModelName,
                PromptVersion = PromptVersion
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "OpenAI headshot generation failed for correlation {CorrelationId}", request.CorrelationId);
            return new HeadshotGenerationResult
            {
                Success = false,
                Provider = ProviderName,
                Model = ModelName,
                PromptVersion = PromptVersion,
                FailureCode = ex switch
                {
                    UnauthorizedAccessException => "ProviderAuthenticationFailed",
                    TaskCanceledException => "ProviderTimeout",
                    HttpRequestException => "ProviderNetworkError",
                    _ => "ProviderGenerationFailed"
                },
                FailureMessage = ex.Message
            };
        }
    }

    private static string BuildEnhancementType(string style, string background)
    {
        var normalizedStyle = string.IsNullOrWhiteSpace(style) ? "professional" : style.Trim().ToLowerInvariant();
        var normalizedBackground = string.IsNullOrWhiteSpace(background) ? "auto" : background.Trim().ToLowerInvariant();

        return normalizedStyle switch
        {
            "linkedin" => "headshot_linkedin",
            "creator" => "headshot_creator",
            "professional" when normalizedBackground is "office" => "headshot_office",
            "professional" when normalizedBackground is "studio" => "headshot_studio",
            _ => "headshot"
        };
    }
}
