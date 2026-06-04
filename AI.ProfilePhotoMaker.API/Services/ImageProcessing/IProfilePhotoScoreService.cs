using AI.ProfilePhotoMaker.API.Models.DTOs;

namespace AI.ProfilePhotoMaker.API.Services.ImageProcessing;

public interface IProfilePhotoScoreService
{
    Task<ProfilePhotoScoreDto> ScoreAsync(Stream imageStream, string fileName, CancellationToken cancellationToken = default);
}
