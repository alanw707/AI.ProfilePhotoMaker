using AI.ProfilePhotoMaker.API.Data;
using AI.ProfilePhotoMaker.API.Infrastructure.Logging;
using AI.ProfilePhotoMaker.API.Models;
using AI.ProfilePhotoMaker.API.Models.DTOs;
using AI.ProfilePhotoMaker.API.Services.Storage;
using Microsoft.EntityFrameworkCore;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using System.Security.Cryptography;
using System.Text;

namespace AI.ProfilePhotoMaker.API.Services.ImageProcessing;

public class HeadshotGenerationService : IHeadshotGenerationService
{
    public const string ActionName = "instant_headshot_generation";

    private readonly ApplicationDbContext _dbContext;
    private readonly IBasicTierService _basicTierService;
    private readonly IHeadshotGenerationProvider _provider;
    private readonly IOutcomePackageService? _outcomePackageService;
    private readonly IStorageService _storageService;
    private readonly StoragePathResolver _pathResolver;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<HeadshotGenerationService> _logger;

    private static string S(string? value) => LoggingSanitizer.Sanitize(value);
    private static string Sid(string? value) => LoggingSanitizer.SanitizeId(value);

    public HeadshotGenerationService(
        ApplicationDbContext dbContext,
        IBasicTierService basicTierService,
        IHeadshotGenerationProvider provider,
        IStorageService storageService,
        StoragePathResolver pathResolver,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<HeadshotGenerationService> logger)
        : this(dbContext, basicTierService, provider, storageService, pathResolver, httpClientFactory, configuration, logger, null)
    {
    }

