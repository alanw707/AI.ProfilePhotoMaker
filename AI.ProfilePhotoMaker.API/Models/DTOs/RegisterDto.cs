namespace AI.ProfilePhotoMaker.API.Models.DTOs;

public record RegisterDto(
    string UserName,
    string Email,
    string Password,
    string FirstName,
    string LastName
);