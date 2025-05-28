namespace AI.ProfilePhotoMaker.API.Models.DTOs;

public record AuthResponseDto(
    bool IsSuccess,
    string Message,
    string Token,
    DateTime? Expiration
);