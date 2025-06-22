namespace AI.ProfilePhotoMaker.API.Models.DTOs;

public record RegisterDto(
    string Email,
    string Password,
    string FirstName,
    string LastName,
    string Gender,
    string Ethnicity
);

public record ProfileCompletionDto(
    string FirstName,
    string LastName,
    string Gender,
    string Ethnicity
);

public record ProfileCompletionCheckDto(
    bool IsCompleted,
    bool HasFirstName,
    bool HasLastName,
    bool HasGender,
    bool HasEthnicity
);