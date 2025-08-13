using System.Security.Claims;
using System.Text;
using System.Text.Json;
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
        private readonly HttpClient _httpClient;

        public AuthController(
            IAuthService authService,
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            IConfiguration configuration,
            ApplicationDbContext context,
            HttpClient httpClient)
        {
            _authService = authService;
            _userManager = userManager;
            _signInManager = signInManager;
            _configuration = configuration;
            _context = context;
            _httpClient = httpClient;
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

        [HttpGet("google-oauth-url")]
        public IActionResult GetGoogleOAuthUrl(string returnUrl = "/app/dashboard")
        {
            var (clientId, _) = GetGoogleClientSettings();
            if (string.IsNullOrEmpty(clientId))
            {
                return BadRequest(new { error = "Google OAuth is not configured" });
            }

            var backendBaseUrl = ResolveBackendBaseUrl();
            var redirectUri = $"{backendBaseUrl}/api/auth/external-login-callback";

            // Generate state parameter for security
            var state = Guid.NewGuid().ToString();
            
            try
            {
                HttpContext.Session.SetString("oauth_state", state);
                HttpContext.Session.SetString("oauth_return_url", returnUrl);
            }
            catch (Exception ex)
            {
                // Log session error but continue - some environments may not have session properly initialized
                Console.WriteLine($"WARNING: Failed to set session values: {ex.Message}");
                // In this case, we'll still generate the OAuth URL but state validation will fail at callback
                // This is acceptable for direct API calls where session isn't available
            }

            // Manually construct the Google OAuth URL
            var authUrl = $"https://accounts.google.com/o/oauth2/v2/auth?" +
                $"client_id={Uri.EscapeDataString(clientId)}&" +
                $"redirect_uri={Uri.EscapeDataString(redirectUri)}&" +
                $"response_type=code&" +
                $"scope={Uri.EscapeDataString("openid profile email")}&" +
                $"state={Uri.EscapeDataString(state)}";

            return Ok(new { authUrl });
        }

        [HttpGet("external-login/{provider}")]
        public IActionResult ExternalLogin(string provider, string returnUrl = "/app/dashboard")
        {
            if (provider.ToLower() != "google")
            {
                return BadRequest(new { error = $"{provider} OAuth not implemented yet" });
            }

            var (clientId, _) = GetGoogleClientSettings();
            if (string.IsNullOrEmpty(clientId))
            {
                return BadRequest(new { error = "Google OAuth is not configured" });
            }

            // Generate state parameter for security
            var state = Guid.NewGuid().ToString();
            
            try
            {
                HttpContext.Session.SetString("oauth_state", state);
                HttpContext.Session.SetString("oauth_return_url", returnUrl);
            }
            catch (Exception ex)
            {
                // Log session error but continue - some environments may not have session properly initialized
                Console.WriteLine($"WARNING: Failed to set session values: {ex.Message}");
                // In this case, we'll still generate the OAuth URL but state validation will fail at callback
                // This is acceptable for direct API calls where session isn't available
            }

            var backendBaseUrl = ResolveBackendBaseUrl();
            var redirectUri = $"{backendBaseUrl}/api/auth/external-login-callback";

            // Construct Google OAuth URL manually
            var authUrl = $"https://accounts.google.com/o/oauth2/v2/auth?" +
                $"client_id={Uri.EscapeDataString(clientId ?? string.Empty)}&" +
                $"redirect_uri={Uri.EscapeDataString(redirectUri ?? string.Empty)}&" +
                $"response_type=code&" +
                $"scope={Uri.EscapeDataString("openid profile email")}&" +
                $"state={Uri.EscapeDataString(state ?? string.Empty)}";

            return Redirect(authUrl);
        }

        [HttpGet("external-login-callback")]
        public async Task<IActionResult> ExternalLoginCallback(string? code = null, string? state = null, string? error = null)
        {
            var frontendBaseUrl = _configuration["AppBaseUrl"] ?? "http://localhost:4200";
            
            string? returnUrl = null;
            string? sessionState = null;
            
            try
            {
                returnUrl = HttpContext.Session.GetString("oauth_return_url") ?? "/app/dashboard";
                sessionState = HttpContext.Session.GetString("oauth_state");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"WARNING: Failed to retrieve session values: {ex.Message}");
                returnUrl = "/app/dashboard"; // Default fallback
                sessionState = null; // Will trigger session_expired error below
            }

            // Handle OAuth errors
            if (!string.IsNullOrEmpty(error))
            {
                return Redirect($"{frontendBaseUrl}/auth/login?error=oauth_{error}");
            }

            // Validate state parameter - CRITICAL SECURITY CHECK
            // sessionState is already retrieved above with error handling
            
            // SECURITY: Always validate state parameter to prevent Login CSRF attacks
            // If session state is missing, this is a security issue - reject the request
            if (string.IsNullOrEmpty(sessionState))
            {
                return Redirect($"{frontendBaseUrl}/auth/login?error=session_expired");
            }
            
            // If state parameter is missing from callback, this is suspicious - reject
            if (string.IsNullOrEmpty(state))
            {
                return Redirect($"{frontendBaseUrl}/auth/login?error=missing_state");
            }
            
            // Compare state values - must match exactly
            if (state != sessionState)
            {
                return Redirect($"{frontendBaseUrl}/auth/login?error=invalid_state");
            }

            // Validate authorization code
            if (string.IsNullOrEmpty(code))
            {
                return Redirect($"{frontendBaseUrl}/auth/login?error=missing_code");
            }

            try
            {
                // Exchange authorization code for access token
                var tokenResponse = await ExchangeCodeForTokenAsync(code);
                if (tokenResponse == null)
                {
                    return Redirect($"{frontendBaseUrl}/auth/login?error=token_exchange_failed");
                }

                // Get user info from Google
                var userInfo = await GetGoogleUserInfoAsync(tokenResponse.AccessToken);
                if (userInfo == null)
                {
                    return Redirect($"{frontendBaseUrl}/auth/login?error=user_info_failed");
                }

                // Find or create user
                var user = await FindOrCreateUserAsync(userInfo);
                if (user == null)
                {
                    return Redirect($"{frontendBaseUrl}/auth/login?error=user_creation_failed");
                }

                // Generate JWT token
                var tokenInfo = _authService.GenerateJwtToken(user);

                return Redirect($"{frontendBaseUrl}{returnUrl}?token={tokenInfo.Token}");
            }
            catch (Exception ex)
            {
                return Redirect($"{frontendBaseUrl}/auth/login?error=oauth_processing_failed");
            }
        }

        private async Task<GoogleTokenResponse?> ExchangeCodeForTokenAsync(string code)
        {
            var (clientId, clientSecret) = GetGoogleClientSettings();
            var backendBaseUrl = ResolveBackendBaseUrl();
            var redirectUri = $"{backendBaseUrl}/api/auth/external-login-callback";

            var tokenRequest = new List<KeyValuePair<string, string>>
            {
                new("client_id", clientId),
                new("client_secret", clientSecret),
                new("code", code),
                new("grant_type", "authorization_code"),
                new("redirect_uri", redirectUri)
            };

            var content = new FormUrlEncodedContent(tokenRequest);
            var response = await _httpClient.PostAsync("https://oauth2.googleapis.com/token", content);

            if (!response.IsSuccessStatusCode)
            {
                // Enhanced error logging for token exchange failures
                var errorContent = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"Token exchange failed: {response.StatusCode} - {errorContent}");
                return null;
            }

            var json = await response.Content.ReadAsStringAsync();
            var tokenResponse = JsonSerializer.Deserialize<GoogleTokenResponse>(json, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
            });

            return tokenResponse;
        }

        private (string clientId, string clientSecret) GetGoogleClientSettings()
        {
            try
            {
                // Use the same Google OAuth options that the middleware uses
                var optionsMonitor = HttpContext.RequestServices.GetRequiredService<Microsoft.Extensions.Options.IOptionsMonitor<Microsoft.AspNetCore.Authentication.Google.GoogleOptions>>();
                var googleOptions = optionsMonitor.Get("Google");

                if (googleOptions != null && !string.IsNullOrEmpty(googleOptions.ClientId) && !string.IsNullOrEmpty(googleOptions.ClientSecret))
                {
                    return (googleOptions.ClientId, googleOptions.ClientSecret);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ERROR: Failed to get Google OAuth options from middleware: {ex.Message}");
            }

            // Fallback to manual configuration reading
            string? cfgId = _configuration["Authentication:Google:ClientId"];
            string? cfgSecret = _configuration["Authentication:Google:ClientSecret"];
            string? envId = Environment.GetEnvironmentVariable("GOOGLE_CLIENT_ID") ?? _configuration["GOOGLE_CLIENT_ID"];
            string? envSecret = Environment.GetEnvironmentVariable("GOOGLE_CLIENT_SECRET") ?? _configuration["GOOGLE_CLIENT_SECRET"];

            bool IsPlaceholder(string? v) =>
                !string.IsNullOrWhiteSpace(v) && (
                    v.StartsWith("REPLACE_WITH_", StringComparison.OrdinalIgnoreCase) ||
                    v.Contains("STORED_IN_USER_SECRETS", StringComparison.OrdinalIgnoreCase) ||
                    v.StartsWith("YOUR_", StringComparison.OrdinalIgnoreCase) ||
                    v.StartsWith("your_", StringComparison.OrdinalIgnoreCase)
                );

            var clientId = !string.IsNullOrWhiteSpace(envId) ? envId : (IsPlaceholder(cfgId) ? null : cfgId);
            var clientSecret = !string.IsNullOrWhiteSpace(envSecret) ? envSecret : (IsPlaceholder(cfgSecret) ? null : cfgSecret);

            return (clientId ?? string.Empty, clientSecret ?? string.Empty);
        }

        private string ResolveBackendBaseUrl()
        {
            // First check if we're in Azure production environment
            var azureWebsiteName = Environment.GetEnvironmentVariable("WEBSITE_SITE_NAME");
            if (!string.IsNullOrEmpty(azureWebsiteName))
            {
                // We're definitely in Azure - use the production OAuth base URL
                var productionOAuthBase = _configuration["Authentication:OAuth:BaseUrl"];
                if (!string.IsNullOrWhiteSpace(productionOAuthBase))
                {
                    return productionOAuthBase;
                }
            }

            // If forwarded headers are present, prefer them
            var forwardedProto = Request.Headers["X-Forwarded-Proto"].FirstOrDefault();
            var forwardedHost = Request.Headers["X-Forwarded-Host"].FirstOrDefault();
            if (!string.IsNullOrEmpty(forwardedProto) && !string.IsNullOrEmpty(forwardedHost))
            {
                return $"{forwardedProto}://{forwardedHost}";
            }

            // Prefer explicit env override
            var envBase = Environment.GetEnvironmentVariable("OAUTH_BASE_URL") ?? _configuration["OAUTH_BASE_URL"];
            if (!string.IsNullOrWhiteSpace(envBase))
            {
                return envBase;
            }

            // Check if we're clearly in development (localhost)
            var isLocal = Request.Host.Host.Contains("localhost") || Request.Host.Host.Contains("127.0.0.1");
            if (isLocal)
            {
                // Use current request URL for local development
                return $"{Request.Scheme}://{Request.Host.Value}";
            }

            // For any other environment, try to use the configured OAuth base URL
            var cfgBase = _configuration["Authentication:OAuth:BaseUrl"];
            if (!string.IsNullOrWhiteSpace(cfgBase))
            {
                return cfgBase;
            }

            // Last resort - use current request
            return $"{Request.Scheme}://{Request.Host.Value}";
        }

        private async Task<GoogleUserInfo?> GetGoogleUserInfoAsync(string accessToken)
        {
            _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

            var response = await _httpClient.GetAsync("https://www.googleapis.com/oauth2/v2/userinfo");

            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var json = await response.Content.ReadAsStringAsync();
            var userInfo = JsonSerializer.Deserialize<GoogleUserInfo>(json, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
            });

            return userInfo;
        }

        private async Task<ApplicationUser?> FindOrCreateUserAsync(GoogleUserInfo userInfo)
        {
            var user = await _userManager.FindByEmailAsync(userInfo.Email);

            if (user == null)
            {
                user = new ApplicationUser
                {
                    UserName = userInfo.Email,
                    Email = userInfo.Email,
                    FirstName = userInfo.GivenName ?? "",
                    LastName = userInfo.FamilyName ?? "",
                    EmailConfirmed = true
                };

                var createResult = await _userManager.CreateAsync(user);
                if (!createResult.Succeeded)
                {
                    return null;
                }

                try
                {
                    // CRITICAL FIX: Create UserProfile for new OAuth users
                    var userProfile = new UserProfile
                    {
                        UserId = user.Id,
                        FirstName = userInfo.GivenName ?? "",
                        LastName = userInfo.FamilyName ?? "",
                        Gender = null,  // Will be set during profile completion if needed
                        Ethnicity = null,  // Will be set during profile completion if needed
                        SubscriptionTier = SubscriptionTier.Basic,
                        Credits = 3,
                        LastCreditReset = DateTime.UtcNow,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    };

                    _context.UserProfiles.Add(userProfile);
                    await _context.SaveChangesAsync();
                }
                catch (Exception ex)
                {
                    // Log error appropriately in production (consider using ILogger)
                    throw; // Re-throw to handle at higher level
                }
            }
            else
            {
                // CRITICAL FIX: Check if existing user has a profile (for migration cases)
                var hasProfile = await _context.UserProfiles.AnyAsync(p => p.UserId == user.Id);
                
                if (!hasProfile)
                {
                    try
                    {
                        // Create profile for existing user who doesn't have one
                        var userProfile = new UserProfile
                        {
                            UserId = user.Id,
                            FirstName = user.FirstName ?? userInfo.GivenName ?? "",
                            LastName = user.LastName ?? userInfo.FamilyName ?? "",
                            Gender = null,
                            Ethnicity = null,
                            SubscriptionTier = SubscriptionTier.Basic,
                            Credits = 3,
                            LastCreditReset = DateTime.UtcNow,
                            CreatedAt = DateTime.UtcNow,
                            UpdatedAt = DateTime.UtcNow
                        };

                        _context.UserProfiles.Add(userProfile);
                        await _context.SaveChangesAsync();
                    }
                    catch (Exception ex)
                    {
                        // Log error appropriately in production (consider using ILogger)
                        throw; // Re-throw to handle at higher level
                    }
                }
                // Profile already exists, no action needed
            }

            return user;
        }

        [HttpGet("profile-completion-status")]
        [Authorize]
        public async Task<IActionResult> GetProfileCompletionStatus()
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized();
            }

            var status = await _authService.CheckProfileCompletionAsync(userId);
            return Ok(status);
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
                return Unauthorized();
            }

            var success = await _authService.CompleteProfileAsync(userId, model);
            if (success)
            {
                return Ok(new { success = true, message = "Profile completed successfully" });
            }

            return BadRequest(new { success = false, error = "Failed to complete profile" });
        }

        [HttpGet("debug/auth-schemes")]
        public IActionResult GetAuthSchemes()
        {
            var schemes = new List<object>();

            // Get all registered authentication schemes
            var authSchemeProvider = HttpContext.RequestServices.GetRequiredService<Microsoft.AspNetCore.Authentication.IAuthenticationSchemeProvider>();
            var allSchemes = authSchemeProvider.GetAllSchemesAsync().Result;

            foreach (var scheme in allSchemes)
            {
                schemes.Add(new
                {
                    Name = scheme.Name,
                    DisplayName = scheme.DisplayName,
                    HandlerType = scheme.HandlerType?.Name
                });
            }

            return Ok(new
            {
                message = "Available authentication schemes",
                schemes = schemes,
                timestamp = DateTime.UtcNow
            });
        }

        [HttpGet("debug/google-oauth")]
        public async Task<IActionResult> DebugGoogleOAuth()
        {
            try
            {
                // Test if we can create an authorization URL manually
                var authService = HttpContext.RequestServices.GetRequiredService<Microsoft.AspNetCore.Authentication.IAuthenticationService>();
                var schemeProvider = HttpContext.RequestServices.GetRequiredService<Microsoft.AspNetCore.Authentication.IAuthenticationSchemeProvider>();

                var googleScheme = await schemeProvider.GetSchemeAsync("Google");

                if (googleScheme == null)
                {
                    return Ok(new { error = "Google authentication scheme not found" });
                }

                // Try to get the Google OAuth options
                var optionsMonitor = HttpContext.RequestServices.GetRequiredService<Microsoft.Extensions.Options.IOptionsMonitor<Microsoft.AspNetCore.Authentication.Google.GoogleOptions>>();
                var googleOptions = optionsMonitor.Get("Google");

                // Create a manual Google OAuth URL
                var state = Guid.NewGuid().ToString();
                var redirectUri = $"https://{Request.Host}/signin-google";
                var authUrl = $"{googleOptions.AuthorizationEndpoint}?" +
                    $"client_id={Uri.EscapeDataString(googleOptions.ClientId ?? string.Empty)}&" +
                    $"redirect_uri={Uri.EscapeDataString(redirectUri ?? string.Empty)}&" +
                    $"response_type=code&" +
                    $"scope={Uri.EscapeDataString("openid profile email")}&" +
                    $"state={Uri.EscapeDataString(state ?? string.Empty)}";

                return Ok(new
                {
                    message = "Google OAuth Debug Info",
                    scheme = new
                    {
                        name = googleScheme.Name,
                        displayName = googleScheme.DisplayName,
                        handlerType = googleScheme.HandlerType?.Name
                    },
                    options = new
                    {
                        clientId = !string.IsNullOrEmpty(googleOptions.ClientId) ? googleOptions.ClientId.Substring(0, 20) + "..." : "NOT SET",
                        clientSecret = !string.IsNullOrEmpty(googleOptions.ClientSecret) ? "SET" : "NOT SET",
                        callbackPath = googleOptions.CallbackPath.ToString(),
                        authorizationEndpoint = googleOptions.AuthorizationEndpoint,
                        tokenEndpoint = googleOptions.TokenEndpoint
                    },
                    manualAuthUrl = authUrl,
                    timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                return Ok(new
                {
                    error = "Exception in Google OAuth debug",
                    message = ex.Message,
                    stackTrace = ex.StackTrace
                });
            }
        }

        [HttpGet("test-redirect")]
        public IActionResult TestRedirect()
        {
            // Simple test to see if redirects work at all
            return Redirect("https://www.google.com");
        }

        [HttpGet("debug/oauth-config")]
        public IActionResult DebugOAuthConfig()
        {
            try
            {
                var (clientId, clientSecret) = GetGoogleClientSettings();
                
                string SafeSubstring(string? value, int maxLength = 20)
                {
                    if (string.IsNullOrEmpty(value)) return "NULL/EMPTY";
                    return value.Substring(0, Math.Min(maxLength, value.Length)) + "...";
                }

                return Ok(new
                {
                    message = "OAuth Configuration Debug",
                    configValues = new
                    {
                        authGoogleClientId = SafeSubstring(_configuration["Authentication:Google:ClientId"]),
                        authGoogleClientSecret = !string.IsNullOrEmpty(_configuration["Authentication:Google:ClientSecret"]) ? "SET" : "NULL",
                        envGoogleClientId = SafeSubstring(Environment.GetEnvironmentVariable("GOOGLE_CLIENT_ID")),
                        envGoogleClientSecret = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("GOOGLE_CLIENT_SECRET")) ? "SET" : "NULL",
                        configGoogleClientId = SafeSubstring(_configuration["GOOGLE_CLIENT_ID"]),
                        configGoogleClientSecret = !string.IsNullOrEmpty(_configuration["GOOGLE_CLIENT_SECRET"]) ? "SET" : "NULL"
                    },
                    finalValues = new
                    {
                        clientId = SafeSubstring(clientId),
                        clientSecret = !string.IsNullOrEmpty(clientSecret) ? "SET" : "NULL/EMPTY"
                    },
                    timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                return Ok(new
                {
                    error = "Failed to debug OAuth configuration",
                    message = ex.Message,
                    timestamp = DateTime.UtcNow
                });
            }
        }

        // Alternative OAuth method removed - contained wrong client ID



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

    // DTOs for Google OAuth
    public class GoogleTokenResponse
    {
        public string AccessToken { get; set; } = "";
        public string TokenType { get; set; } = "";
        public int ExpiresIn { get; set; }
        public string? RefreshToken { get; set; }
        public string? IdToken { get; set; }
    }

    public class GoogleUserInfo
    {
        public string Id { get; set; } = "";
        public string Email { get; set; } = "";
        public bool VerifiedEmail { get; set; }
        public string Name { get; set; } = "";
        public string GivenName { get; set; } = "";
        public string FamilyName { get; set; } = "";
        public string Picture { get; set; } = "";
        public string Locale { get; set; } = "";
    }
}