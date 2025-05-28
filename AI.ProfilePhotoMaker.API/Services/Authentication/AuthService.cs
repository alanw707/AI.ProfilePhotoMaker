using AI.ProfilePhotoMaker.API.Models;
using AI.ProfilePhotoMaker.API.Models.DTOs;
using AI.ProfilePhotoMaker.API.Services.Authentication.interfaces;
using Microsoft.AspNetCore.Identity;

namespace AI.ProfilePhotoMaker.API.Services.Authentication;

public class AuthService : IAuthService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IConfiguration _configuration;

    public AuthService(UserManager<ApplicationUser> userManager, IConfiguration configuration)
    {
        _userManager = userManager;
        _configuration = configuration;
    }

    public async Task<AuthResponseDto> RegisterAsync(RegisterDto model)
    {
        var userExists = await _userManager.FindByEmailAsync(model.Email);
        if (userExists != null)
        {
            return new AuthResponseDto(false, "User already exists!", "", DateTime.MinValue);
        }

        ApplicationUser user = new ApplicationUser(model.UserName, model.FirstName, model.LastName, model.Email);

        var result = await _userManager.CreateAsync(user, model.Password);
        if (!result.Succeeded)
        {
            return new AuthResponseDto(false, "User creation failed! Please check user details and try again.", "", DateTime.MinValue);
        }

        return new AuthResponseDto(true, "User created successfully!", "", DateTime.MinValue);
    }

    public async Task<AuthResponseDto> LoginAsync(LoginDto model)
    {
        var user = await _userManager.FindByEmailAsync(model.Email);
        if (user == null)
        {
            return new AuthResponseDto(false, "Invalid email or password!", "", DateTime.MinValue);
        }

        var isPasswordValid = await _userManager.CheckPasswordAsync(user, model.Password);
        if (!isPasswordValid)
        {
            return new AuthResponseDto(false, "Invalid email or password!", "", DateTime.MinValue);
        }

        var token = GenerateJwtToken(user);
        return new AuthResponseDto(true, "Login successful!", token.Token, token.Expiration);
    }

    // Dummy implementation for GenerateJwtToken to resolve the error.
    // Replace with your actual JWT generation logic.
    private (string Token, DateTime Expiration) GenerateJwtToken(ApplicationUser user)
    {
        // Example dummy token and expiration
        return ("dummy-jwt-token", DateTime.UtcNow.AddHours(1));
    }
}
