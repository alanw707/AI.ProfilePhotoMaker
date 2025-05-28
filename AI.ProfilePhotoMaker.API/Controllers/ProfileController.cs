using AI.ProfilePhotoMaker.API.Services.ImageProcessing;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace AI.ProfilePhotoMaker.API.Controllers;

[ApiController]
[Route("[controller]")]
[Authorize]
public class ProfileController : ControllerBase
{
    private readonly IImageProcessingService _imageProcessingService;

    public ProfileController(IImageProcessingService imageProcessingService)
    {
        _imageProcessingService = imageProcessingService;
    }

    [HttpGet("styles")]
    public async Task<IActionResult> GetStyles()
    {
        var styles = await _imageProcessingService.GetAvailableStylesAsync();
        return Ok(styles);
    }

    [HttpPost("upload")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> UploadImage(IFormFile file, [FromQuery] string style = "Professional")
    {
        if (file == null || file.Length == 0)
            return BadRequest("Invalid image file.");

        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        try
        {
            var processedImageUrl = await _imageProcessingService.ProcessImageAsync(file, userId, style);
            return Ok(new { ImageUrl = processedImageUrl });
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Internal server error: {ex.Message}");
        }
    }

    private Task<string> ProcessImageAsync(IFormFile file)
    {
        // Implement AI processing logic here
        // For example, call Azure Cognitive Services
        return Task.FromResult($"processed-image-url {file.FileName}");
    }
}