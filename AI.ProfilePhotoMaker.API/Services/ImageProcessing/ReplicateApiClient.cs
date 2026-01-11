using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using AI.ProfilePhotoMaker.API.Data;
using AI.ProfilePhotoMaker.API.Infrastructure.Logging;
using AI.ProfilePhotoMaker.API.Models;
using AI.ProfilePhotoMaker.API.Models.DTOs;
using AI.ProfilePhotoMaker.API.Models.Replicate;
using AI.ProfilePhotoMaker.API.Services;
using Microsoft.EntityFrameworkCore;

namespace AI.ProfilePhotoMaker.API.Services.ImageProcessing;

/// <summary>
/// Client service for interacting with the Replicate.com API
/// </summary>
public class ReplicateApiClient : IReplicateApiClient
{
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, ReplicatePredictionResult> s_mockPredictions
        = new();
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<IConfiguration, StyleTuningCacheEntry> s_styleTuningCache = new();
    private static readonly string[] s_realismPromptModifiers =
    {
        "natural skin texture",
        "subtle skin pores",
        "soft natural sheen",
        "realistic skin detail",
        "unretouched look",
        "candid lighting"
    };
    private const double DefaultGuidanceScale = 3.0;
    private const int DefaultNumInferenceSteps = 28;
    private const double DefaultProGuidanceScale = 2.8;
    private const int DefaultProNumInferenceSteps = 34;
    private const double DefaultCasualGuidanceScale = 2.3;
    private const int DefaultCasualNumInferenceSteps = 40;
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<ReplicateApiClient> _logger;
    private readonly ApplicationDbContext _context;
    private readonly Services.Storage.IStorageService _storageService;
    private readonly IWebhookUrlResolver _webhookUrlResolver;
    private readonly bool _mockEnabled;
    private readonly bool _authConfigured;

    private static string S(string? value) => LoggingSanitizer.Sanitize(value);
    private static string Sid(string? value) => LoggingSanitizer.SanitizeId(value);

    public ReplicateApiClient(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<ReplicateApiClient> logger,
        ApplicationDbContext context,
        IWebhookUrlResolver webhookUrlResolver,
        Services.Storage.IStorageService? storageService = null)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _logger = logger;
        _context = context;
        _webhookUrlResolver = webhookUrlResolver;
        _storageService = storageService ?? new NoopStorageService();
        _mockEnabled = (Environment.GetEnvironmentVariable("ENABLE_REPLICATE_MOCK") ?? string.Empty)
            .Equals("true", StringComparison.OrdinalIgnoreCase);

