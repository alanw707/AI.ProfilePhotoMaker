using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using AI.ProfilePhotoMaker.API.Data;
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
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<ReplicateApiClient> _logger;
    private readonly ApplicationDbContext _context;
    private readonly IWebhookUrlResolver _webhookUrlResolver;
    private readonly bool _mockEnabled;

    public ReplicateApiClient(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<ReplicateApiClient> logger,
        ApplicationDbContext context,
        IWebhookUrlResolver webhookUrlResolver)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _logger = logger;
        _context = context;
        _webhookUrlResolver = webhookUrlResolver;
        _mockEnabled = (Environment.GetEnvironmentVariable("ENABLE_REPLICATE_MOCK") ?? string.Empty)
            .Equals("true", StringComparison.OrdinalIgnoreCase);

        // Configure HTTP client
        _httpClient.BaseAddress = new Uri("https://api.replicate.com/v1/");
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        // Add API token from configuration unless in mock mode
        if (!_mockEnabled)
        {
            // Prefer explicit environment variable if present, then config binding
            string? envToken = Environment.GetEnvironmentVariable("REPLICATE_API_TOKEN");
            string? cfgToken = _configuration["Replicate:ApiToken"];
            string apiToken = envToken ?? cfgToken
                ?? throw new InvalidOperationException("Replicate API token not configured");
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Token", apiToken);
        }
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
            var modelRequest = new
            {
                owner = "alanw707",
                name = modelName,
                description = description ?? $"Custom trained model for user {userId}",
                visibility = "private",
                hardware = "gpu-h100"
            };

            var content = new StringContent(JsonSerializer.Serialize(modelRequest), Encoding.UTF8, "application/json");
            _logger.LogInformation("Creating model for user {UserId}: {ModelName}", userId, modelName);
            var response = await _httpClient.PostAsync("models", content);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError("Replicate model creation failed: {StatusCode} {ErrorContent}", response.StatusCode, errorContent);

                // Parse and handle specific error cases
                if (response.StatusCode == HttpStatusCode.Unauthorized)
                {
                    _logger.LogError("Replicate API authentication failed during model creation for user {UserId}", userId);
                    throw new UnauthorizedAccessException("Replicate API authentication failed. Check your API token.");
                }
                else if (response.StatusCode == HttpStatusCode.PaymentRequired)
                {
                    _logger.LogError("Replicate API payment required during model creation for user {UserId}", userId);
                    throw new InvalidOperationException("Replicate API payment required. Please check your billing.");
                }
                else if (response.StatusCode == HttpStatusCode.UnprocessableEntity)
                {
                    _logger.LogError("Replicate API validation error during model creation for user {UserId}: {Error}", userId, errorContent);
                    throw new InvalidOperationException($"Invalid model creation request: {errorContent}");
                }
                else if (response.StatusCode == HttpStatusCode.Conflict)
                {
                    _logger.LogError("Model name conflict for user {UserId}: {ModelName}", userId, modelName);
                    throw new InvalidOperationException($"Model name already exists: {modelName}");
                }
                else
                {
                    throw new Exception($"Failed to create model: {response.StatusCode}, {errorContent}");
                }
            }

            var responseJson = await response.Content.ReadAsStringAsync();
            _logger.LogInformation("Model creation response: {Response}", responseJson);

            var modelResult = JsonSerializer.Deserialize<JsonElement>(
                responseJson,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            // Extract the model name (without owner prefix) from the response
            if (modelResult.TryGetProperty("name", out var nameProperty) &&
                modelResult.TryGetProperty("owner", out var ownerProperty))
            {
                var extractedModelName = nameProperty.GetString() ?? throw new Exception("Model name not found in response");
                var owner = ownerProperty.GetString() ?? throw new Exception("Owner not found in response");
                _logger.LogInformation("Model created: owner={Owner}, name={ModelName}", owner, extractedModelName);
                return extractedModelName; // Return model name only, owner is hardcoded
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
                        _logger.LogInformation("Model created with name extracted from URL: owner={Owner}, name={ModelName}", owner, name);
                        return name; // Return model name only, owner is hardcoded
                    }
                }
            }

            // Log the full response for debugging
            _logger.LogError("Unable to extract model name from response: {Response}", responseJson);
            throw new Exception("Model name not found in response");
        }
        catch (HttpRequestException ex) when (ex.Message.Contains("401") || ex.Message.Contains("Unauthorized"))
        {
            _logger.LogError(ex, "Replicate API authentication failed for user {UserId}", userId);
            throw new UnauthorizedAccessException("Replicate API authentication failed. Check your API token.", ex);
        }
        catch (HttpRequestException ex) when (ex.Message.Contains("429") || ex.Message.Contains("rate limit"))
        {
            _logger.LogWarning(ex, "Replicate API rate limit reached for user {UserId}", userId);
            throw new InvalidOperationException("Replicate API rate limit reached. Please try again later.", ex);
        }
        catch (HttpRequestException ex) when (ex.Message.Contains("402") || ex.Message.Contains("payment"))
        {
            _logger.LogError(ex, "Replicate API payment required for user {UserId}", userId);
            throw new InvalidOperationException("Replicate API payment required. Please check your billing.", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating model for user {UserId}", userId);
            throw;
        }
    }

    /// <summary>
    /// Creates a new training for a user's custom model
    /// </summary>
    /// <param name="userId">The user ID</param>
    /// <param name="imageZipUrl">URL to the zipped training images</param>
    /// <returns>The training ID and status</returns>
    public async Task<ReplicateTrainingResult> CreateModelTrainingAsync(string userId, string imageZipUrl)
    {
        try
        {
            // First, create the model to use as destination
            var modelName = $"user-{userId}-{DateTime.UtcNow:yyyyMMddHHmmss}";
            _logger.LogInformation("Creating model {ModelName} for user {UserId}", modelName, userId);
            var destination = await CreateModelAsync(userId, modelName, $"Custom trained model for user {userId}");

            _logger.LogInformation("Model created successfully: {Destination}", destination);
            _logger.LogInformation("Using destination for training: {Destination}", destination);

            // Create a model creation request record to track the training
            var modelCreationRequest = new ModelCreationRequest
            {
                UserId = userId,
                ModelName = modelName,
                // Store full model ID in format owner/model-name for consistency with webhooks
                ReplicateModelId = destination,
                Status = ModelCreationStatus.Pending,
                TrainingImageZipUrl = imageZipUrl,
                PendingTrainingRequestId = Guid.NewGuid().ToString()
            };

            // Add to database
            _context.ModelCreationRequests.Add(modelCreationRequest);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Created model creation request {RequestId} for user {UserId}",
                modelCreationRequest.Id, userId);

            var trainingRequest = new
            {
                destination = destination,
                input = new
                {
                    input_images = imageZipUrl,
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
                userId, endpoint, imageZipUrl);
            var response = await _httpClient.PostAsync(endpoint, content);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError("Replicate training creation failed: {StatusCode} {ErrorContent}", response.StatusCode, errorContent);

                // Parse and handle specific error cases
                if (response.StatusCode == HttpStatusCode.Unauthorized)
                {
                    _logger.LogError("Replicate API authentication failed during training for user {UserId}", userId);
                    throw new UnauthorizedAccessException("Replicate API authentication failed. Check your API token.");
                }
                else if (response.StatusCode == HttpStatusCode.PaymentRequired)
                {
                    _logger.LogError("Replicate API payment required during training for user {UserId}", userId);
                    throw new InvalidOperationException("Replicate API payment required. Please check your billing.");
                }
                else if (response.StatusCode == HttpStatusCode.UnprocessableEntity)
                {
                    _logger.LogError("Replicate API validation error during training for user {UserId}: {Error}", userId, errorContent);
                    throw new InvalidOperationException($"Invalid training request: {errorContent}");
                }
                else if (response.StatusCode == HttpStatusCode.TooManyRequests)
                {
                    _logger.LogWarning("Replicate API rate limit reached for user {UserId}", userId);
                    throw new InvalidOperationException("Replicate API rate limit reached. Please try again later.");
                }
                else
                {
                    throw new Exception($"Failed to create training: {response.StatusCode}, {errorContent}");
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
                modelCreationRequest.Id, result.Id);

            return result;
        }
        catch (HttpRequestException ex) when (ex.Message.Contains("401") || ex.Message.Contains("Unauthorized"))
        {
            _logger.LogError(ex, "Replicate API authentication failed for user {UserId}", userId);
            throw new UnauthorizedAccessException("Replicate API authentication failed. Check your API token.", ex);
        }
        catch (HttpRequestException ex) when (ex.Message.Contains("429") || ex.Message.Contains("rate limit"))
        {
            _logger.LogWarning(ex, "Replicate API rate limit reached for user {UserId}", userId);
            throw new InvalidOperationException("Replicate API rate limit reached. Please try again later.", ex);
        }
        catch (HttpRequestException ex) when (ex.Message.Contains("402") || ex.Message.Contains("payment"))
        {
            _logger.LogError(ex, "Replicate API payment required for user {UserId}", userId);
            throw new InvalidOperationException("Replicate API payment required. Please check your billing.", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating model training for user {UserId}", userId);
            throw;
        }
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
            _logger.LogError(ex, "Replicate API authentication failed for training {TrainingId}", trainingId);
            throw new UnauthorizedAccessException("Replicate API authentication failed. Check your API token.", ex);
        }
        catch (HttpRequestException ex) when (ex.Message.Contains("404") || ex.Message.Contains("not found"))
        {
            _logger.LogWarning(ex, "Training {TrainingId} not found", trainingId);
            throw new InvalidOperationException($"Training {trainingId} not found.", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting training status for training {TrainingId}", trainingId);
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

            _logger.LogInformation("Generating images with model version: {ModelVersion} for user: {UserId}, style: {Style}",
                trainedModelVersion, userId, style);
            _logger.LogInformation("Generated prompt: {Prompt}", stylePrompt);

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
                    ["go_fast"] = false,
                    ["lora_scale"] = 1,
                    ["megapixels"] = "1",
                    ["num_outputs"] = Math.Max(1, Math.Min(4, numOutputs)), // Clamp between 1-4
                    ["aspect_ratio"] = "1:1",
                    ["output_format"] = "png",
                    ["guidance_scale"] = 3,
                    ["output_quality"] = 80,
                    ["prompt_strength"] = 0.8,
                    ["extra_lora_scale"] = 1,
                    ["num_inference_steps"] = 28,
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
                _logger.LogError("Replicate prediction creation failed: {ErrorContent}", errorContent);
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
                _logger.LogWarning(ex, "Failed to persist prediction ownership for {PredictionId} (user {UserId})", result.Id, userId);
            }

            return result;
        }
        catch (HttpRequestException ex) when (ex.Message.Contains("401") || ex.Message.Contains("Unauthorized"))
        {
            _logger.LogError(ex, "Replicate API authentication failed for user {UserId} with style {Style}", userId, style);
            throw new UnauthorizedAccessException("Replicate API authentication failed. Check your API token.", ex);
        }
        catch (HttpRequestException ex) when (ex.Message.Contains("429") || ex.Message.Contains("rate limit"))
        {
            _logger.LogWarning(ex, "Replicate API rate limit reached for user {UserId} with style {Style}", userId, style);
            throw new InvalidOperationException("Replicate API rate limit reached. Please try again later.", ex);
        }
        catch (HttpRequestException ex) when (ex.Message.Contains("402") || ex.Message.Contains("payment"))
        {
            _logger.LogError(ex, "Replicate API payment required for user {UserId} with style {Style}", userId, style);
            throw new InvalidOperationException("Replicate API payment required. Please check your billing.", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating images for user {UserId} with style {Style}", userId, style);
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
            _logger.LogError(ex, "Replicate API authentication failed for prediction {PredictionId}", predictionId);
            throw new UnauthorizedAccessException("Replicate API authentication failed. Check your API token.", ex);
        }
        catch (HttpRequestException ex) when (ex.Message.Contains("404") || ex.Message.Contains("not found"))
        {
            _logger.LogWarning(ex, "Prediction {PredictionId} not found", predictionId);
            throw new InvalidOperationException($"Prediction {predictionId} not found.", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting prediction status for prediction {PredictionId}", predictionId);
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
            // Fallback to default professional style
            var defaultStyle = await _context.Styles
                .Where(s => s.Name.ToLower() == "professional" && s.IsActive)
                .Select(s => new { s.PromptTemplate, s.NegativePromptTemplate })
                .FirstOrDefaultAsync();

            if (defaultStyle == null)
            {
                // Ultimate fallback if no styles exist in database
                return (
                    "{subject}, professional portrait, composition: well-balanced frame with subject focus, lighting: flattering soft light with subtle highlighting, color palette: balanced natural tones, mood: confident and approachable, technical details: high resolution with excellent clarity, additional elements: simple professional background, appropriate attire for industry",
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

        _logger.LogInformation("Generated prompt with trigger word: {Prompt}", result);

        return result;
    }

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

        _logger.LogInformation("Generated basic prompt: {Prompt}", result);

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
            _logger.LogInformation("Creating training for user {UserId} with destination {Destination}", userId, destination);

            var trainingRequest = new
            {
                destination = destination,
                input = new
                {
                    input_images = imageZipUrl,
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

            _logger.LogInformation("Training created successfully for user {UserId} with ID {TrainingId}", userId, result.Id);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating training with destination for user {UserId}", userId);
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

            _logger.LogInformation("Created model creation request {RequestId} for user {UserId}",
                modelCreationRequest.Id, userId);

            // Initiate model creation
            var destination = await CreateModelAsync(userId, modelCreationRequest.ModelName,
                $"Custom trained model for user {userId}");

            // Update the request with the Replicate model ID
            modelCreationRequest.ReplicateModelId = destination;
            modelCreationRequest.Status = ModelCreationStatus.Creating;
            await _context.SaveChangesAsync();

            _logger.LogInformation("Model creation initiated for request {RequestId} with destination {Destination}",
                modelCreationRequest.Id, destination);

            return modelCreationRequest.Id;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error initiating model creation and training for user {UserId}", userId);
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

            _logger.LogInformation("Kontext Pro enhancement started for user {UserId} with prediction ID {PredictionId}, type: {EnhancementType}",
                userId, result.Id, enhancementType);

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
                    _logger.LogDebug("Persisted enhancement prediction {PredictionId} for user {UserId}", result.Id, userId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to persist enhancement prediction ownership for {PredictionId} (user {UserId})", result.Id, userId);
            }

            return result;
        }
        catch (HttpRequestException ex) when (ex.Message.Contains("401") || ex.Message.Contains("Unauthorized"))
        {
            _logger.LogError(ex, "Replicate API authentication failed for Kontext Pro enhancement for user {UserId}", userId);
            throw new UnauthorizedAccessException("Replicate API authentication failed. Check your API token.", ex);
        }
        catch (HttpRequestException ex) when (ex.Message.Contains("429") || ex.Message.Contains("rate limit"))
        {
            _logger.LogWarning(ex, "Replicate API rate limit reached for Kontext Pro enhancement for user {UserId}", userId);
            throw new InvalidOperationException("Replicate API rate limit reached. Please try again later.", ex);
        }
        catch (HttpRequestException ex) when (ex.Message.Contains("402") || ex.Message.Contains("payment"))
        {
            _logger.LogError(ex, "Replicate API payment required for Kontext Pro enhancement for user {UserId}", userId);
            throw new InvalidOperationException("Replicate API payment required. Please check your billing.", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error enhancing photo with Kontext Pro for user {UserId}", userId);
            throw;
        }
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
                _logger.LogInformation("Model {ModelId} exists and is accessible", modelId);
                return true;
            }
            else if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                _logger.LogInformation("Model {ModelId} not found on Replicate", modelId);
                return false;
            }
            else
            {
                _logger.LogWarning("Unable to check model {ModelId} status: {StatusCode}", modelId, response.StatusCode);
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking if model {ModelId} exists", modelId);
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
            _logger.LogInformation("Deleting model {ModelId} with automatic version cleanup", modelId);
            
            // Check if model exists first
            var modelResponse = await _httpClient.GetAsync($"models/{modelId}");
            if (modelResponse.StatusCode == HttpStatusCode.NotFound)
            {
                _logger.LogInformation("Model {ModelId} not found, considering deletion successful", modelId);
                return (true, null);
            }
            if (!modelResponse.IsSuccessStatusCode)
            {
                var checkError = await modelResponse.Content.ReadAsStringAsync();
                _logger.LogError("Failed to check model {ModelId}: {StatusCode}", modelId, modelResponse.StatusCode);
                return (false, $"Unable to access model: {checkError}");
            }

            // Attempt direct model deletion first
            var deleteResponse = await _httpClient.DeleteAsync($"models/{modelId}");
            if (deleteResponse.IsSuccessStatusCode || deleteResponse.StatusCode == HttpStatusCode.NotFound)
            {
                _logger.LogInformation("Successfully deleted model {ModelId}", modelId);
                return (true, null);
            }

            // Check if deletion failed due to existing versions
            var error = await deleteResponse.Content.ReadAsStringAsync();
            string? errorDetail = null;
            
            try
            {
                var errorObj = JsonDocument.Parse(error);
                errorDetail = errorObj.RootElement.GetProperty("detail").GetString();
            }
            catch
            {
                errorDetail = error;
            }

            // If error indicates model has versions, attempt cascade deletion
            if (errorDetail != null && (
                errorDetail.Contains("existing versions", StringComparison.OrdinalIgnoreCase) ||
                errorDetail.Contains("cannot be deleted", StringComparison.OrdinalIgnoreCase)))
            {
                _logger.LogInformation("Model {ModelId} has existing versions, starting cascade deletion", modelId);
                
                // Get all model versions with enhanced error handling
                var versions = await GetModelVersionsAsync(modelId);
                if (versions.Count == 0)
                {
                    _logger.LogWarning("No versions found for model {ModelId}, but deletion failed due to versions", modelId);
                    return (false, errorDetail ?? "Model has versions but none could be retrieved");
                }

                _logger.LogInformation("Found {VersionCount} versions for model {ModelId}, deleting each version", versions.Count, modelId);

                // Delete each version
                var failedVersions = new List<string>();
                foreach (var versionId in versions)
                {
                    var (versionDeleteSuccess, versionError) = await DeleteModelVersionAsync(modelId, versionId);
                    if (!versionDeleteSuccess)
                    {
                        _logger.LogWarning("Failed to delete version {VersionId} of model {ModelId}: {Error}", 
                            versionId, modelId, versionError);
                        failedVersions.Add(versionId);
                    }
                }

                // Check if any versions failed to delete
                if (failedVersions.Count > 0)
                {
                    _logger.LogError("Failed to delete {FailedCount} versions of model {ModelId}: {FailedVersions}", 
                        failedVersions.Count, modelId, string.Join(", ", failedVersions));
                    return (false, $"Failed to delete {failedVersions.Count} model versions. Cannot proceed with model deletion.");
                }

                _logger.LogInformation("Successfully deleted all {VersionCount} versions of model {ModelId}, retrying model deletion", 
                    versions.Count, modelId);

                // Retry model deletion after clearing all versions
                var retryDeleteResponse = await _httpClient.DeleteAsync($"models/{modelId}");
                if (retryDeleteResponse.IsSuccessStatusCode || retryDeleteResponse.StatusCode == HttpStatusCode.NotFound)
                {
                    _logger.LogInformation("Successfully deleted model {ModelId} after clearing {VersionCount} versions", 
                        modelId, versions.Count);
                    return (true, null);
                }
                else
                {
                    var retryError = await retryDeleteResponse.Content.ReadAsStringAsync();
                    _logger.LogError("Failed to delete model {ModelId} even after clearing versions: {StatusCode} - {Error}", 
                        modelId, retryDeleteResponse.StatusCode, retryError);
                    return (false, $"Model deletion failed even after clearing versions: {retryError}");
                }
            }
            else
            {
                // Different error - not related to versions
                _logger.LogError("Failed to delete model {ModelId}: {StatusCode} - {Error}", 
                    modelId, deleteResponse.StatusCode, error);
                return (false, errorDetail ?? $"Delete failed: {error}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting model {ModelId} with cascade deletion", modelId);
            return (false, $"Network or API error: {ex.Message}");
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
            _logger.LogError(ex, "Error creating prediction with model {ModelId}", modelId);
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

            _logger.LogInformation("Found {Count} models for user {UserId}", models.Count, userId);
            return models;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error finding models for user {UserId}", userId);
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
                _logger.LogError("Failed to get model {ModelId}: {StatusCode}", modelId, response.StatusCode);
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

            _logger.LogWarning("No latest version found for model {ModelId}", modelId);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting model version for {ModelId}", modelId);
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
            _logger.LogError(ex, "Error checking model availability for {ModelId}", modelId);
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
            _logger.LogInformation("Fetching versions for model {ModelId}", modelId);
            var response = await _httpClient.GetAsync($"models/{modelId}/versions");
            
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Failed to fetch versions for model {ModelId}: {StatusCode}", modelId, response.StatusCode);
                return new List<string>();
            }

            var responseContent = await response.Content.ReadAsStringAsync();
            
            // CRITICAL FIX: Validate response content before JSON parsing
            if (string.IsNullOrWhiteSpace(responseContent))
            {
                _logger.LogWarning("Empty response when fetching versions for model {ModelId}", modelId);
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
                _logger.LogError(jsonEx, "Invalid JSON response when fetching versions for model {ModelId}. Response: {Response}", 
                    modelId, responseContent);
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
            
            _logger.LogInformation("Found {Count} versions for model {ModelId}", versions.Count, modelId);
            return versions;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching versions for model {ModelId}", modelId);
            // CRITICAL FIX: Always return empty list instead of throwing to prevent cascade deletion from failing
            return new List<string>();
        }
    }

    public async Task<(bool Success, string? ErrorMessage)> DeleteModelVersionAsync(string modelId, string versionId)
    {
        try
        {
            _logger.LogInformation("Deleting version {VersionId} of model {ModelId}", versionId, modelId);
            var response = await _httpClient.DeleteAsync($"models/{modelId}/versions/{versionId}");
            
            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Successfully deleted version {VersionId} of model {ModelId}", versionId, modelId);
                return (true, null);
            }
            else
            {
                var error = await response.Content.ReadAsStringAsync();
                _logger.LogError("Failed to delete version {VersionId} of model {ModelId}: {StatusCode} - {Error}", versionId, modelId, response.StatusCode, error);
                
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
            _logger.LogError(ex, "Error deleting version {VersionId} of model {ModelId}", versionId, modelId);
            return (false, $"Network or API error: {ex.Message}");
        }
    }
}