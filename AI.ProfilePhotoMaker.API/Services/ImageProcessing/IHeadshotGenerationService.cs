using AI.ProfilePhotoMaker.API.Models.DTOs;

namespace AI.ProfilePhotoMaker.API.Services.ImageProcessing;

public interface IHeadshotGenerationService
{
    Task<HeadshotGenerationResponseDto> GenerateHeadshotAsync(
        HeadshotGenerationRequestDto request,
        string userId,
        CancellationToken cancellationToken = default);
}
