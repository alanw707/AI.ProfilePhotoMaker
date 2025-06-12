using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using AI.ProfilePhotoMaker.API.Models;
using AI.ProfilePhotoMaker.API.Models.DTOs;
using AI.ProfilePhotoMaker.API.Services.Authentication.interfaces;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;

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

        ApplicationUser user = new ApplicationUser(model.UserName, model.Email, model.FirstName, model.LastName);

        var result = await _userManager.CreateAsync(user, model.Password);
        if (!result.Succeeded)
        {
            return new AuthResponseDto(
                false,
                $"User creation failed! Please check user details and try again. Reason: {string.Join("; ", result.Errors.Select(e => e.Description))}",
                "",
                DateTime.MinValue
            );
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
        if (user.UserName != null)
        {
            var authClaims = new List<Claim>
            {
                new Claim(ClaimTypes.Name, user.UserName),
                new Claim(ClaimTypes.NameIdentifier, user.Id),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                new Claim(ClaimTypes.Email, user.Email ?? "")
            };

            var authSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(_configuration["JWT:Secret"] ?? throw new InvalidOperationException("Missing JWT Secret in config file"))
            );

            var expires = DateTime.UtcNow.AddHours(1);

            var token = new JwtSecurityToken(
                issuer: _configuration["JWT:ValidIssuer"],
                audience: _configuration["JWT:ValidAudience"],
                expires: expires,
                claims: authClaims,
                signingCredentials: new SigningCredentials(authSigningKey, SecurityAlgorithms.HmacSha256)
            );

            return (new JwtSecurityTokenHandler().WriteToken(token), expires);
        }

        throw new InvalidOperationException("User name is null. Cannot generate JWT token.");
    }
}
