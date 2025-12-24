using AI.ProfilePhotoMaker.API.Constants;
using AI.ProfilePhotoMaker.API.Models.Validation;

namespace AI.ProfilePhotoMaker.API.Models.DTOs;

public record LoginDto(
    string Email,
    string Password,
    [param: RequireTrue(ErrorMessage = AuthValidationMessages.AgeConfirmationRequiredToSignIn)]
    bool AgeConfirmed
);