        // Configure HTTP client
        _httpClient.BaseAddress = new Uri("https://api.replicate.com/v1/");
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        // Add API token from configuration unless in mock mode
        if (!_mockEnabled)
        {
            // Prefer explicit environment variable if present, then config binding
            var envToken = (Environment.GetEnvironmentVariable("REPLICATE_API_TOKEN") ?? string.Empty).Trim();
            var cfgToken = (_configuration["Replicate:ApiToken"] ?? string.Empty).Trim();
            var apiToken = !string.IsNullOrWhiteSpace(envToken) ? envToken : cfgToken;

            if (string.IsNullOrWhiteSpace(apiToken))
            {
                _authConfigured = false;
                _logger.LogWarning("Replicate API token not configured; Replicate calls will fail until REPLICATE_API_TOKEN or Replicate:ApiToken is set.");
            }
            else
            {
                _authConfigured = true;
                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Token", apiToken);
            }
        }
        else
        {
            _authConfigured = true;
        }
    }

    /// <summary>
    /// Minimal no-op storage service used only in tests that construct ReplicateApiClient directly.
    /// It avoids forcing tests to wire full storage dependencies. Only SAS generation is used by this class,
    /// and here it simply returns the input path unchanged.
    /// </summary>
    private sealed class NoopStorageService : Services.Storage.IStorageService
    {
        public Task<string> SaveImageAsync(Stream imageStream, string fileName, string userId, string folderType = "generated")
            => throw new NotSupportedException();
        public Task<string> SaveImageToPathAsync(Stream imageStream, string storagePath)
            => throw new NotSupportedException();
        public Task<Stream?> GetImageAsync(string storagePath)
            => throw new NotSupportedException();
        public Task<bool> DeleteImageAsync(string storagePath)
            => throw new NotSupportedException();
        public Task<bool> ExistsAsync(string storagePath)
            => throw new NotSupportedException();
        public string GetImageUrl(string storagePath) => storagePath;
        public Task<List<string>> ListUserImagesAsync(string userId)
            => throw new NotSupportedException();
        public Task<Services.Storage.StorageFileInfo?> GetFileInfoAsync(string storagePath)
            => throw new NotSupportedException();
        public Task<string> GenerateSasUrlAsync(string storagePath, TimeSpan expiry, Azure.Storage.Sas.BlobSasPermissions permissions = Azure.Storage.Sas.BlobSasPermissions.Read)
            => Task.FromResult(storagePath);
        public Task<string> SaveZipAsync(Stream zipStream, string storagePath)
            => throw new NotSupportedException();
        public Task<bool> DeleteDirectoryAsync(string directoryPath)
            => throw new NotSupportedException();
        public Task<List<string>> ListFilesAsync(string prefix)
            => throw new NotSupportedException();
    }

    /// <summary>
    /// Creates a new model in Replicate
    /// </summary>
    /// <param name="userId">The user ID</param>
    /// <param name="modelName">The model name</param>
    /// <param name="description">Optional model description</param>
    /// <returns>The created model's full name (owner/model-name)</returns>
    public async Task<string> CreateModelAsync(string userId, string modelName, string? description = null)
    {
        try
        {
            // Mock mode: return a simulated model name without external calls
            if (_mockEnabled)
            {
                var fullModelName = $"mock/{modelName}";
                _logger.LogInformation("[Mock] Returning model name {Model}", fullModelName);
                return fullModelName;
            }
            // Build model creation request.
            // As of recent Replicate API changes, 'owner' and 'hardware' are required.
            // Prefer environment variables so container settings override static appsettings
            var configuredOwner = Environment.GetEnvironmentVariable("REPLICATE_OWNER") ?? _configuration["Replicate:Owner"];
            var configuredHardware = Environment.GetEnvironmentVariable("REPLICATE_HARDWARE") ?? _configuration["Replicate:Hardware"];

            if (string.IsNullOrWhiteSpace(configuredOwner))
            {
                throw new InvalidOperationException("Replicate owner is required. Set 'Replicate:Owner' in appsettings or REPLICATE_OWNER env var.");
            }
            if (string.IsNullOrWhiteSpace(configuredHardware))
            {
                throw new InvalidOperationException("Replicate hardware is required. Set 'Replicate:Hardware' in appsettings or REPLICATE_HARDWARE env var.");
            }

            var modelRequest = new
            {
                owner = configuredOwner,
                name = modelName,
                description = description ?? $"Custom trained model for user {userId}",
                visibility = "private",
                hardware = configuredHardware
            };

            var content = new StringContent(JsonSerializer.Serialize(modelRequest), Encoding.UTF8, "application/json");
            _logger.LogInformation("Creating model for user {UserId}: {ModelName}", Sid(userId), S(modelName));
            var response = await _httpClient.PostAsync("models", content);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError("Replicate model creation failed: {StatusCode} {Error}", response.StatusCode, errorContent);

                switch (response.StatusCode)
                {
                    case HttpStatusCode.Unauthorized:
                        throw new ReplicateApiException(response.StatusCode, "ReplicateAuthFailed",
                            "Replicate API authentication failed. Check your API token.", errorContent);
                    case HttpStatusCode.PaymentRequired:
                        throw new ReplicateApiException(response.StatusCode, "ReplicatePaymentRequired",
                            "Replicate account requires payment.", errorContent);
                    case HttpStatusCode.Forbidden:
                        throw new ReplicateApiException(response.StatusCode, "ReplicatePermissionDenied",
                            "Replicate denied permission to create the model for this token.", errorContent);
                    case HttpStatusCode.Conflict:
                        throw new ReplicateApiException(response.StatusCode, "ReplicateNameConflict",
                            $"Model name already exists: {modelName}", errorContent);
                    case HttpStatusCode.UnprocessableEntity:
                        throw new ReplicateApiException(response.StatusCode, "ReplicateValidationError",
                            "Invalid model creation request.", errorContent);
                    case HttpStatusCode.TooManyRequests:
                        throw new ReplicateApiException(response.StatusCode, "ReplicateRateLimited",
                            "Replicate API rate limit reached.", errorContent);
                    default:
                        throw new ReplicateApiException(response.StatusCode, "ReplicateServiceError",
                            $"Model creation failed with status {(int)response.StatusCode}.", errorContent);
                }
            }

            var responseJson = await response.Content.ReadAsStringAsync();
            _logger.LogInformation("Model creation response: {Response}", responseJson);

            var modelResult = JsonSerializer.Deserialize<JsonElement>(
                responseJson,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            // Extract the full model identifier (owner/name) from the response
            if (modelResult.TryGetProperty("name", out var nameProperty) &&
                modelResult.TryGetProperty("owner", out var ownerProperty))
            {
                var extractedModelName = nameProperty.GetString() ?? throw new Exception("Model name not found in response");
                var owner = ownerProperty.GetString() ?? throw new Exception("Owner not found in response");
                var fullModel = $"{owner}/{extractedModelName}";
                _logger.LogInformation("Model created: {FullModel}", fullModel);
                return fullModel; // Return full model path: owner/name
            }

            // Fallback: try to get the URL and extract the name from it
            if (modelResult.TryGetProperty("url", out var urlProperty))
            {
                var url = urlProperty.GetString();
                if (!string.IsNullOrEmpty(url))
                {
                    // Extract model name from URL like https://api.replicate.com/v1/models/owner/model-name
                    var urlParts = url.Split('/');
                    if (urlParts.Length >= 2)
                    {
                        var owner = urlParts[^2];
                        var name = urlParts[^1];
                        var fullModel = $"{owner}/{name}";
                        _logger.LogInformation("Model created (from URL): {FullModel}", fullModel);
                        return fullModel; // Return full model path: owner/name
                    }
                }
            }

            // Log the full response for debugging
            _logger.LogError("Unable to extract model name from response: {Response}", responseJson);
            throw new Exception("Model name not found in response");
        }
        catch (HttpRequestException ex) when (ex.Message.Contains("401") || ex.Message.Contains("Unauthorized"))
        {
            _logger.LogError(ex, "Replicate API authentication failed for user {UserId}", Sid(userId));
            throw new UnauthorizedAccessException("Replicate API authentication failed. Check your API token.", ex);
        }
        catch (HttpRequestException ex) when (ex.Message.Contains("429") || ex.Message.Contains("rate limit"))
        {
            _logger.LogWarning(ex, "Replicate API rate limit reached for user {UserId}", Sid(userId));
            throw new InvalidOperationException("Replicate API rate limit reached. Please try again later.", ex);
        }
        catch (HttpRequestException ex) when (ex.Message.Contains("402") || ex.Message.Contains("payment"))
        {
            _logger.LogError(ex, "Replicate API payment required for user {UserId}", Sid(userId));
            throw new InvalidOperationException("Replicate API payment required. Please check your billing.", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating model for user {UserId}", Sid(userId));
            throw;
        }
    }

    /// <summary>
    /// Creates a new training for a user's custom model
    /// </summary>
    /// <param name="userId">The user ID</param>
    /// <param name="imageZipUrl">URL to the zipped training images</param>
    /// <returns>The training ID and status</returns>
    public async Task<ReplicateTrainingResult> CreateModelTrainingAsync(string userId, string imageZipUrl, string? modelRequestId = null)
    {
        ModelCreationRequest? modelCreationRequest = null;

        try
        {
            var normalizedZipUrl = await NormalizeImageUrlForExternalAccessAsync(imageZipUrl);
            await PreflightExternalZipUrlAsync(normalizedZipUrl);

            // First, create the model to use as destination
            var modelName = $"user-{userId}-{DateTime.UtcNow:yyyyMMddHHmmss}";
            _logger.LogInformation("Creating model {ModelName} for user {UserId}", S(modelName), Sid(userId));
            var destination = await CreateModelAsync(userId, modelName, $"Custom trained model for user {userId}");

            _logger.LogInformation("Model created successfully: {Destination}", S(destination));
            _logger.LogInformation("Using destination for training: {Destination}", S(destination));

            // Create a model creation request record to track the training
            modelCreationRequest = new ModelCreationRequest
            {
                Id = string.IsNullOrWhiteSpace(modelRequestId) ? Guid.NewGuid().ToString() : modelRequestId,
                UserId = userId,
                ModelName = modelName,
                // Store full model ID in format owner/model-name for consistency with webhooks
                ReplicateModelId = destination,
                Status = ModelCreationStatus.Pending,
                TrainingImageZipUrl = normalizedZipUrl,
                PendingTrainingRequestId = Guid.NewGuid().ToString()
            };

            // Add to database
            _context.ModelCreationRequests.Add(modelCreationRequest);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Created model creation request {RequestId} for user {UserId}",
                modelCreationRequest.Id, Sid(userId));

            var trainingRequest = new
            {
                destination = destination,
                input = new
                {
                    input_images = normalizedZipUrl,
                    trigger_word = $"user_{userId}",
                    lora_type = "subject",
                    training_steps = 2000
                }
            };

            var content = new StringContent(JsonSerializer.Serialize(trainingRequest), Encoding.UTF8, "application/json");
            var modelVersion = _configuration["Replicate:FluxTrainingModelId"];
            if (string.IsNullOrWhiteSpace(modelVersion) || !modelVersion.Contains(':'))
            {
                throw new InvalidOperationException("Replicate:FluxTrainingModelId is not configured with expected 'owner/model:version' format.");
            }
            var versionId = modelVersion.Split(':')[1];
            var endpoint = $"models/replicate/fast-flux-trainer/versions/{versionId}/trainings";

            _logger.LogInformation("Creating training for user {UserId} at endpoint: {Endpoint} with ZIP URL: {ZipUrl}",
                Sid(userId), S(endpoint), S(normalizedZipUrl));
            var response = await _httpClient.PostAsync(endpoint, content);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError("Replicate training creation failed: {StatusCode} {Error}", response.StatusCode, errorContent);

                switch (response.StatusCode)
                {
                    case HttpStatusCode.Unauthorized:
                        throw new ReplicateApiException(response.StatusCode, "ReplicateAuthFailed",
                            "Replicate API authentication failed. Check your API token.", errorContent);
                    case HttpStatusCode.PaymentRequired:
                        throw new ReplicateApiException(response.StatusCode, "ReplicatePaymentRequired",
                            "Replicate account requires payment.", errorContent);
                    case HttpStatusCode.Forbidden:
                        throw new ReplicateApiException(response.StatusCode, "ReplicatePermissionDenied",
                            "Your token is not permitted to create trainings for this model.", errorContent);
                    case HttpStatusCode.NotFound:
                        throw new ReplicateApiException(response.StatusCode, "ReplicateResourceNotFound",
                            "The specified training resource or destination was not found.", errorContent);
                    case HttpStatusCode.UnprocessableEntity:
                        throw new ReplicateApiException(response.StatusCode, "ReplicateValidationError",
                            "Invalid training request. Check input_images URL and parameters.", errorContent);
                    case HttpStatusCode.TooManyRequests:
                        throw new ReplicateApiException(response.StatusCode, "ReplicateRateLimited",
                            "Replicate API rate limit reached. Please try again later.", errorContent);
                    default:
                        throw new ReplicateApiException(response.StatusCode, "ReplicateServiceError",
                            $"Training creation failed with status {(int)response.StatusCode}.", errorContent);
                }
            }

            var responseJson = await response.Content.ReadAsStringAsync();
            _logger.LogInformation("Replicate training response: {Response}", responseJson);

            var result = JsonSerializer.Deserialize<ReplicateTrainingResult>(
                responseJson,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (result == null)
            {
                throw new Exception("Failed to deserialize training response");
            }

            // Update the model creation request with the training ID
            modelCreationRequest.PendingTrainingRequestId = result.Id;
            modelCreationRequest.Status = ModelCreationStatus.Creating;
            _context.ModelCreationRequests.Update(modelCreationRequest);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Updated model creation request {RequestId} with training ID {TrainingId}",
                modelCreationRequest.Id, Sid(result.Id));

            return result;
        }
        catch (HttpRequestException ex) when (ex.Message.Contains("401") || ex.Message.Contains("Unauthorized"))
        {
            _logger.LogError(ex, "Replicate API authentication failed for user {UserId}", Sid(userId));
            await MarkModelRequestFailedAsync(modelCreationRequest, $"Replicate API authentication failed. {ex.Message}");
            throw new UnauthorizedAccessException("Replicate API authentication failed. Check your API token.", ex);
        }
        catch (HttpRequestException ex) when (ex.Message.Contains("429") || ex.Message.Contains("rate limit"))
        {
            _logger.LogWarning(ex, "Replicate API rate limit reached for user {UserId}", Sid(userId));
            await MarkModelRequestFailedAsync(modelCreationRequest, $"Replicate API rate limit reached. {ex.Message}");
            throw new InvalidOperationException("Replicate API rate limit reached. Please try again later.", ex);
        }
        catch (HttpRequestException ex) when (ex.Message.Contains("402") || ex.Message.Contains("payment"))
        {
            _logger.LogError(ex, "Replicate API payment required for user {UserId}", Sid(userId));
            await MarkModelRequestFailedAsync(modelCreationRequest, $"Replicate API payment required. {ex.Message}");
            throw new InvalidOperationException("Replicate API payment required. Please check your billing.", ex);
        }
        catch (ReplicateApiException ex)
        {
            _logger.LogError(ex, "Replicate API error creating model training for user {UserId} ({StatusCode} {Code})", Sid(userId), (int)ex.StatusCode, S(ex.ErrorCode));
            await MarkModelRequestFailedAsync(modelCreationRequest, $"{ex.ErrorCode}: {ex.Message}");
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating model training for user {UserId}", Sid(userId));
            await MarkModelRequestFailedAsync(modelCreationRequest, ex.Message);
            throw;
        }
    }

    private async Task MarkModelRequestFailedAsync(ModelCreationRequest? request, string? rawMessage)
    {
        if (request == null)
        {
            return;
        }

        // Ensure a failed Replicate call doesn't leave the dashboard stuck in "Training" forever.
        if (request.Status != ModelCreationStatus.Pending && request.Status != ModelCreationStatus.Creating)
        {
            return;
        }

        if (request.CompletedAt.HasValue)
        {
            return;
        }

        var message = string.IsNullOrWhiteSpace(rawMessage) ? "Training failed to start." : rawMessage.Trim();
        message = ScrubFailureMessage(message);
        if (message.Length > 500)
        {
            message = message.Substring(0, 500) + "...";
        }

        try
        {
            request.Status = ModelCreationStatus.Failed;
            request.ErrorMessage = message;
            request.CompletedAt = DateTime.UtcNow;
            _context.ModelCreationRequests.Update(request);
            await _context.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to mark model creation request {RequestId} as failed after Replicate error", Sid(request.Id));
        }
    }

    private static string ScrubFailureMessage(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return "Training failed to start.";
        }

        // Avoid storing SAS tokens / secrets in DB error fields (user-visible).
        // Strip query strings from any URLs present in the message.
        var scrubbed = Regex.Replace(message, @"https?://\S+", match =>
        {
            var url = match.Value.TrimEnd('.', ',', ';', ')', ']');
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            {
                return match.Value;
            }

            return uri.GetLeftPart(UriPartial.Path);
        });

        // Best-effort redaction for common token patterns.
        scrubbed = Regex.Replace(scrubbed, @"(?i)(sig=)[^&\s]+", "$1[redacted]");
        scrubbed = Regex.Replace(scrubbed, @"(?i)(token=)[^&\s]+", "$1[redacted]");

        return scrubbed;
    }

    /// <summary>
    /// Gets the status of a model training
    /// </summary>
    /// <param name="trainingId">The training ID</param>
    /// <returns>The current training status</returns>
    public async Task<ReplicateTrainingResult> GetTrainingStatusAsync(string trainingId)
    {
        try
        {
            if (!_mockEnabled && !_authConfigured)
            {
                throw new UnauthorizedAccessException("Replicate API token not configured. Set REPLICATE_API_TOKEN (recommended) or Replicate:ApiToken.");
            }

            var response = await _httpClient.GetAsync($"trainings/{trainingId}");

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError("Failed to get training status: {ErrorContent}", errorContent);
                throw new Exception($"Failed to get training status: {response.StatusCode}, {errorContent}");
            }

            var responseJson = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<ReplicateTrainingResult>(
                responseJson,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (result == null)
            {
                throw new Exception("Failed to deserialize training status response");
            }

            return result;
        }
        catch (HttpRequestException ex) when (ex.Message.Contains("401") || ex.Message.Contains("Unauthorized"))
        {
            _logger.LogError(ex, "Replicate API authentication failed for training {TrainingId}", Sid(trainingId));
            throw new UnauthorizedAccessException("Replicate API authentication failed. Check your API token.", ex);
        }
        catch (HttpRequestException ex) when (ex.Message.Contains("404") || ex.Message.Contains("not found"))
        {
            _logger.LogWarning(ex, "Training {TrainingId} not found", Sid(trainingId));
            throw new InvalidOperationException($"Training {trainingId} not found.", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting training status for training {TrainingId}", Sid(trainingId));
            throw;
        }
    }

    /// <summary>
    /// Generates images using the trained model and a specific style
    /// </summary>
    /// <param name="trainedModelVersion">The trained model version</param>
    /// <param name="userId">The user ID</param>
    /// <param name="style">The style to use for generation</param>
    /// <param name="userInfo">Optional user info for style generation</param>
    /// <returns>The prediction ID and status</returns>
    public async Task<ReplicatePredictionResult> GenerateImagesAsync(
        string trainedModelVersion,
        string userId,
        string style,
        UserInfo? userInfo = null,
        int numOutputs = 2)
    {
        try
        {
            // Get style template from database and create prompt
            var stylePrompts = await GetStylePromptsFromDatabase(style);
            string stylePrompt = CreateFluxStylePrompt(stylePrompts.PromptTemplate, userInfo, userId);
            string negativePrompt = CreateFluxNegativePrompt(stylePrompts.NegativePromptTemplate, userInfo);
            var styleTuning = ResolveStyleTuning(_configuration, style);
            stylePrompt = ApplyRealismModifier(stylePrompt);

            _logger.LogInformation("Generating images with model version: {ModelVersion} for user: {UserId}, style: {Style}",
                S(trainedModelVersion), Sid(userId), S(style));
            _logger.LogDebug(
                "Resolved style tuning for {Style} ({Group}) => guidance {GuidanceScale}, steps {NumInferenceSteps}",
                S(style),
                styleTuning.Group,
                styleTuning.GuidanceScale,
                styleTuning.NumInferenceSteps);
            _logger.LogInformation("Generated prompt: {Prompt}", S(stylePrompt));
            _logger.LogInformation("Generated negative prompt: {NegativePrompt}", S(negativePrompt));

            // Get webhook URL for async processing
            var webhookUrl = await _webhookUrlResolver.GetWebhookUrlAsync("/api/webhooks/replicate/prediction-complete");

            // Build standardized Replicate API request payload for trained model
            var predictionRequest = new
            {
                version = trainedModelVersion, // Trained model version: "owner/model:versionHash"
                input = new Dictionary<string, object?>
                {
                    ["model"] = "dev",
                    ["width"] = 520,
                    ["height"] = 520,
                    ["prompt"] = stylePrompt,
                    ["txt"] = stylePrompt, // Required by trained LoRA models
                    ["negative_prompt"] = negativePrompt,
                    ["go_fast"] = false,
                    ["lora_scale"] = 1,
                    ["megapixels"] = "1",
                    ["num_outputs"] = Math.Max(1, Math.Min(4, numOutputs)), // Clamp between 1-4
                    ["aspect_ratio"] = "1:1",
                    ["output_format"] = "png",
                    ["guidance_scale"] = styleTuning.GuidanceScale,
                    ["output_quality"] = 80,
                    ["prompt_strength"] = 0.8,
                    ["extra_lora_scale"] = 1,
                    ["num_inference_steps"] = styleTuning.NumInferenceSteps,
                    // Metadata for webhook processing
                    ["user_id"] = userId,
                    ["style"] = style
                },
                webhook = webhookUrl,
                webhook_events_filter = new[] { "completed" }
            };

            var content = new StringContent(
                JsonSerializer.Serialize(predictionRequest),
                Encoding.UTF8,
                "application/json");

            // Always use predictions endpoint - single execution path
            var response = await _httpClient.PostAsync("predictions", content);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError("Replicate prediction creation failed: {Status} {Error}", response.StatusCode, errorContent);

                if (response.StatusCode == HttpStatusCode.UnprocessableEntity &&
                    (errorContent.Contains("Version not available yet", StringComparison.OrdinalIgnoreCase) ||
                     errorContent.Contains("not available yet", StringComparison.OrdinalIgnoreCase)))
                {
                    throw new ReplicateApiException(response.StatusCode, "ReplicateVersionNotAvailable",
                        "Model version is not available yet on Replicate. Please try again shortly.", errorContent);
                }

                if (response.StatusCode == HttpStatusCode.TooManyRequests)
                {
                    throw new ReplicateApiException(response.StatusCode, "ReplicateRateLimited",
                        "Replicate API rate limit reached. Please try again later.", errorContent);
                }

                if (response.StatusCode == HttpStatusCode.PaymentRequired)
                {
                    throw new ReplicateApiException(response.StatusCode, "ReplicatePaymentRequired",
                        "Replicate account requires payment.", errorContent);
                }

                if (response.StatusCode == HttpStatusCode.Forbidden)
                {
                    throw new ReplicateApiException(response.StatusCode, "ReplicatePermissionDenied",
                        "Your token is not permitted for this prediction.", errorContent);
                }

                throw new ReplicateApiException(response.StatusCode, "ReplicateServiceError",
                    $"Prediction creation failed with status {(int)response.StatusCode}.", errorContent);
            }

            var responseJson = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<ReplicatePredictionResult>(
                responseJson,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (result == null)
            {
                throw new Exception("Failed to deserialize prediction response");
            }

            // Persist ownership for status checks
            try
            {
                if (!string.IsNullOrEmpty(result.Id))
                {
                    _context.Predictions.Add(new Prediction
                    {
                        Id = result.Id!,
                        UserId = userId,
                        Style = style,
                        CreatedAt = DateTime.UtcNow
                    });
                    await _context.SaveChangesAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Failed to persist prediction ownership for {PredictionId} (user {UserId})",
                    Sid(result.Id),
                    Sid(userId));
            }

            return result;
        }
        catch (HttpRequestException ex) when (ex.Message.Contains("401") || ex.Message.Contains("Unauthorized"))
        {
            _logger.LogError(ex, "Replicate API authentication failed for user {UserId} with style {Style}", Sid(userId), S(style));
            throw new UnauthorizedAccessException("Replicate API authentication failed. Check your API token.", ex);
        }
        catch (HttpRequestException ex) when (ex.Message.Contains("429") || ex.Message.Contains("rate limit"))
        {
            _logger.LogWarning(ex, "Replicate API rate limit reached for user {UserId} with style {Style}", Sid(userId), S(style));
            throw new InvalidOperationException("Replicate API rate limit reached. Please try again later.", ex);
        }
        catch (HttpRequestException ex) when (ex.Message.Contains("402") || ex.Message.Contains("payment"))
        {
            _logger.LogError(ex, "Replicate API payment required for user {UserId} with style {Style}", Sid(userId), S(style));
            throw new InvalidOperationException("Replicate API payment required. Please check your billing.", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating images for user {UserId} with style {Style}", Sid(userId), S(style));
            throw;
        }
    }

    /// <summary>
    /// Generates images using the base generation model (no trained model required).
    /// </summary>
    /// <param name="style">The style to use for generation</param>
    /// <param name="userInfo">Optional user info for style generation</param>
    /// <param name="numOutputs">Number of images to generate (1-4)</param>
    /// <returns>The prediction ID and status</returns>
    public async Task<ReplicatePredictionResult> GenerateBaseStylePreviewAsync(
        string style,
        UserInfo? userInfo = null,
        int numOutputs = 1)
    {
        try
        {
            var stylePrompts = await GetStylePromptsFromDatabase(style);
            string stylePrompt = CreateFluxStylePromptBasic(stylePrompts.PromptTemplate, userInfo);
            string negativePrompt = CreateFluxNegativePrompt(stylePrompts.NegativePromptTemplate, userInfo);
            var styleTuning = ResolveStyleTuning(_configuration, style);
            stylePrompt = ApplyRealismModifier(stylePrompt);

            var modelId = _configuration["Replicate:FluxGenerationModelId"];
            if (string.IsNullOrWhiteSpace(modelId))
            {
                throw new InvalidOperationException("Replicate:FluxGenerationModelId is not configured.");
            }

            var input = new Dictionary<string, object?>
            {
                ["prompt"] = stylePrompt,
                ["negative_prompt"] = negativePrompt,
                ["width"] = 520,
                ["height"] = 520,
                ["num_outputs"] = Math.Max(1, Math.Min(4, numOutputs)),
                ["guidance_scale"] = styleTuning.GuidanceScale,
                ["num_inference_steps"] = styleTuning.NumInferenceSteps,
                ["output_format"] = "png",
                ["output_quality"] = 80
            };

            string endpoint;
            object requestPayload;
            if (modelId.Contains(":", StringComparison.Ordinal))
            {
                endpoint = "predictions";
                requestPayload = new Dictionary<string, object?>
                {
                    ["version"] = modelId,
                    ["input"] = input
                };
            }
            else
            {
                var modelParts = modelId.Split('/', 2);
                if (modelParts.Length != 2)
                {
                    throw new InvalidOperationException("Replicate:FluxGenerationModelId must be 'owner/model' or 'owner/model:version'.");
                }

                endpoint = $"models/{modelParts[0]}/{modelParts[1]}/predictions";
                requestPayload = new Dictionary<string, object?>
                {
                    ["input"] = input
                };
            }

            var content = new StringContent(
                JsonSerializer.Serialize(requestPayload),
                Encoding.UTF8,
                "application/json");

            var response = await _httpClient.PostAsync(endpoint, content);
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError("Replicate base prediction failed: {Status} {Error}", response.StatusCode, errorContent);

                if (response.StatusCode == HttpStatusCode.TooManyRequests)
                {
                    throw new ReplicateApiException(response.StatusCode, "ReplicateRateLimited",
                        "Replicate API rate limit reached. Please try again later.", errorContent);
                }

                if (response.StatusCode == HttpStatusCode.PaymentRequired)
                {
                    throw new ReplicateApiException(response.StatusCode, "ReplicatePaymentRequired",
                        "Replicate account requires payment.", errorContent);
                }

                if (response.StatusCode == HttpStatusCode.Forbidden)
                {
                    throw new ReplicateApiException(response.StatusCode, "ReplicatePermissionDenied",
                        "Your token is not permitted for this prediction.", errorContent);
                }

                throw new ReplicateApiException(response.StatusCode, "ReplicateServiceError",
                    $"Prediction creation failed with status {(int)response.StatusCode}.", errorContent);
            }

            var responseJson = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<ReplicatePredictionResult>(
                responseJson,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (result == null)
            {
                throw new Exception("Failed to deserialize prediction response");
            }

            return result;
        }
        catch (HttpRequestException ex) when (ex.Message.Contains("401") || ex.Message.Contains("Unauthorized"))
        {
            _logger.LogError(ex, "Replicate API authentication failed for base preview style {Style}", S(style));
            throw new UnauthorizedAccessException("Replicate API authentication failed. Check your API token.", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating base preview for style {Style}", S(style));
            throw;
        }
    }

    /// <summary>
    /// Gets the status of an image generation prediction
    /// </summary>
    /// <param name="predictionId">The prediction ID</param>
    /// <returns>The current prediction status</returns>
    public async Task<ReplicatePredictionResult> GetPredictionStatusAsync(string predictionId)
    {
        try
        {
            if (_mockEnabled)
            {
                if (s_mockPredictions.TryGetValue(predictionId, out var cached))
                {
                    return cached;
                }
                // Unknown mock id: return a generic succeeded response
                return new ReplicatePredictionResult
                {
                    Id = predictionId,
                    Version = "mock",
                    Status = "succeeded",
                    CreatedAt = DateTime.UtcNow.AddSeconds(-2),
                    CompletedAt = DateTime.UtcNow
                };
            }
            var response = await _httpClient.GetAsync($"predictions/{predictionId}");

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError("Failed to get prediction status: {ErrorContent}", errorContent);
                throw new Exception($"Failed to get prediction status: {response.StatusCode}, {errorContent}");
            }

            var responseJson = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<ReplicatePredictionResult>(
                responseJson,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (result == null)
            {
                throw new Exception("Failed to deserialize prediction status response");
            }

            return result;
        }
        catch (HttpRequestException ex) when (ex.Message.Contains("401") || ex.Message.Contains("Unauthorized"))
        {
            _logger.LogError(ex, "Replicate API authentication failed for prediction {PredictionId}", Sid(predictionId));
            throw new UnauthorizedAccessException("Replicate API authentication failed. Check your API token.", ex);
        }
        catch (HttpRequestException ex) when (ex.Message.Contains("404") || ex.Message.Contains("not found"))
        {
            _logger.LogWarning(ex, "Prediction {PredictionId} not found", Sid(predictionId));
            throw new InvalidOperationException($"Prediction {predictionId} not found.", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting prediction status for prediction {PredictionId}", Sid(predictionId));
            throw;
        }
    }

    /// <summary>
    /// Gets style prompts from database
    /// </summary>
    private async Task<(string PromptTemplate, string NegativePromptTemplate)> GetStylePromptsFromDatabase(string styleName)
    {
        var style = await _context.Styles
            .Where(s => s.Name.ToLower() == styleName.ToLower() && s.IsActive)
            .Select(s => new { s.PromptTemplate, s.NegativePromptTemplate })
            .FirstOrDefaultAsync();

        if (style == null)
        {
            // Fallback to corporate style (exists in database)
            var defaultStyle = await _context.Styles
                .Where(s => s.Name.ToLower() == "corporate" && s.IsActive)
                .Select(s => new { s.PromptTemplate, s.NegativePromptTemplate })
                .FirstOrDefaultAsync();

            if (defaultStyle == null)
            {
                // Ultimate fallback if no styles exist in database
                return (
                    "professional portrait of a {gender} {ethnicity}, {subject}, composition: well-balanced frame with subject focus, lighting: flattering soft light with subtle highlighting, color palette: balanced natural tones, mood: confident and approachable, technical details: high resolution with excellent clarity, additional elements: simple professional background, appropriate attire for industry",
                    "deformed iris, deformed pupils, semi-realistic, cgi, 3d, render, sketch, cartoon, drawing, anime, mutated hands and fingers, deformed, distorted, disfigured, poorly drawn, bad anatomy, wrong anatomy, extra limb, missing limb, floating limbs, disconnected limbs, mutation, mutated, ugly, disgusting, blurry, amputation"
                );
            }

            return (defaultStyle.PromptTemplate, defaultStyle.NegativePromptTemplate);
        }

        return (style.PromptTemplate, style.NegativePromptTemplate);
    }

    /// <summary>
    /// Creates a comprehensive FLUX.1 style prompt by replacing placeholders in the template
    /// </summary>
    private string CreateFluxStylePrompt(string promptTemplate, UserInfo? userInfo, string userId)
    {
        // For trained models, use the trigger word as the subject to activate the trained model
        string triggerWord = $"user_{userId}";

        // Replace all placeholders in the template
        string gender = userInfo?.Gender?.ToLower() ?? "person";
        string ethnicity = userInfo?.Ethnicity?.ToLower() ?? "";

        // Handle gender + ethnicity combination properly
        string genderEthnicityCombo = !string.IsNullOrEmpty(ethnicity) ? $"{gender} {ethnicity}" : gender;

        string result = promptTemplate
            .Replace("{subject}", triggerWord)  // Use trigger word as subject for trained models
            .Replace("{gender} {ethnicity}", genderEthnicityCombo)
            .Replace("{gender}", gender)
            .Replace("{ethnicity}", ethnicity);

        // Clean up extra spaces 
        result = result.Replace("  ", " ").Trim();

        _logger.LogInformation("Generated prompt with trigger word: {Prompt}", S(result));

        return result;
    }

    /// <summary>
    /// Creates a negative prompt by replacing placeholders in the template (no trigger word).
    /// </summary>
    private static string CreateFluxNegativePrompt(string negativePromptTemplate, UserInfo? userInfo)
    {
        if (string.IsNullOrWhiteSpace(negativePromptTemplate))
        {
            return string.Empty;
        }

        // Replace placeholders without using the trained model trigger word.
        string gender = userInfo?.Gender?.ToLower() ?? "person";
        string ethnicity = userInfo?.Ethnicity?.ToLower() ?? "";

        string genderEthnicityCombo = !string.IsNullOrEmpty(ethnicity) ? $"{gender} {ethnicity}" : gender;

        string result = negativePromptTemplate
            .Replace("{gender} {ethnicity}", genderEthnicityCombo)
            .Replace("{gender}", gender)
            .Replace("{ethnicity}", ethnicity)
            .Replace("{subject}", "person");

        // Clean up extra spaces
        return result.Replace("  ", " ").Trim();
    }

    internal static StyleTuningResult ResolveStyleTuning(IConfiguration configuration, string styleName)
    {
        var config = LoadStyleTuningConfig(configuration);
        if (string.IsNullOrWhiteSpace(styleName))
        {
            return new StyleTuningResult(config.DefaultGuidanceScale, config.DefaultNumInferenceSteps, "default");
        }

        var trimmedStyle = styleName.Trim();
        if (config.ProStyles.Contains(trimmedStyle))
        {
            return new StyleTuningResult(config.ProGuidanceScale, config.ProNumInferenceSteps, "pro");
        }

        if (config.CasualRelaxedStyles.Contains(trimmedStyle))
        {
            return new StyleTuningResult(config.CasualGuidanceScale, config.CasualNumInferenceSteps, "casual");
        }

        return new StyleTuningResult(config.DefaultGuidanceScale, config.DefaultNumInferenceSteps, "default");
    }

    internal static string ApplyRealismModifier(string prompt, Random? random = null)
    {
        if (string.IsNullOrWhiteSpace(prompt))
        {
            return prompt;
        }

        var availableModifiers = s_realismPromptModifiers
            .Where(modifier => !prompt.Contains(modifier, StringComparison.OrdinalIgnoreCase))
            .ToArray();
        if (availableModifiers.Length == 0)
        {
            return prompt;
        }

        var rng = random ?? Random.Shared;
        var modifierSelection = availableModifiers[rng.Next(availableModifiers.Length)];
        var trimmedPrompt = prompt.TrimEnd();
        var separator = trimmedPrompt.EndsWith(",", StringComparison.Ordinal) ? " " : ", ";
        return $"{trimmedPrompt}{separator}{modifierSelection}";
    }

    private static StyleTuningConfig LoadStyleTuningConfig(IConfiguration configuration)
    {
        if (!s_styleTuningCache.TryGetValue(configuration, out var cached) || cached.Token.HasChanged)
        {
            var config = BuildStyleTuningConfig(configuration);
            var token = configuration.GetReloadToken();
            cached = new StyleTuningCacheEntry(config, token);
            s_styleTuningCache.Remove(configuration);
            s_styleTuningCache.Add(configuration, cached);
        }

        return cached.Config;
    }

    private static StyleTuningConfig BuildStyleTuningConfig(IConfiguration configuration)
    {
        var config = CreateDefaultStyleTuningConfig();
        var section = configuration.GetSection("Replicate:StyleTuning");
        if (section == null || !section.Exists())
        {
            return config;
        }

        config.DefaultGuidanceScale = ClampGuidanceScale(section.GetValue("DefaultGuidanceScale", config.DefaultGuidanceScale));
        config.DefaultNumInferenceSteps = ClampInferenceSteps(section.GetValue("DefaultNumInferenceSteps", config.DefaultNumInferenceSteps));
        config.ProGuidanceScale = ClampGuidanceScale(section.GetValue("ProGuidanceScale", config.ProGuidanceScale));
        config.ProNumInferenceSteps = ClampInferenceSteps(section.GetValue("ProNumInferenceSteps", config.ProNumInferenceSteps));
        config.CasualGuidanceScale = ClampGuidanceScale(section.GetValue("CasualGuidanceScale", config.CasualGuidanceScale));
        config.CasualNumInferenceSteps = ClampInferenceSteps(section.GetValue("CasualNumInferenceSteps", config.CasualNumInferenceSteps));

        foreach (var style in section.GetSection("ProStyles").Get<string[]>() ?? Array.Empty<string>())
        {
            AddStyleName(config.ProStyles, style);
        }

        foreach (var style in section.GetSection("CasualRelaxedStyles").Get<string[]>() ?? Array.Empty<string>())
        {
            AddStyleName(config.CasualRelaxedStyles, style);
        }

        return config;
    }

    private static StyleTuningConfig CreateDefaultStyleTuningConfig()
    {
        return new StyleTuningConfig
        {
            DefaultGuidanceScale = DefaultGuidanceScale,
            DefaultNumInferenceSteps = DefaultNumInferenceSteps,
            ProGuidanceScale = DefaultProGuidanceScale,
            ProNumInferenceSteps = DefaultProNumInferenceSteps,
            CasualGuidanceScale = DefaultCasualGuidanceScale,
            CasualNumInferenceSteps = DefaultCasualNumInferenceSteps
        };
    }

    private static double ClampGuidanceScale(double value)
    {
        if (double.IsNaN(value) || double.IsInfinity(value))
        {
            return DefaultGuidanceScale;
        }

        return Math.Clamp(value, 0.1, 20.0);
    }

    private static int ClampInferenceSteps(int value)
    {
        if (value <= 0)
        {
            return DefaultNumInferenceSteps;
        }

        return Math.Clamp(value, 1, 80);
    }

    private static void AddStyleName(HashSet<string> target, string? styleName)
    {
        if (!string.IsNullOrWhiteSpace(styleName))
        {
            target.Add(styleName.Trim());
        }
    }

    private sealed class StyleTuningConfig
    {
        public double DefaultGuidanceScale { get; set; }
        public int DefaultNumInferenceSteps { get; set; }
        public double ProGuidanceScale { get; set; }
        public int ProNumInferenceSteps { get; set; }
        public double CasualGuidanceScale { get; set; }
        public int CasualNumInferenceSteps { get; set; }
        public HashSet<string> ProStyles { get; } = new(StringComparer.OrdinalIgnoreCase);
        public HashSet<string> CasualRelaxedStyles { get; } = new(StringComparer.OrdinalIgnoreCase);
    }

    private sealed class StyleTuningCacheEntry
    {
        public StyleTuningCacheEntry(StyleTuningConfig config, Microsoft.Extensions.Primitives.IChangeToken token)
        {
            Config = config;
            Token = token;
        }

        public StyleTuningConfig Config { get; }
        public Microsoft.Extensions.Primitives.IChangeToken Token { get; }
    }

    internal readonly record struct StyleTuningResult(double GuidanceScale, int NumInferenceSteps, string Group);

    /// <summary>
    /// Creates a FLUX.1 style prompt for basic tier (without trigger word)
    /// </summary>
    private string CreateFluxStylePromptBasic(string promptTemplate, UserInfo? userInfo)
    {
        // Get base subject description
        string subject = GetSubjectDescription(userInfo);

        // Replace all placeholders in the template
        string gender = userInfo?.Gender?.ToLower() ?? "person";
        string ethnicity = userInfo?.Ethnicity?.ToLower() ?? "";

        // Handle gender + ethnicity combination properly
        string genderEthnicityCombo = !string.IsNullOrEmpty(ethnicity) ? $"{gender} {ethnicity}" : gender;

        string result = promptTemplate
            .Replace("{subject}", subject)
            .Replace("{gender} {ethnicity}", genderEthnicityCombo)
            .Replace("{gender}", gender)
            .Replace("{ethnicity}", ethnicity);

        // Clean up extra spaces 
        result = result.Replace("  ", " ").Trim();

        _logger.LogInformation("Generated basic prompt: {Prompt}", S(result));

        return result;
    }

    /// <summary>
    /// Gets a personalized subject description based on user information
    /// </summary>
    private string GetSubjectDescription(UserInfo? userInfo)
    {
        if (userInfo == null)
        {
            return "professional person";
        }

        // Build gender description
        string genderDesc = userInfo.Gender?.ToLower() switch
        {
            "male" => "professional man",
            "female" => "professional woman",
            _ => "professional person"
        };

        // Add ethnicity if provided
        string ethnicityDesc = !string.IsNullOrEmpty(userInfo.Ethnicity)
            ? $"{userInfo.Ethnicity} {genderDesc}"
            : genderDesc;

        // Add any additional attributes
        if (userInfo.Attributes != null && userInfo.Attributes.Count > 0)
        {
            string attributes = string.Join(", ", userInfo.Attributes.Values);
            return $"{ethnicityDesc}, {attributes}";
        }

        return ethnicityDesc;
    }

    /// <summary>
    /// Creates a new training using an existing model destination (for webhook-based flow)
    /// </summary>
    /// <param name="userId">The user ID</param>
    /// <param name="imageZipUrl">URL to the zipped training images</param>
    /// <param name="destination">The model destination (owner/model-name)</param>
    /// <returns>The training ID and status</returns>
    public async Task<ReplicateTrainingResult> CreateModelTrainingWithDestinationAsync(string userId, string imageZipUrl, string destination)
    {
        try
        {
            var normalizedZipUrl = await NormalizeImageUrlForExternalAccessAsync(imageZipUrl);
            await PreflightExternalZipUrlAsync(normalizedZipUrl);

            _logger.LogInformation(
                "Creating training for user {UserId} with destination {Destination}",
                Sid(userId),
                S(destination));

            var trainingRequest = new
            {
                destination = destination,
                input = new
                {
                    input_images = normalizedZipUrl,
                    trigger_word = $"user_{userId}",
                    lora_type = "subject",
                    training_steps = 2000
                }
            };

            var content = new StringContent(JsonSerializer.Serialize(trainingRequest), Encoding.UTF8, "application/json");
            var modelVersion = _configuration["Replicate:FluxTrainingModelId"];
            if (string.IsNullOrWhiteSpace(modelVersion) || !modelVersion.Contains(':'))
            {
                throw new InvalidOperationException("Replicate:FluxTrainingModelId is not configured with expected 'owner/model:version' format.");
            }
            var versionId = modelVersion.Split(':')[1];
            var endpoint = $"models/replicate/fast-flux-trainer/versions/{versionId}/trainings";
            var response = await _httpClient.PostAsync(endpoint, content);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError("Replicate training creation failed: {ErrorContent}", errorContent);
                throw new Exception($"Failed to create training: {response.StatusCode}, {errorContent}");
            }

            var responseJson = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<ReplicateTrainingResult>(
                responseJson,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (result == null)
            {
                throw new Exception("Failed to deserialize training response");
            }

            _logger.LogInformation(
                "Training created successfully for user {UserId} with ID {TrainingId}",
                Sid(userId),
                Sid(result.Id));
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating training with destination for user {UserId}", Sid(userId));
            throw;
        }
    }

    /// <summary>
    /// Initiates model creation and training workflow (webhook-based)
    /// </summary>
    /// <param name="userId">The user ID</param>
    /// <param name="imageZipUrl">URL to the zipped training images</param>
    /// <returns>The model creation request ID</returns>
    public async Task<string> InitiateModelCreationAndTrainingAsync(string userId, string imageZipUrl)
    {
        try
        {
            // Create a model creation request record
            var modelCreationRequest = new ModelCreationRequest
            {
                UserId = userId,
                ModelName = $"user-{userId}-{DateTime.UtcNow:yyyyMMddHHmmss}",
                Status = ModelCreationStatus.Pending,
                TrainingImageZipUrl = imageZipUrl,
                PendingTrainingRequestId = Guid.NewGuid().ToString()
            };

            // Add to database first
            _context.ModelCreationRequests.Add(modelCreationRequest);
            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "Created model creation request {RequestId} for user {UserId}",
                modelCreationRequest.Id,
                Sid(userId));

            // Initiate model creation
            var destination = await CreateModelAsync(userId, modelCreationRequest.ModelName,
                $"Custom trained model for user {userId}");

            // Update the request with the Replicate model ID
            modelCreationRequest.ReplicateModelId = destination;
            modelCreationRequest.Status = ModelCreationStatus.Creating;
            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "Model creation initiated for request {RequestId} with destination {Destination}",
                modelCreationRequest.Id,
                S(destination));

            return modelCreationRequest.Id;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error initiating model creation and training for user {UserId}", Sid(userId));
            throw;
        }
    }

    /// <summary>
    /// Generates images using a DTO with all parameters
    /// </summary>
    /// <param name="request">The generate images request</param>
    /// <returns>The prediction result URL</returns>
    public async Task<string> GenerateImagesAsync(GenerateImagesRequestDto request)
    {
        var result = await GenerateImagesAsync(
            request.TrainedModelVersion,
            request.UserId,
            request.Style,
            request.UserInfo,
            request.NumOutputs);

        return result.Id ?? ""; // Return prediction ID
    }


    /// <summary>
    /// Enhances a user's uploaded photo using Flux Kontext Pro for text-based image editing
    /// Provides professional photo enhancement for basic tier users
    /// </summary>
    /// <param name="userId">The user ID</param>
    /// <param name="imageUrl">URL to the user's uploaded photo</param>
    /// <param name="enhancementType">Type of enhancement (professional, portrait, linkedin)</param>
    /// <returns>The prediction result with enhanced image</returns>
    public async Task<ReplicatePredictionResult> EnhancePhotoAsync(string userId, string imageUrl, string enhancementType = "professional")
    {
        try
        {
            // Ensure the image URL is externally accessible (SAS or proxy) for Replicate to fetch
            imageUrl = await NormalizeImageUrlForExternalAccessAsync(imageUrl);

            // Use Flux Kontext Pro for text-based photo enhancement (model-specific predictions endpoint)
            string kontextProModel = _configuration["Replicate:FluxKontextProModelId"] ?? "black-forest-labs/flux-kontext-pro";

            // Extract owner/model; ignore any version suffix as model endpoint doesn't need it
            string modelPartOnly = kontextProModel.Contains(":") ? kontextProModel.Split(':', 2)[0] : kontextProModel;
            var modelParts = modelPartOnly.Split('/', 2);
            if (modelParts.Length != 2)
            {
                throw new InvalidOperationException("Invalid FluxKontextProModelId configuration. Expected 'owner/model' or 'owner/model:version'.");
            }
            var owner = modelParts[0];
            var modelName = modelParts[1];

            // Create enhancement prompt based on type
            string enhancementPrompt = GetEnhancementPrompt(enhancementType);

            // Build input payload (webhook only at top-level, not inside input)
            var input = new Dictionary<string, object?>
            {
                ["input_image"] = imageUrl,
                ["prompt"] = enhancementPrompt,
                ["negative_prompt"] = "blurry, low quality, distorted, deformed, bad anatomy, poor lighting, overexposed, underexposed, artifact, noise",
                ["num_inference_steps"] = 30,
                ["guidance_scale"] = 7.5,
                ["strength"] = 0.8,
                ["output_format"] = "png",
                ["width"] = 1024,
                ["height"] = 1024,
            };

            var webhookUrl = await _webhookUrlResolver.GetWebhookUrlAsync("/api/webhooks/replicate/prediction-complete");
            var requestPayload = new Dictionary<string, object?>
            {
                ["input"] = input,
                ["webhook"] = webhookUrl,
                ["webhook_events_filter"] = new[] { "completed" }
            };

            // Use model-specific predictions endpoint per Replicate API (no version required)
            var endpoint = $"models/{owner}/{modelName}/predictions";
            var content = new StringContent(JsonSerializer.Serialize(requestPayload), Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync(endpoint, content);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError(
                    "Replicate Kontext Pro enhancement failed. Status: {StatusCode}. Body: {ErrorContent}",
                    (int)response.StatusCode,
                    errorContent);

                switch (response.StatusCode)
                {
                    case HttpStatusCode.Unauthorized:
                        throw new UnauthorizedAccessException(
                            "Replicate API authentication failed. Check REPLICATE_API_TOKEN.");
                    case HttpStatusCode.PaymentRequired:
                        throw new InvalidOperationException(
                            "Replicate API payment required. Please check billing.");
                    case (HttpStatusCode)429:
                        throw new InvalidOperationException(
                            "Replicate API rate limit reached. Please try again later.");
                    case HttpStatusCode.BadRequest:
                        // Surface common bad request scenario: inaccessible input image URL
                        throw new ArgumentException(
                            $"Invalid enhancement request. Replicate responded 400. Details: {errorContent}");
                    case HttpStatusCode.UnprocessableEntity:
                        // Common schema errors: missing version, unexpected fields
                        throw new InvalidOperationException(
                            $"Enhancement request validation failed (422). Details: {errorContent}");
                    default:
                        throw new Exception(
                            $"Failed to create Kontext Pro enhancement prediction: {(int)response.StatusCode} {response.StatusCode}, {errorContent}");
                }
            }

            var responseJson = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<ReplicatePredictionResult>(
                responseJson,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (result == null)
            {
                throw new Exception("Failed to deserialize Kontext Pro enhancement response");
            }

            _logger.LogInformation(
                "Kontext Pro enhancement started for user {UserId} with prediction ID {PredictionId}, type: {EnhancementType}",
                Sid(userId),
                Sid(result.Id),
                S(enhancementType));

            // Persist ownership for status checks (same pattern as GenerateImagesAsync)
            try
            {
                if (!string.IsNullOrEmpty(result.Id))
                {
                    _context.Predictions.Add(new Prediction
                    {
                        Id = result.Id!,
                        UserId = userId,
                        Style = $"enhancement:{enhancementType}",
                        CreatedAt = DateTime.UtcNow
                    });
                    await _context.SaveChangesAsync();
                    _logger.LogDebug(
                        "Persisted enhancement prediction {PredictionId} for user {UserId}",
                        Sid(result.Id),
                        Sid(userId));
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Failed to persist enhancement prediction ownership for {PredictionId} (user {UserId})",
                    Sid(result.Id),
                    Sid(userId));
            }

            return result;
        }
        catch (HttpRequestException ex) when (ex.Message.Contains("401") || ex.Message.Contains("Unauthorized"))
        {
            _logger.LogError(ex, "Replicate API authentication failed for Kontext Pro enhancement for user {UserId}", Sid(userId));
            throw new UnauthorizedAccessException("Replicate API authentication failed. Check your API token.", ex);
        }
        catch (HttpRequestException ex) when (ex.Message.Contains("429") || ex.Message.Contains("rate limit"))
        {
            _logger.LogWarning(ex, "Replicate API rate limit reached for Kontext Pro enhancement for user {UserId}", Sid(userId));
            throw new InvalidOperationException("Replicate API rate limit reached. Please try again later.", ex);
        }
        catch (HttpRequestException ex) when (ex.Message.Contains("402") || ex.Message.Contains("payment"))
        {
            _logger.LogError(ex, "Replicate API payment required for Kontext Pro enhancement for user {UserId}", Sid(userId));
            throw new InvalidOperationException("Replicate API payment required. Please check your billing.", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error enhancing photo with Kontext Pro for user {UserId}", Sid(userId));
            throw;
        }
    }

    /// <summary>
    /// Ensures the provided image URL is externally accessible to Replicate's servers.
    /// - For Azurite localhost URLs, rewrites to use ExternalApiBaseUrl/AppBaseUrl with the same path.
    /// - For Azure Blob URLs without SAS, generates a short-lived SAS URL using the storage service.
    /// Leaves other URLs unchanged.
    /// </summary>
    internal async Task<string> NormalizeImageUrlForExternalAccessAsync(string originalUrl)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(originalUrl)) return originalUrl;
            if (!Uri.TryCreate(originalUrl, UriKind.Absolute, out var uri)) return originalUrl;

            // Handle Azurite local URLs by routing through our externally accessible base URL
            var isAzuriteLocal = (uri.Host.Equals("127.0.0.1") || uri.Host.Equals("localhost") || uri.Host.Equals("azurite") || uri.Host.Equals("aipm-azurite"))
                                 && uri.Port == 10000
                                 && uri.AbsolutePath.StartsWith("/devstoreaccount1/", StringComparison.OrdinalIgnoreCase);
            if (isAzuriteLocal)
            {
                var externalBase = _configuration["ExternalApiBaseUrl"]
                                   ?? _configuration["Webhooks:NgrokTunnelUrl"]
                                   ?? _configuration["AppBaseUrl"]
                                   ?? "https://localhost:5001";
                var rewritten = $"{externalBase.TrimEnd('/')}{uri.AbsolutePath}{uri.Query}";
                if (externalBase.Contains("ngrok", StringComparison.OrdinalIgnoreCase)
                    && !rewritten.Contains("ngrok-skip-browser-warning=", StringComparison.OrdinalIgnoreCase))
                {
                    rewritten += (rewritten.Contains('?') ? "&" : "?") + "ngrok-skip-browser-warning=true";
                }
                _logger.LogDebug(
                    "Rewriting Azurite URL for external access {Original} -> {Rewritten}",
                    S(originalUrl),
                    S(rewritten));
                return rewritten;
            }

            // For Azure Blob without SAS, generate SAS so Replicate can fetch the image
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
                                "Generated SAS URL for external access (container={Container}, path={Path})",
                                S(container),
                                S(blobPath));
                            return sasUrl;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(
                            ex,
                            "Failed to generate SAS URL for external access (container={Container}, path={Path})",
                            S(container),
                            S(blobPath));
                    }
                }
            }

            return originalUrl;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to normalize image URL for external access: {Url}", S(originalUrl));
            return originalUrl;
        }
    }

    private async Task PreflightExternalZipUrlAsync(string zipUrl)
    {
        if (_mockEnabled)
        {
            return;
        }

        if (!Uri.TryCreate(zipUrl, UriKind.Absolute, out var uri))
        {
            throw new InvalidOperationException("Training ZIP URL is not a valid absolute URL.");
        }

        using var client = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(10)
        };

        // Prefer HEAD (no body). If unsupported, fall back to a tiny ranged GET.
        using var headRequest = new HttpRequestMessage(HttpMethod.Head, zipUrl);
        using var headResponse = await client.SendAsync(headRequest, HttpCompletionOption.ResponseHeadersRead);
        if (headResponse.IsSuccessStatusCode)
        {
            return;
        }

        if (headResponse.StatusCode is HttpStatusCode.MethodNotAllowed or HttpStatusCode.NotImplemented)
        {
            using var getRequest = new HttpRequestMessage(HttpMethod.Get, zipUrl);
            getRequest.Headers.Range = new RangeHeaderValue(0, 0);
            using var getResponse = await client.SendAsync(getRequest, HttpCompletionOption.ResponseHeadersRead);

            if (getResponse.IsSuccessStatusCode || getResponse.StatusCode == HttpStatusCode.PartialContent)
            {
                return;
            }

            throw new InvalidOperationException(
                $"Training ZIP URL is not reachable ({(int)getResponse.StatusCode} {getResponse.StatusCode}). " +
                $"Ensure your external base URL is correct and reachable: {uri.GetLeftPart(UriPartial.Authority)}");
        }

        throw new InvalidOperationException(
            $"Training ZIP URL is not reachable ({(int)headResponse.StatusCode} {headResponse.StatusCode}). " +
            $"Ensure your external base URL is correct and reachable: {uri.GetLeftPart(UriPartial.Authority)}");
    }

    /// <summary>
    /// Gets enhancement prompt based on enhancement type for Kontext Pro
    /// </summary>
    private static string GetEnhancementPrompt(string enhancementType)
    {
        return enhancementType.ToLower() switch
        {
            "background" => "Remove background and replace with clean professional backdrop, perfect cutout with smooth edges, studio-quality background removal with neutral professional setting",
            "social" => GetRandomSocialMediaPrompt(),
            "cartoon" => "Transform into fun cartoon/animated style illustration with artistic flair, vibrant colors, and playful animated character appearance with stylized features",
            _ => "Enhance this photo with improved lighting, better composition, increased sharpness, and professional quality finish"
        };
    }

    /// <summary>
    /// Gets a random social media enhancement prompt with different background options
    /// </summary>
    private static string GetRandomSocialMediaPrompt()
    {
        var backgroundOptions = new[]
        {
            "tropical beach with palm trees and crystal clear blue water",
            "in front of the Eiffel Tower in Paris with beautiful architecture",
            "at the Golden Gate Bridge in San Francisco with stunning cityscape",
            "in Central Park New York with lush green trees and pathways",
            "at Santorini Greece with white buildings and blue domed churches",
            "at the Grand Canyon with breathtaking natural rock formations",
            "in front of the Colosseum in Rome with ancient architecture",
            "at Machu Picchu Peru with ancient Incan ruins and mountains",
            "at the Great Wall of China with historic stone walls",
            "in front of the Sydney Opera House with harbor views",
            "at Times Square New York with bright lights and urban energy",
            "at the Louvre Museum in Paris with classic French architecture",
            "in a Japanese garden with cherry blossoms and peaceful scenery",
            "at the Hollywood sign in Los Angeles with city hills",
            "at Niagara Falls with powerful waterfalls and mist",
            "in front of Big Ben in London with iconic clock tower",
            "at the Statue of Liberty in New York with harbor views",
            "at the Taj Mahal in India with stunning white marble architecture"
        };

        var random = new Random();
        var selectedBackground = backgroundOptions[random.Next(backgroundOptions.Length)];

        return $"Transform this photo for social media with enhanced lighting, vibrant colors, and Instagram-ready styling. Replace the original background placing the person {selectedBackground}. Keep the person optimized with perfect skin tone, sharp details, and appealing aesthetics while creating an exciting travel destination backdrop perfect for social media sharing";
    }

    /// <summary>
    /// Checks if a model exists and is accessible on Replicate
    /// </summary>
    /// <param name="modelId">The model ID (owner/model-name)</param>
    /// <returns>True if model exists and is accessible, false otherwise</returns>
    public async Task<bool> CheckModelExistsAsync(string modelId)
    {
        try
        {
            var response = await _httpClient.GetAsync($"models/{modelId}");

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Model {ModelId} exists and is accessible", S(modelId));
                return true;
            }
            else if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                _logger.LogInformation("Model {ModelId} not found on Replicate", S(modelId));
                return false;
            }
            else
            {
                _logger.LogWarning("Unable to check model {ModelId} status: {StatusCode}", S(modelId), response.StatusCode);
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking if model {ModelId} exists", S(modelId));
            return false;
        }
    }

    /// <summary>
    /// Deletes a model from Replicate
    /// </summary>
    /// <param name="modelId">The model ID (owner/model-name)</param>
    /// <returns>Success status and error message if failed</returns>
    public async Task<(bool Success, string? ErrorMessage)> DeleteModelAsync(string modelId)
    {
        try
        {
            _logger.LogInformation("Deleting model {ModelId} with proactive version cleanup", S(modelId));

            // Check if model exists first
            var modelResponse = await _httpClient.GetAsync($"models/{modelId}");
            if (modelResponse.StatusCode == HttpStatusCode.NotFound)
            {
                _logger.LogInformation("Model {ModelId} not found, deletion complete", S(modelId));
                return (true, null);
            }
            if (!modelResponse.IsSuccessStatusCode)
            {
                var checkError = await modelResponse.Content.ReadAsStringAsync();
                _logger.LogError("Failed to access model {ModelId}: {StatusCode}", S(modelId), modelResponse.StatusCode);
                return (false, $"Unable to access model: {checkError}");
            }

            // Proactive approach: Always clean versions first
            var versions = await GetModelVersionsAsync(modelId);

            if (versions.Count > 0)
            {
                _logger.LogInformation("Found {VersionCount} versions for model {ModelId}, cleaning up proactively",
                    versions.Count, S(modelId));

                var failedVersions = new List<string>();
                foreach (var versionId in versions)
                {
                    var (success, error) = await DeleteModelVersionAsync(modelId, versionId);
                    if (!success)
                    {
                        _logger.LogWarning("Version cleanup failed: {VersionId} → {Error}", Sid(versionId), S(error));
                        failedVersions.Add(versionId);
                    }
                }

                if (failedVersions.Count > 0)
                {
                    _logger.LogError("Version cleanup incomplete: {FailedCount}/{TotalCount} failed",
                        failedVersions.Count, versions.Count);
                    return (false, $"Failed to delete {failedVersions.Count} versions: {string.Join(", ", failedVersions)}");
                }

                _logger.LogInformation("Version cleanup complete: {VersionCount} versions deleted", versions.Count);
            }
            else
            {
                _logger.LogDebug("No versions found for model {ModelId}, proceeding to model deletion", S(modelId));
            }

            // Delete model (should succeed after version cleanup)
            var deleteResponse = await _httpClient.DeleteAsync($"models/{modelId}");

            if (deleteResponse.IsSuccessStatusCode || deleteResponse.StatusCode == HttpStatusCode.NotFound)
            {
                _logger.LogInformation("Model deletion successful: {ModelId}", S(modelId));
                return (true, null);
            }

            // Handle unexpected deletion failure
            var deleteError = await deleteResponse.Content.ReadAsStringAsync();
            string? errorDetail = ExtractErrorDetail(deleteError);

            _logger.LogError("Unexpected deletion failure for {ModelId}: {StatusCode} - {Error}",
                S(modelId), deleteResponse.StatusCode, S(errorDetail));

            return (false, errorDetail ?? $"Delete failed: {deleteError}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Model deletion error: {ModelId}", S(modelId));
            return (false, $"Network or API error: {ex.Message}");
        }
    }

    /// <summary>
    /// Extracts error detail from API response with fallback
    /// </summary>
    private static string? ExtractErrorDetail(string errorResponse)
    {
        if (string.IsNullOrWhiteSpace(errorResponse)) return null;

        try
        {
            var errorObj = JsonDocument.Parse(errorResponse);
            return errorObj.RootElement.GetProperty("detail").GetString();
        }
        catch
        {
            return errorResponse;
        }
    }

    /// <summary>
    /// Creates a prediction using a specific model and input parameters
    /// </summary>
    /// <param name="modelId">The model ID to use for prediction</param>
    /// <param name="input">Input parameters for the model</param>
    /// <returns>The prediction result</returns>
    public async Task<ReplicatePredictionResult> CreatePredictionAsync(string modelId, Dictionary<string, object> input)
    {
        try
        {
            var predictionRequest = new
            {
                version = modelId,
                input = input
            };

            var content = new StringContent(
                JsonSerializer.Serialize(predictionRequest),
                Encoding.UTF8,
                "application/json");

            var response = await _httpClient.PostAsync("predictions", content);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError("Prediction creation failed: {ErrorContent}", errorContent);
                throw new Exception($"Failed to create prediction: {response.StatusCode}, {errorContent}");
            }

            var responseJson = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<ReplicatePredictionResult>(
                responseJson,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (result == null)
            {
                throw new Exception("Failed to deserialize prediction response");
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating prediction with model {ModelId}", S(modelId));
            throw;
        }
    }

    /// <summary>
    /// Finds existing trained models for a user by scanning Replicate API
    /// </summary>
    /// <param name="userId">The user ID to search for</param>
    /// <returns>List of discovered user models</returns>
    public async Task<List<ReplicateModelInfo>> FindUserModelsByPatternAsync(string userId)
    {
        try
        {
            var models = new List<ReplicateModelInfo>();
            var response = await _httpClient.GetAsync("models");

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Failed to get models list: {StatusCode}", response.StatusCode);
                return models;
            }

            var responseJson = await response.Content.ReadAsStringAsync();
            using var document = JsonDocument.Parse(responseJson);
            var root = document.RootElement;

            if (root.TryGetProperty("results", out var resultsElement) && resultsElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var modelElement in resultsElement.EnumerateArray())
                {
                    if (modelElement.TryGetProperty("name", out var nameProperty) &&
                        modelElement.TryGetProperty("owner", out var ownerProperty))
                    {
                        var name = nameProperty.GetString();
                        var owner = ownerProperty.GetString();

                        if (!string.IsNullOrEmpty(name) && !string.IsNullOrEmpty(owner) &&
                            name.Contains($"user-{userId}"))
                        {
                            models.Add(new ReplicateModelInfo
                            {
                                Name = name,
                                Owner = owner
                            });
                        }
                    }
                }
            }

            _logger.LogInformation("Found {Count} models for user {UserId}", models.Count, Sid(userId));
            return models;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error finding models for user {UserId}", Sid(userId));
            return new List<ReplicateModelInfo>();
        }
    }

    /// <summary>
    /// Gets the latest version ID for a specific model from Replicate API
    /// </summary>
    /// <param name="modelId">The model ID in format owner/model-name</param>
    /// <returns>The latest version ID hash or null if not found</returns>
    public async Task<string?> GetModelVersionAsync(string modelId)
    {
        try
        {
            var response = await _httpClient.GetAsync($"models/{modelId}");

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Failed to get model {ModelId}: {StatusCode}", S(modelId), response.StatusCode);
                return null;
            }

            var responseJson = await response.Content.ReadAsStringAsync();
            using var document = JsonDocument.Parse(responseJson);
            var root = document.RootElement;

            if (root.TryGetProperty("latest_version", out var latestVersionElement) &&
                latestVersionElement.TryGetProperty("id", out var idElement))
            {
                return idElement.GetString();
            }

            _logger.LogWarning("No latest version found for model {ModelId}", S(modelId));
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting model version for {ModelId}", S(modelId));
            return null;
        }
    }

    /// <summary>
    /// Checks if a model exists and is available on Replicate
    /// </summary>
    /// <param name="modelId">The model ID in format owner/model-name</param>
    /// <returns>True if model exists and is available</returns>
    public async Task<bool> CheckModelAvailabilityAsync(string modelId)
    {
        try
        {
            var response = await _httpClient.GetAsync($"models/{modelId}");
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking model availability for {ModelId}", S(modelId));
            return false;
        }
    }

    /// <summary>
    /// Gets all versions for a model from Replicate API with enhanced error handling
    /// </summary>
    /// <param name="modelId">The model ID in format owner/model-name</param>
    /// <returns>List of version IDs</returns>
    public async Task<List<string>> GetModelVersionsAsync(string modelId)
    {
        try
        {
            _logger.LogInformation("Fetching versions for model {ModelId}", S(modelId));
            var response = await _httpClient.GetAsync($"models/{modelId}/versions");

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Failed to fetch versions for model {ModelId}: {StatusCode}", S(modelId), response.StatusCode);
                return new List<string>();
            }

            var responseContent = await response.Content.ReadAsStringAsync();

            // CRITICAL FIX: Validate response content before JSON parsing
            if (string.IsNullOrWhiteSpace(responseContent))
            {
                _logger.LogWarning("Empty response when fetching versions for model {ModelId}", S(modelId));
                return new List<string>();
            }

            // CRITICAL FIX: Robust JSON parsing with specific error handling
            JsonDocument versionData;
            try
            {
                versionData = JsonDocument.Parse(responseContent);
            }
            catch (JsonException jsonEx)
            {
                _logger.LogError(
                    jsonEx,
                    "Invalid JSON response when fetching versions for model {ModelId}. Response: {Response}",
                    S(modelId),
                    S(responseContent));
                return new List<string>();
            }

            var versions = new List<string>();
            if (versionData.RootElement.TryGetProperty("results", out var resultsElement) && resultsElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var version in resultsElement.EnumerateArray())
                {
                    if (version.TryGetProperty("id", out var idElement))
                    {
                        var versionId = idElement.GetString();
                        if (!string.IsNullOrEmpty(versionId))
                        {
                            versions.Add(versionId);
                        }
                    }
                }
            }

            _logger.LogInformation("Found {Count} versions for model {ModelId}", versions.Count, S(modelId));
            return versions;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching versions for model {ModelId}", S(modelId));
            // CRITICAL FIX: Always return empty list instead of throwing to prevent cascade deletion from failing
            return new List<string>();
        }
    }

    public async Task<bool> WaitForModelVersionAvailabilityAsync(string modelId, string versionId, TimeSpan? timeout = null, TimeSpan? maxBackoff = null)
    {
        var effectiveTimeout = timeout ?? TimeSpan.FromSeconds(120);
        var effectiveMaxBackoff = maxBackoff ?? TimeSpan.FromSeconds(20);
        var deadline = DateTime.UtcNow.Add(effectiveTimeout);

        int attempt = 0;
        while (DateTime.UtcNow < deadline)
        {
            try
            {
                var versions = await GetModelVersionsAsync(modelId);
                if (versions.Any(v => string.Equals(v, versionId, StringComparison.OrdinalIgnoreCase)))
                {
                    _logger.LogInformation("Model version {VersionId} is now available for {ModelId}", Sid(versionId), S(modelId));
                    return true;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error while polling versions for {ModelId}; continuing to retry", S(modelId));
            }

            // Exponential backoff with cap
            attempt++;
            var backoffSeconds = Math.Min(Math.Pow(2, attempt) * 1.5, effectiveMaxBackoff.TotalSeconds);
            var delay = TimeSpan.FromSeconds(backoffSeconds);
            var remaining = deadline - DateTime.UtcNow;
            if (remaining <= TimeSpan.Zero)
            {
                break;
            }
            await Task.Delay(delay < remaining ? delay : remaining);
        }

        _logger.LogWarning(
            "Timed out waiting for version {VersionId} of model {ModelId} to become available (timeout {Timeout}s)",
            Sid(versionId),
            S(modelId),
            (int)effectiveTimeout.TotalSeconds);
        return false;
    }

    public async Task<(bool Success, string? ErrorMessage)> DeleteModelVersionAsync(string modelId, string versionId)
    {
        try
        {
            _logger.LogInformation("Deleting version {VersionId} of model {ModelId}", Sid(versionId), S(modelId));
            var response = await _httpClient.DeleteAsync($"models/{modelId}/versions/{versionId}");

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Successfully deleted version {VersionId} of model {ModelId}", Sid(versionId), S(modelId));
                return (true, null);
            }
            else
            {
                var error = await response.Content.ReadAsStringAsync();
                _logger.LogError(
                    "Failed to delete version {VersionId} of model {ModelId}: {StatusCode} - {Error}",
                    Sid(versionId),
                    S(modelId),
                    response.StatusCode,
                    S(error));

                // Parse and return user-friendly error message
                try
                {
                    var errorObj = JsonDocument.Parse(error);
                    var detail = errorObj.RootElement.GetProperty("detail").GetString();
                    return (false, detail ?? error);
                }
                catch
                {
                    return (false, $"Delete version failed: {error}");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting version {VersionId} of model {ModelId}", Sid(versionId), S(modelId));
            return (false, $"Network or API error: {ex.Message}");
        }
    }
}
