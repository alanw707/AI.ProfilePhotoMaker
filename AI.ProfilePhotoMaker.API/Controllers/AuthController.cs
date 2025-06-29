using System.Security.Claims;
using AI.ProfilePhotoMaker.API.Data;
using AI.ProfilePhotoMaker.API.Models;
using AI.ProfilePhotoMaker.API.Models.DTOs;
using AI.ProfilePhotoMaker.API.Services.Authentication;
using AI.ProfilePhotoMaker.API.Services.Authentication.interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

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
        public IActionResult ExternalLogin(string provider, string returnUrl = "")
        {
            // Use AppBaseUrl from configuration
            var baseUrl = _configuration["AppBaseUrl"] ?? "http://localhost:5035";
            
            var redirectUrl = $"{baseUrl}/api/auth/external-login/callback?returnUrl={returnUrl}";
            var properties = new Microsoft.AspNetCore.Authentication.AuthenticationProperties 
            { 
                RedirectUri = redirectUrl 
            };
            
            return Challenge(properties, provider);
        }

        [HttpGet("external-login/callback")]
        public async Task<IActionResult> ExternalLoginCallback(string returnUrl = "", string code = "", string state = "")
        {
            // Debug logging
            Console.WriteLine($"OAuth Callback - State: {state}, Code: {code}, ReturnUrl: {returnUrl}");
            
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
                        Console.WriteLine($"Successfully retrieved Google user info: {userInfo.Email}");
                        return await ProcessGoogleUserAsync(userInfo.Email, userInfo.GivenName, userInfo.FamilyName, returnUrl);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Direct Google OAuth failed: {ex.Message}");
                }
            }
            
            // Fallback to standard ASP.NET Core OAuth flow
            var info = await _signInManager.GetExternalLoginInfoAsync();
            if (info == null)
            {
                Console.WriteLine("GetExternalLoginInfoAsync returned null");
                return Redirect($"http://localhost:4200{returnUrl}?error=external_login_failed");
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
                    return Redirect($"http://localhost:4200{returnUrl}?token={token.Token}&expiration={token.Expiration}");
                }
            }

            // Create new user or link account
            var email = info.Principal.FindFirstValue(ClaimTypes.Email);
            var firstName = info.Principal.FindFirstValue(ClaimTypes.GivenName) ?? "";
            var lastName = info.Principal.FindFirstValue(ClaimTypes.Surname) ?? "";

            if (string.IsNullOrEmpty(email))
            {
                return Redirect($"http://localhost:4200{returnUrl}?error=no_email_from_provider");
            }

            var existingUser = await _userManager.FindByEmailAsync(email);
            if (existingUser != null)
            {
                Console.WriteLine($"Found existing user with email: {email}");
                // Link external login to existing user
                var addLoginResult = await _userManager.AddLoginAsync(existingUser, info);
                if (addLoginResult.Succeeded)
                {
                    Console.WriteLine("Successfully linked Google account to existing user");
                    var token = ((AuthService)_authService).GenerateJwtToken(existingUser);
                    return Redirect($"http://localhost:4200{returnUrl}?token={token.Token}&expiration={token.Expiration}");
                }
                else
                {
                    Console.WriteLine($"Failed to link Google account: {string.Join(", ", addLoginResult.Errors.Select(e => e.Description))}");
                    return Redirect($"http://localhost:4200{returnUrl}?error=failed_to_link_account");
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
                        return Redirect($"http://localhost:4200{returnUrl}?token={token.Token}&expiration={token.Expiration}");
                    }
                }
            }

            return Redirect($"http://localhost:4200{returnUrl}?error=external_login_failed");
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

        [HttpGet("google-direct-callback")]
        public async Task<IActionResult> GoogleDirectCallback(string? code = null, string returnUrl = "/dashboard")
        {
            Console.WriteLine($"Google Direct Callback - Code: {code}, ReturnUrl: {returnUrl}");
            Console.WriteLine($"All query parameters: {string.Join(", ", HttpContext.Request.Query.Select(q => $"{q.Key}={q.Value}"))}");
            
            // Try to get code from query parameters if not passed as parameter
            if (string.IsNullOrEmpty(code))
            {
                code = HttpContext.Request.Query["code"].ToString();
            }
            
            if (string.IsNullOrEmpty(code))
            {
                Console.WriteLine("No authorization code found in request");
                return Redirect($"http://localhost:4200/login?error=no_authorization_code");
            }

            try
            {
                var userInfo = await GetGoogleUserInfoAsync(code);
                if (userInfo != null)
                {
                    Console.WriteLine($"Successfully retrieved Google user info: {userInfo.Email}");
                    return await ProcessGoogleUserAsync(userInfo.Email, userInfo.GivenName, userInfo.FamilyName, returnUrl);
                }
                else
                {
                    Console.WriteLine("Failed to retrieve user info from Google");
                    return Redirect($"http://localhost:4200{returnUrl}?error=failed_to_get_user_info");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Google Direct OAuth failed: {ex.Message}");
                return Redirect($"http://localhost:4200{returnUrl}?error=oauth_processing_failed");
            }
        }

        [HttpGet("google-ticket-callback")]
        public async Task<IActionResult> GoogleTicketCallback(string email, string? firstName = null, string? lastName = null, string returnUrl = "/dashboard")
        {
            Console.WriteLine($"Google Ticket Callback - Email: {email}, Name: {firstName} {lastName}, ReturnUrl: {returnUrl}");
            
            if (string.IsNullOrEmpty(email))
            {
                Console.WriteLine("No email provided in ticket callback");
                return Redirect($"http://localhost:4200/login?error=no_email_in_ticket");
            }

            try
            {
                Console.WriteLine($"Processing user from OAuth ticket: {email}");
                return await ProcessGoogleUserAsync(email, firstName, lastName, returnUrl);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Google Ticket processing failed: {ex.Message}");
                return Redirect($"http://localhost:4200{returnUrl}?error=ticket_processing_failed");
            }
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
                Console.WriteLine($"Token exchange failed: {tokenContent}");
                return null;
            }

            var tokenData = System.Text.Json.JsonSerializer.Deserialize<GoogleTokenResponse>(tokenContent);
            if (tokenData?.AccessToken == null)
            {
                Console.WriteLine("No access token received");
                return null;
            }

            // Get user info using access token
            httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", tokenData.AccessToken);
            var userResponse = await httpClient.GetAsync("https://www.googleapis.com/oauth2/v2/userinfo");
            var userContent = await userResponse.Content.ReadAsStringAsync();

            if (!userResponse.IsSuccessStatusCode)
            {
                Console.WriteLine($"User info request failed: {userContent}");
                return null;
            }

            return System.Text.Json.JsonSerializer.Deserialize<GoogleUserInfo>(userContent);
        }

        private async Task<IActionResult> ProcessGoogleUserAsync(string email, string? firstName, string? lastName, string returnUrl)
        {
            Console.WriteLine($"ProcessGoogleUserAsync called with email: {email}, returnUrl: {returnUrl}");
            
            var existingUser = await _userManager.FindByEmailAsync(email);
            if (existingUser != null)
            {
                Console.WriteLine($"Found existing user with email: {email}");
                
                // Check if profile is complete for existing OAuth users
                var profileStatus = await _authService.CheckProfileCompletionAsync(existingUser.Id);
                var token = ((AuthService)_authService).GenerateJwtToken(existingUser);
                Console.WriteLine($"Generated JWT token, length: {token.Token.Length}");
                
                string redirectUrl;
                if (!profileStatus.IsCompleted)
                {
                    // Redirect to profile completion if incomplete
                    redirectUrl = $"http://localhost:4200/complete-profile?token={Uri.EscapeDataString(token.Token)}&expiration={Uri.EscapeDataString(token.Expiration.ToString())}";
                    Console.WriteLine($"Redirecting to profile completion: {redirectUrl}");
                }
                else
                {
                    // Normal login flow
                    redirectUrl = $"http://localhost:4200{returnUrl}?token={Uri.EscapeDataString(token.Token)}&expiration={Uri.EscapeDataString(token.Expiration.ToString())}";
                    Console.WriteLine($"Redirecting to: {redirectUrl}");
                }
                
                return Redirect(redirectUrl);
            }
            else
            {
                Console.WriteLine($"Creating new user with email: {email}");
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
                    Console.WriteLine($"Generated JWT token for new user, length: {token.Token.Length}");
                    
                    // TODO: Redirect to profile completion page once frontend is ready
                    // For now, redirect to dashboard but mark profile as incomplete
                    var redirectUrl = $"http://localhost:4200{returnUrl}?token={Uri.EscapeDataString(token.Token)}&expiration={Uri.EscapeDataString(token.Expiration.ToString())}&profileIncomplete=true";
                    Console.WriteLine($"Redirecting new OAuth user to dashboard: {redirectUrl}");
                    
                    return Redirect(redirectUrl);
                }
                else
                {
                    Console.WriteLine($"Failed to create user: {string.Join(", ", createResult.Errors.Select(e => e.Description))}");
                    return Redirect($"http://localhost:4200{returnUrl}?error=failed_to_create_user");
                }
            }
        }
    }

    public class GoogleTokenResponse
    {
        public string? access_token { get; set; }
        public string? AccessToken => access_token;
    }

    public class GoogleUserInfo
    {
        public string? email { get; set; }
        public string? given_name { get; set; }
        public string? family_name { get; set; }
        public string? Email => email;
        public string? GivenName => given_name;
        public string? FamilyName => family_name;
    }
}
