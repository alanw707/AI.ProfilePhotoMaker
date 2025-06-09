using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using AI.ProfilePhotoMaker.API.Models;
using AI.ProfilePhotoMaker.API.Models.DTOs;
using AI.ProfilePhotoMaker.API.Models.Replicate;

namespace AI.ProfilePhotoMaker.API.Services.ImageProcessing;

/// <summary>
/// Client service for interacting with the Replicate.com API
/// </summary>
public class ReplicateApiClient : IReplicateApiClient
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<ReplicateApiClient> _logger;

    public ReplicateApiClient(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<ReplicateApiClient> logger)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _logger = logger;

        // Configure HTTP client
        _httpClient.BaseAddress = new Uri("https://api.replicate.com/v1/");
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        
        // Add API token from configuration
        string apiToken = _configuration["Replicate:ApiToken"] 
            ?? throw new InvalidOperationException("Replicate API token not configured");
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Token", apiToken);
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
            var trainingRequest = new
            {
                // Use Flux AI training model (replace with actual model ID)
                version = _configuration["Replicate:FluxTrainingModelId"],
                input = new
                {
                    train_data = imageZipUrl,
                    instance_name = $"user_{userId}_{DateTime.UtcNow:yyyyMMdd}",
                    instance_prompt = "a photo of a person",
                    use_face_detection = true,
                    num_training_steps = 1500,
                    webhook = $"{_configuration["AppBaseUrl"]}/api/webhooks/replicate/training-complete"
                },
                webhook = $"{_configuration["AppBaseUrl"]}/api/webhooks/replicate/training-complete"
            };

            var content = new StringContent(JsonSerializer.Serialize(trainingRequest), Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync("trainings", content);

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
            // Create style prompt based on user info
            string stylePrompt = CreateFluxStylePrompt(style, userInfo);
            string negativePrompt = CreateNegativePrompt(style);
            
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
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting prediction status for prediction {PredictionId}", predictionId);
            throw;
        }
    }

    /// <summary>
    /// Creates a comprehensive FLUX.1 style prompt based on user information and selected style
    /// Following the FLUX.1 prompt guide structure: subject, style, composition, lighting, color palette, mood, technical details
    /// </summary>
    private string CreateFluxStylePrompt(string style, UserInfo? userInfo)
    {
        // Get base subject description
        string subject = GetSubjectDescription(userInfo);

        // Style-specific prompts with FLUX.1 structure
        return style.ToLower() switch
        {
            "professional" => 
                $"{subject}, professional headshot, corporate portrait style, " +
                $"composition: centered subject with neutral background, slight angle, " +
                $"lighting: three-point studio lighting with soft key light, fill light, and rim light, " +
                $"color palette: muted blues and grays with natural skin tones, " +
                $"mood: confident and approachable, " +
                $"technical details: shot with 85mm lens at f/2.8, shallow depth of field, 4K resolution, " +
                $"additional elements: subtle office or gradient background, professional attire, well-groomed appearance",
            
            "casual" => 
                $"{subject}, casual lifestyle portrait, " +
                $"composition: rule of thirds with natural framing, " +
                $"lighting: golden hour natural sunlight with soft diffusion, " +
                $"color palette: warm earthy tones with vibrant accents, " +
                $"mood: relaxed, friendly and authentic, " +
                $"technical details: shot with 50mm lens at f/2.0, medium depth of field, " +
                $"additional elements: outdoor setting with natural elements, casual stylish clothing, genuine smile",
            
            "creative" => 
                $"{subject}, artistic creative portrait, " +
                $"composition: dynamic asymmetrical framing with creative negative space, " +
                $"lighting: dramatic side lighting with colored gels and intentional shadows, " +
                $"color palette: bold contrasting colors with artistic color grading, " +
                $"mood: intriguing and expressive, " +
                $"technical details: shot with wide angle lens, creative perspective, high contrast, " +
                $"additional elements: artistic background elements, creative props or styling, unique fashion elements",
            
            "corporate" => 
                $"{subject}, executive corporate portrait, " +
                $"composition: formal centered composition with professional framing, " +
                $"lighting: classic Rembrandt lighting with soft fill, " +
                $"color palette: deep blues, grays and blacks with subtle accents, " +
                $"mood: authoritative, trustworthy and professional, " +
                $"technical details: shot with medium telephoto lens, optimal clarity and sharpness, " +
                $"additional elements: elegant business attire, office or branded environment subtly visible, power posture",
            
            "linkedin" => 
                $"{subject}, optimized LinkedIn profile photo, " +
                $"composition: head and shoulders framing with balanced negative space above head, " +
                $"lighting: flattering soft light with subtle highlighting, " +
                $"color palette: professional neutral tones with complementary background, " +
                $"mood: approachable yet professional, " +
                $"technical details: 1000x1000 pixel square format, sharp focus on eyes, " +
                $"additional elements: simple clean background, professional but approachable expression, business casual attire",
            
            "academic" => 
                $"{subject}, scholarly academic portrait, " +
                $"composition: dignified framing with intellectual elements, " +
                $"lighting: soft even lighting with subtle gradient, " +
                $"color palette: rich traditional tones with subtle depth, " +
                $"mood: thoughtful, knowledgeable and authoritative, " +
                $"technical details: medium format quality, excellent clarity, " +
                $"additional elements: books, laboratory or campus environment, academic attire or professional clothing, scholarly posture",
            
            "tech" => 
                $"{subject}, modern tech industry portrait, " +
                $"composition: contemporary framing with technical elements, " +
                $"lighting: modern high-key lighting with subtle blue accents, " +
                $"color palette: tech blues and cool grays with vibrant accents, " +
                $"mood: innovative, forward-thinking and approachable, " +
                $"technical details: ultra-high definition, perfect clarity, " +
                $"additional elements: minimal tech environment, modern casual professional attire, confident engaged expression",
            
            "medical" => 
                $"{subject}, healthcare professional portrait, " +
                $"composition: trustworthy frontal composition with medical context, " +
                $"lighting: clean even lighting with healthy glow, " +
                $"color palette: whites, blues and comforting tones, " +
                $"mood: compassionate, competent and reassuring, " +
                $"technical details: sharp focus throughout, excellent clarity, " +
                $"additional elements: medical attire or lab coat, stethoscope or medical environment, caring expression",
            
            "legal" => 
                $"{subject}, legal professional portrait, " +
                $"composition: balanced formal composition with legal elements, " +
                $"lighting: classical portrait lighting with defined shadows, " +
                $"color palette: deep rich tones with mahogany and navy accents, " +
                $"mood: authoritative, trustworthy and dignified, " +
                $"technical details: perfect focus and formal composition, " +
                $"additional elements: legal books, office with wooden elements, formal suit, confident and serious expression",
            
            "executive" => 
                $"{subject}, premium executive portrait, " +
                $"composition: powerful centered composition with prestigious elements, " +
                $"lighting: dramatic executive lighting with defined highlights, " +
                $"color palette: luxury tones with gold, navy and charcoal accents, " +
                $"mood: powerful, successful and commanding, " +
                $"technical details: medium format quality with perfect detail rendering, " +
                $"additional elements: luxury office environment, premium suit or executive attire, leadership pose and expression",
            
            // Default professional fallback
            _ => $"{subject}, professional portrait, " +
                 $"composition: well-balanced frame with subject focus, " +
                 $"lighting: flattering soft light with subtle highlighting, " +
                 $"color palette: balanced natural tones, " +
                 $"mood: confident and approachable, " +
                 $"technical details: high resolution with excellent clarity, " +
                 $"additional elements: simple professional background, appropriate attire for industry"
        };
    }

    /// <summary>
    /// Creates a negative prompt to help the AI avoid common issues
    /// </summary>
    private string CreateNegativePrompt(string style)
    {
        // Base negative prompt to avoid common issues
        string baseNegative = "deformed iris, deformed pupils, semi-realistic, cgi, 3d, render, sketch, cartoon, drawing, anime, mutated hands and fingers, deformed, distorted, disfigured, poorly drawn, bad anatomy, wrong anatomy, extra limb, missing limb, floating limbs, disconnected limbs, mutation, mutated, ugly, disgusting, blurry, amputation";
        
        // Style-specific negative prompts
        return style.ToLower() switch
        {
            "professional" => $"{baseNegative}, casual clothing, t-shirt, vacation setting, party scene, inappropriate attire",
            "creative" => $"{baseNegative}, boring, plain background, standard pose, conventional lighting",
            "corporate" => $"{baseNegative}, casual attire, beach, party scene, inappropriate setting",
            "linkedin" => $"{baseNegative}, full body shot, distracting background, extreme filters, unprofessional setting",
            "tech" => $"{baseNegative}, outdated technology, traditional office, formal suit",
            "medical" => $"{baseNegative}, inappropriate medical setting, casual vacation clothing",
            "legal" => $"{baseNegative}, casual setting, inappropriate attire, party scene",
            "executive" => $"{baseNegative}, casual clothing, unprofessional setting, low quality office",
            _ => baseNegative
        };
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