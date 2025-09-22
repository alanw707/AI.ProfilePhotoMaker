using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using AI.ProfilePhotoMaker.API.Configuration;
using AI.ProfilePhotoMaker.API.Data;
using AI.ProfilePhotoMaker.API.Models;
using AI.ProfilePhotoMaker.API.Models.DTOs;
using AI.ProfilePhotoMaker.API.Services.Authentication.interfaces;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace AI.ProfilePhotoMaker.API.Services.Authentication;

public class AuthService : IAuthService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly IConfiguration _configuration;
    private readonly ApplicationDbContext _context;
    private readonly ILogger<AuthService> _logger;
    private readonly LegacyCompatibilityOptions _legacyOptions;

    public AuthService(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        IConfiguration configuration,
        ApplicationDbContext context,
        ILogger<AuthService> logger,
        IOptions<LegacyCompatibilityOptions> legacyOptions)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _configuration = configuration;
        _context = context;
        _logger = logger;
        _legacyOptions = legacyOptions.Value;
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

        // Create UserProfile with the additional information
        var userProfile = new UserProfile
        {
            UserId = user.Id,
            FirstName = model.FirstName,
            LastName = model.LastName,
            Gender = model.Gender,
            Ethnicity = model.Ethnicity,
            SubscriptionTier = SubscriptionTier.Basic,
            Credits = 5,
            LastCreditReset = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.UserProfiles.Add(userProfile);
        await _context.SaveChangesAsync();

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
                new Claim(ClaimTypes.Email, user.Email ?? ""),
                new Claim(ClaimTypes.GivenName, user.FirstName ?? ""),
                new Claim(ClaimTypes.Surname, user.LastName ?? "")
            };

            // Read JWT configuration using the same key casing used by validation ("Jwt")
            var secret = _configuration["Jwt:Secret"];
            if (string.IsNullOrWhiteSpace(secret) && _legacyOptions.EnableAuthLegacyJwtKeyFallback)
            {
                secret = _configuration["JWT:Secret"];
                if (!string.IsNullOrWhiteSpace(secret))
                {
                    _logger.LogWarning(
                        "Using legacy JWT secret configuration key \"JWT:Secret\". Migrate to \"Jwt:Secret\" and disable LegacyCompatibility.EnableAuthLegacyJwtKeyFallback when ready.");
                }
            }

            if (string.IsNullOrWhiteSpace(secret))
            {
                _logger.LogError("Missing JWT Secret in configuration (Jwt:Secret) and legacy fallback disabled or unavailable.");
                throw new InvalidOperationException("Missing JWT Secret in configuration (Jwt:Secret)");
            }

            var authSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));

            var expires = DateTime.UtcNow.AddHours(1);

            // Ensure issuer/audience match the casing used by JwtBearer validation (Program.cs uses "Jwt")
            var issuer = _configuration["Jwt:ValidIssuer"];
            var audience = _configuration["Jwt:ValidAudience"];

            if (string.IsNullOrWhiteSpace(issuer) && _legacyOptions.EnableAuthLegacyJwtKeyFallback)
            {
                issuer = _configuration["JWT:ValidIssuer"];
                if (!string.IsNullOrWhiteSpace(issuer))
                {
                    _logger.LogWarning(
                        "Using legacy JWT issuer configuration key \"JWT:ValidIssuer\". Update to \"Jwt:ValidIssuer\" once environments are migrated.");
                }
            }

            if (string.IsNullOrWhiteSpace(audience) && _legacyOptions.EnableAuthLegacyJwtKeyFallback)
            {
                audience = _configuration["JWT:ValidAudience"];
                if (!string.IsNullOrWhiteSpace(audience))
                {
                    _logger.LogWarning(
                        "Using legacy JWT audience configuration key \"JWT:ValidAudience\". Update to \"Jwt:ValidAudience\" once environments are migrated.");
                }
            }

            var token = new JwtSecurityToken(
                issuer: issuer,
                audience: audience,
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

    public async Task<ProfileCompletionCheckDto> CheckProfileCompletionAsync(string userId)
    {
        var userProfile = await _context.UserProfiles.FirstOrDefaultAsync(up => up.UserId == userId);

        if (userProfile == null)
        {
            return new ProfileCompletionCheckDto(false, false, false, false, false);
        }

        var hasFirstName = !string.IsNullOrWhiteSpace(userProfile.FirstName);
        var hasLastName = !string.IsNullOrWhiteSpace(userProfile.LastName);
        var hasGender = !string.IsNullOrWhiteSpace(userProfile.Gender);
        var hasEthnicity = !string.IsNullOrWhiteSpace(userProfile.Ethnicity);

        var isCompleted = hasFirstName && hasLastName && hasGender && hasEthnicity;

        return new ProfileCompletionCheckDto(isCompleted, hasFirstName, hasLastName, hasGender, hasEthnicity);
    }

    public async Task<bool> CompleteProfileAsync(string userId, ProfileCompletionDto model)
    {
        var userProfile = await _context.UserProfiles.FirstOrDefaultAsync(up => up.UserId == userId);

        if (userProfile == null)
        {
            // Create new profile if it doesn't exist (for OAuth users)
            userProfile = new UserProfile
            {
                UserId = userId,
                FirstName = model.FirstName,
                LastName = model.LastName,
                Gender = model.Gender,
                Ethnicity = model.Ethnicity,
                SubscriptionTier = SubscriptionTier.Basic,
                Credits = 5,
                LastCreditReset = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _context.UserProfiles.Add(userProfile);
        }
        else
        {
            // Update existing profile
            userProfile.FirstName = model.FirstName;
            userProfile.LastName = model.LastName;
            userProfile.Gender = model.Gender;
            userProfile.Ethnicity = model.Ethnicity;
            userProfile.UpdatedAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync();
        return true;
    }
}
