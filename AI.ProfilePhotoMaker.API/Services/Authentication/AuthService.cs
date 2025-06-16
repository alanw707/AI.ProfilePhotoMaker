using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using AI.ProfilePhotoMaker.API.Models;
using AI.ProfilePhotoMaker.API.Models.DTOs;
using AI.ProfilePhotoMaker.API.Services.Authentication.interfaces;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;

namespace AI.ProfilePhotoMaker.API.Services.Authentication;

public class AuthService : IAuthService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly IConfiguration _configuration;

    public AuthService(
        UserManager<ApplicationUser> userManager, 
        SignInManager<ApplicationUser> signInManager,
        IConfiguration configuration)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _configuration = configuration;
    }

    public async Task<AuthResponseDto> RegisterAsync(RegisterDto model)
    {
        var userExists = await _userManager.FindByEmailAsync(model.Email);
        if (userExists != null)
        {
            return new AuthResponseDto(false, "User already exists!", "", DateTime.MinValue);
        }

        // Auto-generate username from email (use email prefix before @)
        var userName = model.Email.Split('@')[0];
        ApplicationUser user = new ApplicationUser(userName, model.Email, model.FirstName, model.LastName);

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

        // Generate token for successful registration to automatically log in the user
        var token = GenerateJwtToken(user);
        return new AuthResponseDto(true, "User created successfully!", token.Token, token.Expiration, user.Email, user.FirstName, user.LastName);
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
        return new AuthResponseDto(true, "Login successful!", token.Token, token.Expiration, user.Email, user.FirstName, user.LastName);
    }

    // JWT token generation for external login
    public (string Token, DateTime Expiration) GenerateJwtToken(ApplicationUser user)
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

    public async Task<string> GetExternalLoginUrlAsync(string provider, string returnUrl)
    {
        var properties = _signInManager.ConfigureExternalAuthenticationProperties(provider, $"/api/auth/external-login/callback?returnUrl={returnUrl}");
        var redirectUrl = properties.RedirectUri;
        return await Task.FromResult(redirectUrl ?? "");
    }

    public async Task<AuthResponseDto> ProcessExternalLoginAsync(string provider, string code, string? state = null)
    {
        var info = await _signInManager.GetExternalLoginInfoAsync();
        if (info == null)
        {
            return new AuthResponseDto(false, "Error loading external login information.", "", DateTime.MinValue);
        }

        // Try to sign the user in with this external login provider
        var result = await _signInManager.ExternalLoginSignInAsync(info.LoginProvider, info.ProviderKey, isPersistent: false);
        
        if (result.Succeeded)
        {
            // User already has an account, return JWT token
            var user = await _userManager.FindByLoginAsync(info.LoginProvider, info.ProviderKey);
            if (user != null)
            {
                var token = GenerateJwtToken(user);
                return new AuthResponseDto(true, "External login successful!", token.Token, token.Expiration, user.Email, user.FirstName, user.LastName);
            }
        }

        // If the user doesn't have an account, create one
        var email = info.Principal.FindFirstValue(ClaimTypes.Email);
        var firstName = info.Principal.FindFirstValue(ClaimTypes.GivenName) ?? "";
        var lastName = info.Principal.FindFirstValue(ClaimTypes.Surname) ?? "";

        if (string.IsNullOrEmpty(email))
        {
            return new AuthResponseDto(false, "Email claim not received from external provider.", "", DateTime.MinValue);
        }

        var existingUser = await _userManager.FindByEmailAsync(email);
        if (existingUser != null)
        {
            // User exists but doesn't have external login - add it
            var addLoginResult = await _userManager.AddLoginAsync(existingUser, info);
            if (addLoginResult.Succeeded)
            {
                var token = GenerateJwtToken(existingUser);
                return new AuthResponseDto(true, "External login added and login successful!", token.Token, token.Expiration, existingUser.Email, existingUser.FirstName, existingUser.LastName);
            }
            else
            {
                return new AuthResponseDto(false, "Failed to add external login to existing user.", "", DateTime.MinValue);
            }
        }

        // Create new user
        var userName = email.Split('@')[0];
        var newUser = new ApplicationUser(userName, email, firstName, lastName);
        
        var createResult = await _userManager.CreateAsync(newUser);
        if (createResult.Succeeded)
        {
            var addLoginResult = await _userManager.AddLoginAsync(newUser, info);
            if (addLoginResult.Succeeded)
            {
                var token = GenerateJwtToken(newUser);
                return new AuthResponseDto(true, "User created and external login successful!", token.Token, token.Expiration, newUser.Email, newUser.FirstName, newUser.LastName);
            }
        }

        return new AuthResponseDto(false, "Failed to create user from external login.", "", DateTime.MinValue);
    }
}
