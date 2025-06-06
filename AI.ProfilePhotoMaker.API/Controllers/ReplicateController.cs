using AI.ProfilePhotoMaker.API.Models.DTOs;
using AI.ProfilePhotoMaker.API.Services.ImageProcessing;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AI.ProfilePhotoMaker.API.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize]
public class ReplicateController : ControllerBase
{
    private readonly IReplicateApiClient _replicateApiClient;

    public ReplicateController(IReplicateApiClient replicateApiClient)
    {
        _replicateApiClient = replicateApiClient;
    }

    /// <summary>
    /// Initiates model training for a user
    /// </summary>
    [HttpPost("train")]
    public async Task<IActionResult> TrainModel([FromBody] TrainModelRequestDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(new { success = false, error = new { code = "InvalidModel", message = "Invalid input." } });

        var result = await _replicateApiClient.CreateModelTrainingAsync(dto.UserId, dto.ImageZipUrl);
        return Ok(new { success = true, data = result, error = (object?)null });
    }

    /// <summary>
    /// Gets the status of a model training
    /// </summary>
    [HttpGet("train/status/{trainingId}")]
    public async Task<IActionResult> GetTrainingStatus(string trainingId)
    {
        var result = await _replicateApiClient.GetTrainingStatusAsync(trainingId);
        return Ok(new { success = true, data = result, error = (object?)null });
    }

    /// <summary>
    /// Generates images using a trained model and style
    /// </summary>
    [HttpPost("generate")]
    public async Task<IActionResult> GenerateImages([FromBody] GenerateImagesRequestDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(new { success = false, error = new { code = "InvalidModel", message = "Invalid input." } });

        var result = await _replicateApiClient.GenerateImagesAsync(dto.TrainedModelVersion, dto.UserId, dto.Style, dto.UserInfo);
        return Ok(new { success = true, data = result, error = (object?)null });
    }

    /// <summary>
    /// Gets the status of an image generation prediction
    /// </summary>
    [HttpGet("generate/status/{predictionId}")]
    public async Task<IActionResult> GetPredictionStatus(string predictionId)
    {
        var result = await _replicateApiClient.GetPredictionStatusAsync(predictionId);
        return Ok(new { success = true, data = result, error = (object?)null });
    }
}
