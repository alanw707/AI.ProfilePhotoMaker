using AI.ProfilePhotoMaker.API.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace AI.ProfilePhotoMaker.API.Controllers;

[Route("api/style-preview")]
[ApiController]
[Authorize]
public class StylePreviewController : ControllerBase
{
    private readonly ILogger<StylePreviewController> _logger;
    private readonly ApplicationDbContext _dbContext;
    private readonly IWebHostEnvironment _env;
    private readonly IConfiguration _configuration;
    private readonly string _previewsPath;

    public StylePreviewController(
        ILogger<StylePreviewController> logger,
        ApplicationDbContext dbContext,
        IWebHostEnvironment env,
        IConfiguration configuration)
    {
        _logger = logger;
        _dbContext = dbContext;
        _env = env;
        _configuration = configuration;
        _previewsPath = Path.Combine(_env.ContentRootPath, "style-previews");
        
        // Ensure directory exists
        Directory.CreateDirectory(_previewsPath);
    }

    /// <summary>
    /// Generate a preview image for a specific style
    /// </summary>
    [HttpPost("generate/{styleName}")]
    public async Task<IActionResult> GenerateStylePreview(string styleName)
    {
        try
        {
            var style = await _dbContext.Styles
                .FirstOrDefaultAsync(s => s.Name.ToLower() == styleName.ToLower() && s.IsActive);
                
            if (style == null)
            {
                return NotFound(new { error = $"Style '{styleName}' not found" });
            }

            // Check if preview already exists
            var fileName = $"{style.Name.ToLower().Replace("/", "-").Replace(" ", "-")}-preview.jpg";
            var filePath = Path.Combine(_previewsPath, fileName);
            
            if (System.IO.File.Exists(filePath))
            {
                return Ok(new { 
                    success = true, 
                    message = "Preview already exists", 
                    path = $"/style-previews/{fileName}" 
                });
            }

            // Demographics for diversity
            var genders = new[] { "man", "woman" };
            var ethnicities = new[] { "caucasian", "african american", "asian", "hispanic", "middle eastern", "south asian" };
            var random = new Random();
            
            // Pick random demographics
            var gender = genders[random.Next(genders.Length)];
            var ethnicity = ethnicities[random.Next(ethnicities.Length)];
            
            // Build the prompt
            var prompt = style.PromptTemplate
                .Replace("{gender}", $"{ethnicity} {gender}")
                + ", professional headshot, high quality photography, 4k resolution, studio lighting";

            // Use the Flux Kontext Pro model for quick generation
            var modelId = _configuration["Replicate:FluxKontextProModelId"];
            var apiToken = _configuration["Replicate:ApiToken"];
            
            if (string.IsNullOrEmpty(modelId) || string.IsNullOrEmpty(apiToken))
            {
                return StatusCode(500, new { error = "Replicate configuration missing" });
            }

            // Create prediction directly
            using var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Add("Authorization", $"Token {apiToken}");
            
            var response = await httpClient.PostAsJsonAsync(
                "https://api.replicate.com/v1/predictions",
                new
                {
                    version = modelId.Contains(':') ? modelId.Split(':')[1] : modelId,
                    input = new
                    {
                        prompt = prompt,
                        output_format = "jpg",
                        output_quality = 90,
                        style_name = style.Name // For webhook identification
                    }
                }
            );

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                _logger.LogError("Failed to create prediction: {Error}", error);
                return StatusCode(500, new { error = "Failed to generate preview" });
            }

            var resultJson = await response.Content.ReadAsStringAsync();
            var result = JsonDocument.Parse(resultJson);
            var predictionId = result.RootElement.GetProperty("id").GetString();
            
            _logger.LogInformation("Started style preview generation for {StyleName}, prediction ID: {PredictionId}", 
                style.Name, predictionId);

            return Ok(new { 
                success = true, 
                message = "Preview generation started", 
                predictionId = predictionId,
                estimatedTime = "30-60 seconds"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate preview for style {StyleName}", styleName);
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    /// <summary>
    /// Generate preview images for all active styles
    /// </summary>
    [HttpPost("generate-all")]
    public async Task<IActionResult> GenerateAllPreviews()
    {
        var styles = await _dbContext.Styles.Where(s => s.IsActive).ToListAsync();
        var results = new List<object>();

        foreach (var style in styles)
        {
            try
            {
                var fileName = $"{style.Name.ToLower().Replace("/", "-").Replace(" ", "-")}-preview.jpg";
                var filePath = Path.Combine(_previewsPath, fileName);
                
                if (System.IO.File.Exists(filePath))
                {
                    results.Add(new { 
                        style = style.Name, 
                        status = "exists", 
                        path = $"/style-previews/{fileName}" 
                    });
                    continue;
                }

                // Generate preview for this style
                var generateResult = await GenerateStylePreview(style.Name);
                if (generateResult is OkObjectResult okResult)
                {
                    results.Add(new { style = style.Name, status = "generating", data = okResult.Value });
                }
                else
                {
                    results.Add(new { style = style.Name, status = "error" });
                }
                
                // Add delay to respect rate limits
                await Task.Delay(2000);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to generate preview for style {StyleName}", style.Name);
                results.Add(new { style = style.Name, status = "error", error = ex.Message });
            }
        }

        return Ok(new { 
            success = true, 
            message = "Preview generation process completed", 
            results = results 
        });
    }

    /// <summary>
    /// Check prediction status and download image when ready
    /// </summary>
    [HttpGet("check-status/{predictionId}")]
    public async Task<IActionResult> CheckPredictionStatus(string predictionId)
    {
        try
        {
            var apiToken = _configuration["Replicate:ApiToken"];
            using var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Add("Authorization", $"Token {apiToken}");
            
            var response = await httpClient.GetAsync($"https://api.replicate.com/v1/predictions/{predictionId}");
            if (!response.IsSuccessStatusCode)
            {
                return StatusCode(500, new { error = "Failed to check prediction status" });
            }

            var resultJson = await response.Content.ReadAsStringAsync();
            var result = JsonDocument.Parse(resultJson);
            var root = result.RootElement;
            var status = root.GetProperty("status").GetString();
            
            if (status == "succeeded" && root.TryGetProperty("output", out var output))
            {
                // Get the style name from input
                string? styleName = null;
                if (root.TryGetProperty("input", out var input) && 
                    input.TryGetProperty("style_name", out var styleNameElement))
                {
                    styleName = styleNameElement.GetString();
                }
                
                if (string.IsNullOrEmpty(styleName))
                {
                    return BadRequest(new { error = "Style name not found in prediction" });
                }

                // Download and save the image
                var imageUrl = output[0].GetString();
                var fileName = $"{styleName.ToLower().Replace("/", "-").Replace(" ", "-")}-preview.jpg";
                
                if (!string.IsNullOrEmpty(imageUrl))
                {
                    var imageData = await httpClient.GetByteArrayAsync(imageUrl);
                    var filePath = Path.Combine(_previewsPath, fileName);
                    
                    await System.IO.File.WriteAllBytesAsync(filePath, imageData);
                    
                    _logger.LogInformation("Saved style preview for {StyleName} to {FilePath}", styleName, filePath);
                }
                
                return Ok(new { 
                    success = true, 
                    status = "completed",
                    style = styleName, 
                    path = $"/style-previews/{fileName}" 
                });
            }
            
            return Ok(new { 
                success = true, 
                status = status,
                message = $"Prediction is {status}"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking prediction status");
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    /// <summary>
    /// List all available style previews
    /// </summary>
    [HttpGet("list")]
    [AllowAnonymous]
    public IActionResult ListStylePreviews()
    {
        var previews = new List<object>();
        
        if (Directory.Exists(_previewsPath))
        {
            var files = Directory.GetFiles(_previewsPath, "*-preview.jpg");
            foreach (var file in files)
            {
                var fileName = Path.GetFileName(file);
                var styleName = fileName.Replace("-preview.jpg", "").Replace("-", " ");
                previews.Add(new
                {
                    style = styleName,
                    fileName = fileName,
                    path = $"/style-previews/{fileName}",
                    size = new FileInfo(file).Length
                });
            }
        }
        
        return Ok(new { 
            success = true, 
            count = previews.Count,
            previews = previews 
        });
    }
}