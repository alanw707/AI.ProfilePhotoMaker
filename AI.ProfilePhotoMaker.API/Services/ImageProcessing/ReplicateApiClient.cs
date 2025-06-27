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
            string stylePrompt = CreateFluxStylePrompt(stylePrompts.PromptTemplate, userInfo, userId);
            string negativePrompt = stylePrompts.NegativePromptTemplate;
            
            // Log the model version being used for generation
            _logger.LogInformation("Generating images with model version: {ModelVersion} for user: {UserId}, style: {Style}", 
                trainedModelVersion, userId, style);
            
            // Debug logging for prompt generation
            _logger.LogInformation("Style template from DB: {Template}", stylePrompts.PromptTemplate);
            _logger.LogInformation("UserInfo passed: Gender={Gender}, Ethnicity={Ethnicity}", 
                userInfo?.Gender ?? "NULL", userInfo?.Ethnicity ?? "NULL");
            _logger.LogInformation("Generated prompt: {Prompt}", stylePrompt);
            
            var predictionRequest = new
            {
                version = trainedModelVersion,
                input = new
                {
                    prompt = stylePrompt,
                    negative_prompt = negativePrompt,
                    num_inference_steps = 40,
                    guidance_scale = 7.5,
                    num_outputs = 1,
                    scheduler = "K_EULER_ANCESTRAL",
                    output_format = "png",
                    webhook = $"{_configuration["AppBaseUrl"]}/api/webhooks/replicate/prediction-complete",
                    webhook_events_filter = new[] { "completed" },
                    // Add metadata for webhook processing
                    user_id = userId,
                    style = style
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
    private string CreateFluxStylePrompt(string promptTemplate, UserInfo? userInfo, string userId)
    {
        // Get base subject description
        string subject = GetSubjectDescription(userInfo);

        // Add trigger word (user_ + user ID) to activate the trained model
        string triggerWord = $"user_{userId}";

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

        // Add trigger word at the beginning of the prompt to activate custom model
        result = $"{triggerWord}, {result}";

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

    /// <summary>
    /// Generates a basic casual headshot using base FLUX model (no custom training required)
    /// </summary>
    /// <param name="userId">The user ID</param>
    /// <param name="userInfo">User information for generation</param>
    /// <param name="gender">User's gender for better generation</param>
    /// <returns>The prediction result</returns>
    public async Task<ReplicatePredictionResult> GenerateBasicImageAsync(string userId, UserInfo? userInfo, string gender)
    {
        try
        {
            // Use the base FLUX model for basic tier generations
            string baseFluxModel = _configuration["Replicate:FluxGenerationModelId"] ?? "black-forest-labs/flux-dev";
            
            // Create a casual style prompt for basic tier
            var casualStylePrompts = await GetStylePromptsFromDatabase("casual");
            
            // If casual style not found, use a hardcoded casual prompt
            string stylePrompt;
            string negativePrompt;
            
            if (casualStylePrompts.PromptTemplate != "")
            {
                // For basic tier, no trigger word needed (no custom trained model)
                stylePrompt = CreateFluxStylePromptBasic(casualStylePrompts.PromptTemplate, userInfo);
                negativePrompt = casualStylePrompts.NegativePromptTemplate;
            }
            else
            {
                // Fallback casual prompt for basic tier
                string subject = GetSubjectDescription(userInfo);
                stylePrompt = $"{subject}, casual lifestyle portrait, headshot, composition: rule of thirds with natural framing, lighting: soft natural sunlight with gentle diffusion, color palette: warm earthy tones with vibrant accents, mood: relaxed, friendly and authentic, technical details: shot with 50mm lens at f/2.0, medium depth of field, additional elements: simple clean background, casual stylish clothing, genuine smile";
                negativePrompt = "deformed iris, deformed pupils, semi-realistic, cgi, 3d, render, sketch, cartoon, drawing, anime, mutated hands and fingers, deformed, distorted, disfigured, poorly drawn, bad anatomy, wrong anatomy, extra limb, missing limb, floating limbs, disconnected limbs, mutation, mutated, ugly, disgusting, blurry, amputation, inappropriate attire, nudity, nsfw";
            }

            var predictionRequest = new
            {
                version = baseFluxModel,
                input = new
                {
                    prompt = stylePrompt,
                    negative_prompt = negativePrompt,
                    num_inference_steps = 30, // Slightly lower for basic tier
                    guidance_scale = 7.0,
                    num_outputs = 1, // Only 1 image for basic tier
                    scheduler = "K_EULER_ANCESTRAL",
                    output_format = "png",
                    width = 1024,
                    height = 1024,
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
                _logger.LogError("Replicate basic image generation failed: {ErrorContent}", errorContent);
                throw new Exception($"Failed to create basic image prediction: {response.StatusCode}, {errorContent}");
            }

            var responseJson = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<ReplicatePredictionResult>(
                responseJson, 
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (result == null)
            {
                throw new Exception("Failed to deserialize basic image prediction response");
            }

            _logger.LogInformation("Basic image generation started for user {UserId} with prediction ID {PredictionId}", userId, result.Id);
            return result;
        }
        catch (HttpRequestException ex) when (ex.Message.Contains("401") || ex.Message.Contains("Unauthorized"))
        {
            _logger.LogError(ex, "Replicate API authentication failed for basic generation for user {UserId}", userId);
            throw new UnauthorizedAccessException("Replicate API authentication failed. Check your API token.", ex);
        }
        catch (HttpRequestException ex) when (ex.Message.Contains("429") || ex.Message.Contains("rate limit"))
        {
            _logger.LogWarning(ex, "Replicate API rate limit reached for basic generation for user {UserId}", userId);
            throw new InvalidOperationException("Replicate API rate limit reached. Please try again later.", ex);
        }
        catch (HttpRequestException ex) when (ex.Message.Contains("402") || ex.Message.Contains("payment"))
        {
            _logger.LogError(ex, "Replicate API payment required for basic generation for user {UserId}", userId);
            throw new InvalidOperationException("Replicate API payment required. Please check your billing.", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating basic image for user {UserId}", userId);
            throw;
        }
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
            // Use Flux Kontext Pro for text-based photo enhancement
            string kontextProModel = _configuration["Replicate:FluxKontextProModelId"] ?? "black-forest-labs/flux-kontext-pro";
            
            // Create enhancement prompt based on type
            string enhancementPrompt = GetEnhancementPrompt(enhancementType);
            
            var predictionRequest = new
            {
                version = kontextProModel,
                input = new
                {
                    input_image = imageUrl, // FIX: was 'image', should be 'input_image'
                    prompt = enhancementPrompt,
                    negative_prompt = "blurry, low quality, distorted, deformed, bad anatomy, poor lighting, overexposed, underexposed, artifact, noise",
                    num_inference_steps = 30,
                    guidance_scale = 7.5,
                    strength = 0.8, // How much to modify the original image
                    output_format = "png",
                    width = 1024,
                    height = 1024,
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
                _logger.LogError("Replicate Kontext Pro enhancement failed: {ErrorContent}", errorContent);
                throw new Exception($"Failed to create Kontext Pro enhancement prediction: {response.StatusCode}, {errorContent}");
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
    /// <returns>True if deletion was successful, false otherwise</returns>
    public async Task<bool> DeleteModelAsync(string modelId)
    {
        try
        {
            _logger.LogInformation("Attempting to delete model {ModelId} from Replicate", modelId);
            
            var response = await _httpClient.DeleteAsync($"models/{modelId}");
            
            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Successfully deleted model {ModelId} from Replicate", modelId);
                return true;
            }
            else if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                _logger.LogInformation("Model {ModelId} not found for deletion (may already be deleted)", modelId);
                return true; // Consider this a success since the model is gone
            }
            else if (response.StatusCode == System.Net.HttpStatusCode.Forbidden)
            {
                _logger.LogWarning("Forbidden to delete model {ModelId} - may not be owned by this account", modelId);
                return false;
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError("Failed to delete model {ModelId}: {StatusCode}, {ErrorContent}", 
                    modelId, response.StatusCode, errorContent);
                return false;
            }
        }
        catch (HttpRequestException ex) when (ex.Message.Contains("401") || ex.Message.Contains("Unauthorized"))
        {
            _logger.LogError(ex, "Replicate API authentication failed when deleting model {ModelId}", modelId);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting model {ModelId} from Replicate", modelId);
            return false;
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
                input = input,
                webhook = $"{_configuration["AppBaseUrl"]}/api/webhooks/replicate/prediction-complete",
                webhook_events_filter = new[] { "completed" }
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

            _logger.LogInformation("Prediction created successfully with ID {PredictionId} using model {ModelId}", result.Id, modelId);
            return result;
        }
        catch (HttpRequestException ex) when (ex.Message.Contains("401") || ex.Message.Contains("Unauthorized"))
        {
            _logger.LogError(ex, "Replicate API authentication failed for model {ModelId}", modelId);
            throw new UnauthorizedAccessException("Replicate API authentication failed. Check your API token.", ex);
        }
        catch (HttpRequestException ex) when (ex.Message.Contains("429") || ex.Message.Contains("rate limit"))
        {
            _logger.LogWarning(ex, "Replicate API rate limit reached for model {ModelId}", modelId);
            throw new InvalidOperationException("Replicate API rate limit reached. Please try again later.", ex);
        }
        catch (HttpRequestException ex) when (ex.Message.Contains("402") || ex.Message.Contains("payment"))
        {
            _logger.LogError(ex, "Replicate API payment required for model {ModelId}", modelId);
            throw new InvalidOperationException("Replicate API payment required. Please check your billing.", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating prediction for model {ModelId}", modelId);
            throw;
        }
    }
}