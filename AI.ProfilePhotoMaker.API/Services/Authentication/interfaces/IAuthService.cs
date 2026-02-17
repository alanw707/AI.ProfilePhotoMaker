using AI.ProfilePhotoMaker.API.Models.DTOs;
using AI.ProfilePhotoMaker.API.Models;

namespace AI.ProfilePhotoMaker.API.Services.Authentication.interfaces;

public interface IAuthService
{
    Task<AuthResponseDto> RegisterAsync(RegisterDto model);
    Task<AuthResponseDto> LoginAsync(LoginDto model);
    Task<string> GetExternalLoginUrlAsync(string provider, string returnUrl);
    Task<AuthResponseDto> ProcessExternalLoginAsync(string provider, string code, string? state = null);
    Task<ProfileCompletionCheckDto> CheckProfileCompletionAsync(string userId);
    Task<bool> CompleteProfileAsync(string userId, ProfileCompletionDto model);
    (string Token, DateTime Expiration) GenerateJwtToken(ApplicationUser user, IList<string> roles);
}