    public HeadshotGenerationService(
        ApplicationDbContext dbContext,
        IBasicTierService basicTierService,
        IHeadshotGenerationProvider provider,
        IStorageService storageService,
        StoragePathResolver pathResolver,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<HeadshotGenerationService> logger,
        IOutcomePackageService? outcomePackageService)
    {
        _dbContext = dbContext;
        _basicTierService = basicTierService;
        _provider = provider;
        _outcomePackageService = outcomePackageService;
        _storageService = storageService;
        _pathResolver = pathResolver;
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<HeadshotGenerationResponseDto> GenerateHeadshotAsync(
        HeadshotGenerationRequestDto request,
        string userId,
        CancellationToken cancellationToken = default)
    {
        var profile = await _dbContext.UserProfiles.FirstOrDefaultAsync(p => p.UserId == userId, cancellationToken);
        if (profile == null)
        {
            throw new InvalidOperationException("User profile not found.");
        }

        var sourcePath = ValidateAndNormalizeSourcePath(request.ImageStoragePath, userId);
        var portraitStyle = await ResolvePortraitStyleAsync(request.Style, cancellationToken);
        request.Style = portraitStyle.Name;
        var packageCode = NormalizePackageCode(request.PackageCode);
        var requestedOutputs = Math.Clamp(request.NumOutputs, 1, 9);
        requestedOutputs = packageCode switch
        {
            "free_preview" => 1,
            "starter_package" => Math.Min(requestedOutputs, 3),
            "pro_package" => Math.Min(requestedOutputs, 9),
            _ => 1
        };
        if (_outcomePackageService != null)
        {
            var hasPackageAllowance = packageCode == "free_preview"
                ? requestedOutputs == 1
                : await _outcomePackageService.GetActiveEntitlementAsync(userId, packageCode, cancellationToken) is { RemainingPackageUses: > 0 } entitlement && entitlement.RemainingCandidates >= requestedOutputs;
            if (!hasPackageAllowance)
            {
                throw new HeadshotGenerationException("PackageEntitlementRequired", "Choose or unlock a profile photo package before generating these candidates.");
            }
        }

        var requiredCredits = packageCode == "free_preview" && _outcomePackageService != null ? 0 : CreditCostConfig.GetCreditCost(ActionName) * requestedOutputs;
        var correlationId = BuildDeterministicCorrelationId(userId, sourcePath, request);
        var existingImages = await _dbContext.ProcessedImages
            .Where(i => i.UserProfileId == profile.Id && i.CorrelationId == correlationId && i.GenerationStatus == "succeeded")
            .OrderBy(i => i.CreatedAt)
            .Take(requestedOutputs)
            .ToListAsync(cancellationToken);
        if (existingImages.Count >= requestedOutputs)
        {
            var existingRemainingCredits = await _basicTierService.GetAvailableCreditsAsync(userId);
            var existingCandidates = existingImages.Select(ToCandidateDto).ToList();
            var primary = existingCandidates[0];
            _logger.LogInformation(
                "Returning idempotent instant headshot candidates for user {UserId}, count={Count}, correlation={CorrelationId}",
                Sid(userId), existingCandidates.Count, S(correlationId));

            return new HeadshotGenerationResponseDto
            {
                Success = true,
                ImageUrl = primary.ImageUrl,
                StoragePath = primary.StoragePath,
                ProcessedImageId = primary.ProcessedImageId,
                Provider = primary.Provider,
                Model = primary.Model,
                Style = NormalizeStyle(request.Style),
                Background = NormalizeBackground(request.Background),
                CreditsCost = 0,
                RemainingCredits = existingRemainingCredits,
                CorrelationId = correlationId,
                Candidates = existingCandidates
            };
        }

        var availableCredits = await _basicTierService.GetAvailableCreditsAsync(userId);
        if (availableCredits < requiredCredits)
        {
            throw new HeadshotGenerationException("InsufficientCredits", $"Instant headshot generation requires {requiredCredits} credit{(requiredCredits == 1 ? string.Empty : "s")}.");
        }

        CreditConsumptionResult? consumed = null;

        try
        {
            if (requiredCredits > 0)
            {
                consumed = await _basicTierService.ConsumeCreditsAsync(
                    userId,
                    requiredCredits,
                    ActionName,
                    correlationId,
                    cancellationToken);

                if (consumed == null || !consumed.Success)
                {
                    var code = consumed?.Error == "insufficient_credits" ? "InsufficientCredits" : "CreditConsumptionFailed";
                    throw new HeadshotGenerationException(code, "Unable to charge credits for instant headshot generation.");
                }
            }

            _logger.LogInformation(
                "Instant headshot generation started for user {UserId}, provider={Provider}, model={Model}, correlation={CorrelationId}",
                Sid(userId), S(_provider.ProviderName), S(_provider.ModelName), S(correlationId));

            var candidates = new List<HeadshotCandidateDto>();

            for (var outputIndex = 0; outputIndex < requestedOutputs; outputIndex++)
            {
                var candidateCorrelationId = requestedOutputs == 1
                    ? correlationId
                    : $"{correlationId}:candidate:{outputIndex + 1}";
                var result = await _provider.GenerateAsync(new HeadshotGenerationRequest
                {
                    UserId = userId,
                    ImageStoragePath = sourcePath,
                    Style = portraitStyle.Name,
                    Background = request.Background,
                    PromptTemplate = BuildInstantHeadshotPrompt(portraitStyle.PromptTemplate, profile),
                    CorrelationId = candidateCorrelationId
                }, cancellationToken);

                if (!result.Success)
                {
                    throw new HeadshotGenerationException(
                        result.FailureCode ?? "ProviderGenerationFailed",
                        result.FailureMessage ?? "Headshot provider failed to generate an image.");
                }

                var storedPath = await StoreProviderOutputAsync(result.DataUrlOrUrl, userId, packageCode == "free_preview" && _outcomePackageService != null, cancellationToken);

                var processedImage = new ProcessedImage
                {
                    OriginalImageUrl = sourcePath,
                    ProcessedImageUrl = storedPath,
                    Style = NormalizeStyle(request.Style),
                    UserProfileId = profile.Id,
                    CreatedAt = DateTime.UtcNow,
                    IsGenerated = true,
                    IsOriginalUpload = false,
                    Provider = result.Provider,
                    ProviderModel = result.Model,
                    GenerationMode = "instant_headshot",
                    PromptVersion = result.PromptVersion,
                    CreditCost = CreditCostConfig.GetCreditCost(ActionName),
                    GenerationStatus = "succeeded",
                    CorrelationId = correlationId
                };
                processedImage.SetScheduledDeletionDate();

                _dbContext.ProcessedImages.Add(processedImage);
                await _dbContext.SaveChangesAsync(cancellationToken);
                candidates.Add(ToCandidateDto(processedImage));
            }

            if (packageCode != "free_preview" &&
                _outcomePackageService != null &&
                !await _outcomePackageService.ConsumeCandidatesAsync(userId, packageCode, requestedOutputs, cancellationToken))
            {
                throw new HeadshotGenerationException("PackageEntitlementRequired", "Unable to consume profile photo package allowance.");
            }

            var remainingCredits = await _basicTierService.GetAvailableCreditsAsync(userId);
            await _basicTierService.LogUsageAsync(
                userId,
                "instant_headshot_generation_succeeded",
                $"provider={candidates[0].Provider}; model={candidates[0].Model}; candidateCount={candidates.Count}; correlationId={correlationId}",
                creditsCost: 0,
                creditsRemaining: remainingCredits);

            _logger.LogInformation(
                "Instant headshot generation succeeded for user {UserId}, candidates={Count}, correlation={CorrelationId}",
                Sid(userId), candidates.Count, S(correlationId));

            var primary = candidates[0];
            return new HeadshotGenerationResponseDto
            {
                Success = true,
                ImageUrl = primary.ImageUrl,
                StoragePath = primary.StoragePath,
                ProcessedImageId = primary.ProcessedImageId,
                Provider = primary.Provider,
                Model = primary.Model,
                Style = NormalizeStyle(request.Style),
                Background = NormalizeBackground(request.Background),
                CreditsCost = requiredCredits,
                RemainingCredits = remainingCredits,
                CorrelationId = correlationId,
                Candidates = candidates
            };
        }
        catch
        {
            await _basicTierService.RefundCreditsAsync(userId, consumed);
            throw;
        }
    }

    private HeadshotCandidateDto ToCandidateDto(ProcessedImage image)
    {
        return new HeadshotCandidateDto
        {
            ImageUrl = _storageService.GetImageUrl(image.ProcessedImageUrl),
            StoragePath = image.ProcessedImageUrl,
            ProcessedImageId = image.Id,
            Provider = image.Provider ?? _provider.ProviderName,
            Model = image.ProviderModel ?? _provider.ModelName,
            CorrelationId = image.CorrelationId ?? string.Empty
        };
    }

    private static string BuildDeterministicCorrelationId(string userId, string sourcePath, HeadshotGenerationRequestDto request)
    {
        var clientRequestId = string.IsNullOrWhiteSpace(request.ClientRequestId)
            ? "legacy"
            : request.ClientRequestId.Trim();
        var normalized = string.Join('|',
            userId.Trim(),
            sourcePath.Trim(),
            NormalizeStyle(request.Style),
            NormalizeBackground(request.Background),
            NormalizePackageCode(request.PackageCode),
            request.NumOutputs.ToString(System.Globalization.CultureInfo.InvariantCulture),
            clientRequestId);
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(normalized))).ToLowerInvariant();
        return $"{ActionName}:{hash[..32]}";
    }

    private string ValidateAndNormalizeSourcePath(string storagePath, string userId)
    {
        if (string.IsNullOrWhiteSpace(storagePath))
        {
            throw new HeadshotGenerationException("InvalidImageSource", "Image storage path is required.");
        }

        var trimmed = storagePath.Trim();
        string decoded;
        try
        {
            decoded = Uri.UnescapeDataString(trimmed);
        }
        catch
        {
            throw new HeadshotGenerationException("InvalidImageSource", "Invalid image source.");
        }

        if (!string.Equals(trimmed, decoded, StringComparison.Ordinal) ||
            Uri.TryCreate(trimmed, UriKind.Absolute, out _) ||
            trimmed.Contains('\\') ||
            trimmed.Split('/', StringSplitOptions.RemoveEmptyEntries).Any(p => p is "." or ".."))
        {
            throw new HeadshotGenerationException("InvalidImageSource", "Invalid image source.");
        }

        var allowedPrefixes = new[]
        {
            _pathResolver.GetDirectoryPrefix(StorageType.Upload, userId),
            _pathResolver.GetDirectoryPrefix(StorageType.Enhanced, userId)
        };

        if (!allowedPrefixes.Any(prefix => trimmed.StartsWith(prefix, StringComparison.Ordinal) && trimmed.Length > prefix.Length))
        {
            _logger.LogWarning("Rejected headshot source path for user {UserId}. Path={Path}", Sid(userId), S(trimmed));
            throw new HeadshotGenerationException("InvalidImageSource", "Invalid image source.");
        }

        return trimmed;
    }

    private async Task<string> StoreProviderOutputAsync(string output, string userId, bool freePreview, CancellationToken cancellationToken)
    {
        var bytes = await ReadOutputBytesAsync(output, cancellationToken);
        if (freePreview)
        {
            bytes = await CreateFreePreviewAsync(bytes, cancellationToken);
        }

        var fileName = $"headshot-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid():N}.png";
        await using var stream = new MemoryStream(bytes);
        return await _storageService.SaveImageAsync(stream, fileName, userId, "generated");
    }

    private static async Task<byte[]> CreateFreePreviewAsync(byte[] bytes, CancellationToken cancellationToken)
    {
        using var image = Image.Load<Rgba32>(bytes);
        image.Mutate(ctx => ctx.Resize(new ResizeOptions
        {
            Mode = ResizeMode.Max,
            Size = new Size(512, 512)
        }));

        image.ProcessPixelRows(accessor =>
        {
            for (var y = 0; y < accessor.Height; y++)
            {
                var row = accessor.GetRowSpan(y);
                for (var x = 0; x < row.Length; x++)
                {
                    var stripe = ((x + y) / 28) % 7 == 0;
                    var lowerBand = y > accessor.Height * 3 / 4;
                    if (!stripe && !lowerBand) continue;

                    ref var pixel = ref row[x];
                    pixel.R = (byte)(pixel.R * 0.72);
                    pixel.G = (byte)(pixel.G * 0.72);
                    pixel.B = (byte)(pixel.B * 0.72);
                }
            }
        });

        await using var output = new MemoryStream();
        await image.SaveAsync(output, new PngEncoder(), cancellationToken);
        return output.ToArray();
    }

    private async Task<byte[]> ReadOutputBytesAsync(string output, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(output))
        {
            throw new HeadshotGenerationException("ProviderEmptyOutput", "Headshot provider returned no image.");
        }

        const string dataPrefix = "data:image/png;base64,";
        if (output.StartsWith(dataPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return Convert.FromBase64String(output[dataPrefix.Length..]);
        }

        if (Uri.TryCreate(output, UriKind.Absolute, out var uri))
        {
            var client = _httpClientFactory.CreateClient();
            using var response = await client.GetAsync(uri, cancellationToken);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsByteArrayAsync(cancellationToken);
        }

        throw new HeadshotGenerationException("ProviderInvalidOutput", "Headshot provider returned an unsupported image format.");
    }

    private static string BuildInstantHeadshotPrompt(string promptTemplate, UserProfile profile)
    {
        var gender = string.IsNullOrWhiteSpace(profile.Gender) ? "person" : profile.Gender.Trim().ToLowerInvariant();
        var ethnicity = string.IsNullOrWhiteSpace(profile.Ethnicity) ? string.Empty : profile.Ethnicity.Trim().ToLowerInvariant();
        var genderEthnicityCombo = string.IsNullOrWhiteSpace(ethnicity) ? gender : $"{gender} {ethnicity}";

        return promptTemplate
            .Replace("{subject}", "professional person")
            .Replace("{gender} {ethnicity}", genderEthnicityCombo)
            .Replace("{gender}", gender)
            .Replace("{ethnicity}", ethnicity)
            .Replace("  ", " ")
            .Trim();
    }

    private async Task<Style> ResolvePortraitStyleAsync(string? requestedStyle, CancellationToken cancellationToken)
    {
        var normalizedStyle = NormalizeStyle(requestedStyle);
        var style = await _dbContext.Styles
            .Where(s => s.IsActive && s.Name.ToLower() == normalizedStyle)
            .FirstOrDefaultAsync(cancellationToken);

        if (style != null)
        {
            return style;
        }

        throw new HeadshotGenerationException("StyleUnavailable", "That portrait style is no longer available. Choose another style.");
    }

    private static string NormalizePackageCode(string? packageCode)
    {
        var normalized = (packageCode ?? "free_preview").Trim().ToLowerInvariant();
        return normalized is "free_preview" or "starter_package" or "pro_package" ? normalized : "free_preview";
    }

    private static string NormalizeStyle(string? style)
    {
        if (string.IsNullOrWhiteSpace(style))
        {
            return "linkedin";
        }

        var normalized = style.Trim().ToLowerInvariant();
        return normalized == "professional" ? "linkedin" : normalized;
    }

    private static string NormalizeBackground(string? background) =>
        string.IsNullOrWhiteSpace(background) ? "auto" : background.Trim().ToLowerInvariant();
}

public class HeadshotGenerationException : Exception
{
    public HeadshotGenerationException(string code, string message) : base(message)
    {
        Code = code;
    }

    public string Code { get; }
}
