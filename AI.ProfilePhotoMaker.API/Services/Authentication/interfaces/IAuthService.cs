using AI.ProfilePhotoMaker.API.Models.DTOs;

namespace AI.ProfilePhotoMaker.API.Services.Authentication.interfaces;

public interface IAuthService
{
    Task<AuthResponseDto> RegisterAsync(RegisterDto model);
    Task<AuthResponseDto> LoginAsync(LoginDto model);
    Task<string> GetExternalLoginUrlAsync(string provider, string returnUrl);
    Task<AuthResponseDto> ProcessExternalLoginAsync(string provider, string code, string? state = null);
}