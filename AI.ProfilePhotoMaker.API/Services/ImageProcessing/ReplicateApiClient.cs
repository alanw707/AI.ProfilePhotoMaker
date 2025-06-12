using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using AI.ProfilePhotoMaker.API.Data;
using AI.ProfilePhotoMaker.API.Models;
using AI.ProfilePhotoMaker.API.Models.DTOs;
using AI.ProfilePhotoMaker.API.Models.Replicate;
using Microsoft.EntityFrameworkCore;

namespace AI.ProfilePhotoMaker.API.Services.ImageProcessing;

/// <summary>
/// Client service for interacting with the Replicate.com API
/// </summary>
public class ReplicateApiClient : IReplicateApiClient
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<ReplicateApiClient> _logger;
    private readonly ApplicationDbContext _context;

    public ReplicateApiClient(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<ReplicateApiClient> logger,
        ApplicationDbContext context)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _logger = logger;
        _context = context;

        // Configure HTTP client
        _httpClient.BaseAddress = new Uri("https://api.replicate.com/v1/");
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        
        // Add API token from configuration
        string apiToken = _configuration["Replicate:ApiToken"] 
            ?? throw new InvalidOperationException("Replicate API token not configured");
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Token", apiToken);
    }

    /// <summary>
    /// Creates a new model in Replicate
    /// </summary>
    /// <param name="userId">The user ID</param>
    /// <param name="modelName">The model name</param>
    /// <param name="description">Optional model description</param>
    /// <returns>The created model's full name (owner/model-name)</returns>
    public async Task<string> CreateModelAsync(string userId, string modelName, string description = null)
    {
        try
        {
            var modelRequest = new
            {
                owner = "alanw707",
                name = modelName,
                description = description ?? $"Custom trained model for user {userId}",
                visibility = "private",
                hardware = "gpu-h100"
            };

            var content = new StringContent(JsonSerializer.Serialize(modelRequest), Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync("models", content);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError("Replicate model creation failed: {ErrorContent}", errorContent);
                throw new Exception($"Failed to create model: {response.StatusCode}, {errorContent}");
            }

            var responseJson = await response.Content.ReadAsStringAsync();
            _logger.LogInformation("Model creation response: {Response}", responseJson);
            
            var modelResult = JsonSerializer.Deserialize<JsonElement>(
                responseJson,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            // Extract the full model name (owner/model-name) from the response
            if (modelResult.TryGetProperty("name", out var nameProperty) &&
                modelResult.TryGetProperty("owner", out var ownerProperty))
            {
                var extractedModelName = nameProperty.GetString() ?? throw new Exception("Model name not found in response");
                var owner = ownerProperty.GetString() ?? throw new Exception("Owner not found in response");
                var fullModelName = $"{owner}/{extractedModelName}";
                _logger.LogInformation("Model created with full name: {FullModelName}", fullModelName);
                return fullModelName;
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
                        var fullModelName = $"{owner}/{name}";
                        _logger.LogInformation("Model created with name extracted from URL: {ModelName}", fullModelName);
                        return fullModelName;
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

            var trainingRequest = new
            {
                destination = destination,
                input = new
                {
                    input_images = imageZipUrl,
                    trigger_word = $"user_{userId}",
                    lora_type = "subject",
                    training_steps = 1000
                },
                webhook = $"{_configuration["AppBaseUrl"]}/api/webhooks/replicate/training-complete",
                webhook_events_filter = new[] { "completed" }
            };

            var content = new StringContent(JsonSerializer.Serialize(trainingRequest), Encoding.UTF8, "application/json");
            var modelVersion = _configuration["Replicate:FluxTrainingModelId"];
            var endpoint = $"models/replicate/fast-flux-trainer/versions/{modelVersion.Split(':')[1]}/trainings";
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
        UserInfo? userInfo = null)
    {
        try
        {
            // Get style template from database and create prompt
            var stylePrompts = await GetStylePromptsFromDatabase(style);
            string stylePrompt = CreateFluxStylePrompt(stylePrompts.PromptTemplate, userInfo);
            string negativePrompt = stylePrompts.NegativePromptTemplate;
            
            var predictionRequest = new
            {
                version = trainedModelVersion,
                input = new
                {
                    prompt = stylePrompt,
                    negative_prompt = negativePrompt,
                    num_inference_steps = 40,
                    guidance_scale = 7.5,
                    num_outputs = 4,
                    scheduler = "K_EULER_ANCESTRAL",
                    webhook = $"{_configuration["AppBaseUrl"]}/api/webhooks/replicate/prediction-complete",
                    webhook_events_filter = new[] { "completed" }
                },
                webhook = $"{_configuration["AppBaseUrl"]}/api/webhooks/replicate/prediction-complete"
            };

            var content = new StringContent(
                JsonSerializer.Serialize(predictionRequest), 
                Encoding.UTF8, 
                "application/json");
                
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
    private string CreateFluxStylePrompt(string promptTemplate, UserInfo? userInfo)
    {
        // Get base subject description
        string subject = GetSubjectDescription(userInfo);

        // Replace {subject} placeholder in the template
        return promptTemplate.Replace("{subject}", subject);
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
                    training_steps = 1000
                },
                webhook = $"{_configuration["AppBaseUrl"]}/api/webhooks/replicate/training-complete",
                webhook_events_filter = new[] { "completed" }
            };

            var content = new StringContent(JsonSerializer.Serialize(trainingRequest), Encoding.UTF8, "application/json");
            var modelVersion = _configuration["Replicate:FluxTrainingModelId"];
            var endpoint = $"models/replicate/fast-flux-trainer/versions/{modelVersion.Split(':')[1]}/trainings";
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
            request.UserInfo);

        return result.Id ?? ""; // Return prediction ID
    }
}