namespace AI.ProfilePhotoMaker.API.Models.DTOs;

public record AuthResponseDto(
    bool IsSuccess,
    string Message,
    string Token,
    DateTime? Expiration,
    string? Email = null,
    string? FirstName = null,
    string? LastName = null
);