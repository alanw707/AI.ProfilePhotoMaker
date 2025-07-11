using System.Security.Claims;
using AI.ProfilePhotoMaker.API.Data;
using AI.ProfilePhotoMaker.API.Models;
using AI.ProfilePhotoMaker.API.Models.DTOs;
using AI.ProfilePhotoMaker.API.Services.Authentication;
using AI.ProfilePhotoMaker.API.Services.Authentication.interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace AI.ProfilePhotoMaker.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly IConfiguration _configuration;
        private readonly ApplicationDbContext _context;

        public AuthController(
            IAuthService authService,
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            IConfiguration configuration,
            ApplicationDbContext context)
        {
            _authService = authService;
            _userManager = userManager;
            _signInManager = signInManager;
            _configuration = configuration;
            _context = context;
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterDto model)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var result = await _authService.RegisterAsync(model);
            if (!result.IsSuccess)
            {
                return BadRequest(result);
            }

            return Ok(result);
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginDto model)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var result = await _authService.LoginAsync(model);
            if (!result.IsSuccess)
            {
                return BadRequest(result);
            }

            return Ok(result);
        }

        [HttpGet("external-login/{provider}")]
        public IActionResult ExternalLogin(string provider, string returnUrl = "", string frontendUrl = "")
        {
            // Use AppBaseUrl from configuration
            var baseUrl = _configuration["AppBaseUrl"] ?? "http://localhost:5035";
            
            // Include frontendUrl in the callback URL so we know where to redirect after OAuth
            var redirectUrl = $"{baseUrl}/api/auth/external-login/callback?returnUrl={returnUrl}&frontendUrl={frontendUrl}";
            var properties = new Microsoft.AspNetCore.Authentication.AuthenticationProperties 
            { 
                RedirectUri = redirectUrl 
            };
            
            
            return Challenge(properties, provider);
        }

        [HttpGet("external-login/callback")]
        public async Task<IActionResult> ExternalLoginCallback(string returnUrl = "", string code = "", string state = "", string frontendUrl = "")
        {
            
            // Determine the target frontend URL once at the beginning
            var targetFrontendUrl = !string.IsNullOrEmpty(frontendUrl) ? frontendUrl : GetFrontendBaseUrl();
            
            // If we have a code but GetExternalLoginInfoAsync fails due to state validation,
            // try to manually process the Google OAuth code
            if (!string.IsNullOrEmpty(code))
            {
                try
                {
                    // Try to get user info directly from Google using the code
                    var userInfo = await GetGoogleUserInfoAsync(code);
                    if (userInfo != null)
                    {
                        return await ProcessGoogleUserAsync(userInfo.Email, userInfo.GivenName, userInfo.FamilyName, returnUrl, frontendUrl);
                    }
                }
                catch (Exception ex)
                {
                }
            }
            
            // Fallback to standard ASP.NET Core OAuth flow
            var info = await _signInManager.GetExternalLoginInfoAsync();
            if (info == null)
            {
                return Redirect($"{targetFrontendUrl}{returnUrl}?error=external_login_failed");
            }

            // Try to sign in the user with this external login provider
            var result = await _signInManager.ExternalLoginSignInAsync(info.LoginProvider, info.ProviderKey, isPersistent: false);
            
            if (result.Succeeded)
            {
                // User already has an account, generate JWT and redirect
                var user = await _userManager.FindByLoginAsync(info.LoginProvider, info.ProviderKey);
                if (user != null)
                {
                    var token = ((AuthService)_authService).GenerateJwtToken(user);
                    return Redirect($"{targetFrontendUrl}{returnUrl}?token={token.Token}&expiration={token.Expiration}");
                }
            }

            // Create new user or link account
            var email = info.Principal.FindFirstValue(ClaimTypes.Email);
            var firstName = info.Principal.FindFirstValue(ClaimTypes.GivenName) ?? "";
            var lastName = info.Principal.FindFirstValue(ClaimTypes.Surname) ?? "";

            if (string.IsNullOrEmpty(email))
            {
                return Redirect($"{targetFrontendUrl}{returnUrl}?error=no_email_from_provider");
            }

            var existingUser = await _userManager.FindByEmailAsync(email);
            if (existingUser != null)
            {
                // Link external login to existing user
                var addLoginResult = await _userManager.AddLoginAsync(existingUser, info);
                if (addLoginResult.Succeeded)
                {
                    var token = ((AuthService)_authService).GenerateJwtToken(existingUser);
                    return Redirect($"{targetFrontendUrl}{returnUrl}?token={token.Token}&expiration={token.Expiration}");
                }
                else
                {
                    return Redirect($"{targetFrontendUrl}{returnUrl}?error=failed_to_link_account");
                }
            }
            else
            {
                // Create new user
                var userName = email.Split('@')[0];
                var newUser = new ApplicationUser(userName, email, firstName, lastName);
                
                var createResult = await _userManager.CreateAsync(newUser);
                if (createResult.Succeeded)
                {
                    var addLoginResult = await _userManager.AddLoginAsync(newUser, info);
                    if (addLoginResult.Succeeded)
                    {
                        var token = ((AuthService)_authService).GenerateJwtToken(newUser);
                        return Redirect($"{targetFrontendUrl}{returnUrl}?token={token.Token}&expiration={token.Expiration}");
                    }
                }
            }

            return Redirect($"{targetFrontendUrl}{returnUrl}?error=external_login_failed");
        }

        [HttpPost("external-login/callback")]
        public async Task<IActionResult> ExternalLoginCallback([FromBody] ExternalLoginCallbackDto model)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var result = await _authService.ProcessExternalLoginAsync(model.Provider, model.Code, model.State);
            if (!result.IsSuccess)
            {
                return BadRequest(result);
            }

            return Ok(result);
        }



        [HttpGet("profile-completion-status")]
        [Authorize]
        public async Task<IActionResult> GetProfileCompletionStatus()
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized(new { success = false, error = "User not authenticated" });
            }

            var status = await _authService.CheckProfileCompletionAsync(userId);
            return Ok(new { success = true, data = status });
        }

        [HttpPost("complete-profile")]
        [Authorize]
        public async Task<IActionResult> CompleteProfile([FromBody] ProfileCompletionDto model)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized(new { success = false, error = "User not authenticated" });
            }

            var result = await _authService.CompleteProfileAsync(userId, model);
            if (result)
            {
                return Ok(new { success = true, message = "Profile completed successfully" });
            }

            return BadRequest(new { success = false, error = "Failed to complete profile" });
        }

        private async Task<GoogleUserInfo?> GetGoogleUserInfoAsync(string code)
        {
            var httpClient = new HttpClient();
            
            // Exchange code for access token
            var tokenRequest = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("client_id", _configuration["Authentication:Google:ClientId"] ?? ""),
                new KeyValuePair<string, string>("client_secret", _configuration["Authentication:Google:ClientSecret"] ?? ""),
                new KeyValuePair<string, string>("code", code),
                new KeyValuePair<string, string>("grant_type", "authorization_code"),
                new KeyValuePair<string, string>("redirect_uri", $"{_configuration["AppBaseUrl"] ?? "http://localhost:5035"}/api/auth/external-login/callback")
            });

            var tokenResponse = await httpClient.PostAsync("https://oauth2.googleapis.com/token", tokenRequest);
            var tokenContent = await tokenResponse.Content.ReadAsStringAsync();
            
            if (!tokenResponse.IsSuccessStatusCode)
            {
                return null;
            }

            var tokenData = System.Text.Json.JsonSerializer.Deserialize<GoogleTokenResponse>(tokenContent);
            if (tokenData?.AccessToken == null)
            {
                return null;
            }

            // Get user info using access token
            httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", tokenData.AccessToken);
            var userResponse = await httpClient.GetAsync("https://www.googleapis.com/oauth2/v2/userinfo");
            var userContent = await userResponse.Content.ReadAsStringAsync();

            if (!userResponse.IsSuccessStatusCode)
            {
                return null;
            }

            return System.Text.Json.JsonSerializer.Deserialize<GoogleUserInfo>(userContent);
        }

        private async Task<IActionResult> ProcessGoogleUserAsync(string email, string? firstName, string? lastName, string returnUrl, string frontendUrl = "")
        {
            // Determine the target frontend URL once at the beginning
            var targetFrontendUrl = !string.IsNullOrEmpty(frontendUrl) ? frontendUrl : GetFrontendBaseUrl();
            
            var existingUser = await _userManager.FindByEmailAsync(email);
            if (existingUser != null)
            {
                // Check if profile is complete for existing OAuth users
                var profileStatus = await _authService.CheckProfileCompletionAsync(existingUser.Id);
                var token = ((AuthService)_authService).GenerateJwtToken(existingUser);
                
                string redirectUrl;
                if (!profileStatus.IsCompleted)
                {
                    // Redirect to profile completion if incomplete
                    redirectUrl = $"{targetFrontendUrl}/complete-profile?token={Uri.EscapeDataString(token.Token)}&expiration={Uri.EscapeDataString(token.Expiration.ToString())}";
                }
                else
                {
                    // Normal login flow
                    redirectUrl = $"{targetFrontendUrl}{returnUrl}?token={Uri.EscapeDataString(token.Token)}&expiration={Uri.EscapeDataString(token.Expiration.ToString())}";
                }
                
                return Redirect(redirectUrl);
            }
            else
            {
                // Create new user
                var userName = email.Split('@')[0];
                var newUser = new ApplicationUser(userName, email, firstName ?? "", lastName ?? "");
                
                var createResult = await _userManager.CreateAsync(newUser);
                if (createResult.Succeeded)
                {
                    // Create basic UserProfile for OAuth user (incomplete, needs profile completion)
                    var userProfile = new UserProfile
                    {
                        UserId = newUser.Id,
                        FirstName = firstName,
                        LastName = lastName,
                        Gender = null, // Will be completed later
                        Ethnicity = null, // Will be completed later
                        SubscriptionTier = SubscriptionTier.Basic,
                        Credits = 3,
                        LastCreditReset = DateTime.UtcNow,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    };
                    
                    _context.UserProfiles.Add(userProfile);
                    await _context.SaveChangesAsync();

                    var token = ((AuthService)_authService).GenerateJwtToken(newUser);
                    
                    // TODO: Redirect to profile completion page once frontend is ready
                    // For now, redirect to dashboard but mark profile as incomplete
                    var redirectUrl = $"{targetFrontendUrl}{returnUrl}?token={Uri.EscapeDataString(token.Token)}&expiration={Uri.EscapeDataString(token.Expiration.ToString())}&profileIncomplete=true";
                    
                    return Redirect(redirectUrl);
                }
                else
                {
                    return Redirect($"{targetFrontendUrl}{returnUrl}?error=failed_to_create_user");
                }
            }
        }

        /// <summary>
        /// Get the appropriate frontend base URL based on current request context
        /// </summary>
        private string GetFrontendBaseUrl()
        {
            // Check referer header first (most reliable for OAuth flows)
            var referer = Request.Headers["Referer"].FirstOrDefault();
            if (!string.IsNullOrEmpty(referer))
            {
                try
                {
                    var uri = new Uri(referer);
                    return $"{uri.Scheme}://{uri.Host}{(uri.Port != 80 && uri.Port != 443 ? $":{uri.Port}" : "")}";
                }
                catch
                {
                    // Ignore parsing errors
                }
            }
            
            // Check Origin header
            var origin = Request.Headers["Origin"].FirstOrDefault();
            if (!string.IsNullOrEmpty(origin))
            {
                return origin;
            }
            
            // Default to localhost for development
            return "http://localhost:4200";
        }

    }

    public class GoogleTokenResponse
    {
        public string? access_token { get; set; }
        public string? AccessToken => access_token;
    }

    public class GoogleUserInfo
    {
        public string email { get; set; } = string.Empty;
        public string? given_name { get; set; }
        public string? family_name { get; set; }
        public string Email => email;
        public string? GivenName => given_name;
        public string? FamilyName => family_name;
    }
}
