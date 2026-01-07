namespace AI.ProfilePhotoMaker.API.Models.DTOs;

public sealed class GenerateProfileImagesResponseDto
{
    public string ImageUrl { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public int CreditsRemaining { get; set; }
    public int CreditsCost { get; set; }
}
