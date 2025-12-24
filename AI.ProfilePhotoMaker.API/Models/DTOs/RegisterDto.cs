using AI.ProfilePhotoMaker.API.Models.Validation;

namespace AI.ProfilePhotoMaker.API.Models.DTOs;

public record RegisterDto(
    string Email,
    string Password,
    string FirstName,
    string LastName,
    string Gender,
    string Ethnicity,
    [param: RequireTrue(ErrorMessage = "Age confirmation is required to create an account.")]
    bool AgeConfirmed,
    string? TurnstileToken = null
);

public record ProfileCompletionDto(
    string FirstName,
    string LastName,
    string Gender,
    string Ethnicity,
    [param: RequireTrue(ErrorMessage = "Age confirmation is required to complete profile.")]
    bool AgeConfirmed
);

public record ProfileCompletionCheckDto(
    bool IsCompleted,
    bool HasFirstName,
    bool HasLastName,
    bool HasGender,
    bool HasEthnicity
);
