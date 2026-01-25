using System.Security.Claims;
using System.Text;
using System.Text.Json;
using AI.ProfilePhotoMaker.API.Constants;
using AI.ProfilePhotoMaker.API.Data;
using AI.ProfilePhotoMaker.API.Infrastructure.Logging;
using AI.ProfilePhotoMaker.API.Models;
using AI.ProfilePhotoMaker.API.Models.DTOs;
using AI.ProfilePhotoMaker.API.Services.Authentication;
using AI.ProfilePhotoMaker.API.Services.Authentication.interfaces;
using AI.ProfilePhotoMaker.API.Services.Notifications;
using AI.ProfilePhotoMaker.API.Services.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Caching.Memory;
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
        private readonly IWebHostEnvironment _environment;
        private readonly ILogger<AuthController> _logger;
        private readonly ITurnstileVerificationService _turnstile;
        private readonly IEmailNotificationService _emailNotificationService;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IMemoryCache _cache;
        private static string S(string? value) => LoggingSanitizer.Sanitize(value);
        private static string Sid(string? value) => LoggingSanitizer.SanitizeId(value);
        private static string LogSafe(string? value, int maxLength = 128)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            // Prevent log forging by stripping line breaks and control whitespace.
            var cleaned = value
                .ReplaceLineEndings(" ")
                .Replace('\t', ' ')
                .Trim();

            return cleaned.Length <= maxLength ? cleaned : cleaned[..maxLength];
        }

        private static string HostForLog(string? url)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                return string.Empty;
            }

            return Uri.TryCreate(url, UriKind.Absolute, out var parsed)
                ? parsed.Host
                : LogSafe(url, 64);
        }

        public AuthController(
            IAuthService authService,
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            IConfiguration configuration,
            ApplicationDbContext context,
            HttpClient httpClient,
            IWebHostEnvironment environment,
            ILogger<AuthController> logger,
            ITurnstileVerificationService turnstile,
            IEmailNotificationService emailNotificationService,
            IServiceScopeFactory scopeFactory,
            IMemoryCache cache)
        {
            _authService = authService;
            _userManager = userManager;
            _signInManager = signInManager;
            _configuration = configuration;
            _context = context;
            _httpClient = httpClient;
            _environment = environment;
            _logger = logger;
            _turnstile = turnstile;
            _emailNotificationService = emailNotificationService;
            _scopeFactory = scopeFactory;
            _cache = cache;
        }

        private void QueueSignupEmails(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
            {
                return;
            }

            _ = Task.Run(async () =>
            {
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
                    var emailService = scope.ServiceProvider.GetRequiredService<IEmailNotificationService>();

                    var user = await userManager.FindByEmailAsync(email);
                    if (user == null || string.IsNullOrWhiteSpace(user.Email))
                    {
                        return;
                    }

                    await emailService.SendWelcomeAsync(user.Id, user.Email, user.FirstName);

                    if (!user.EmailConfirmed)
                    {
                        var token = await userManager.GenerateEmailConfirmationTokenAsync(user);
                        var encodedToken = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));
                        await emailService.SendEmailVerificationAsync(user.Id, user.Email, encodedToken);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to send signup emails for {Email}", Sid(email));
                }
            });
        }

        private void QueueWelcomeEmail(string userId)
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                return;
            }

            _ = Task.Run(async () =>
            {
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
                    var emailService = scope.ServiceProvider.GetRequiredService<IEmailNotificationService>();

                    var user = await userManager.FindByIdAsync(userId);
                    if (user == null || string.IsNullOrWhiteSpace(user.Email))
                    {
                        return;
                    }

                    await emailService.SendWelcomeAsync(user.Id, user.Email, user.FirstName);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to send welcome email for user {UserId}", Sid(userId));
                }
            });
        }

        private CookieOptions BuildAuthCookieOptions(DateTimeOffset? expires = null)
        {
            // Only set an explicit Domain when we are on our real domain; this prevents invalid cookies in tests/local.
            var requestHost = Request?.Host.Host;
            var configuredDomain = _configuration["Authentication:CookieDomain"];
            string? domain = null;
            if (!string.IsNullOrWhiteSpace(configuredDomain))
            {
                domain = configuredDomain;
            }
            else if (!string.IsNullOrWhiteSpace(requestHost) &&
                     requestHost.EndsWith("aiprofilephotomaker.com", StringComparison.OrdinalIgnoreCase))
            {
                domain = ".aiprofilephotomaker.com";
            }

            var isHttps = Request?.IsHttps ?? !_environment.IsDevelopment();

            return new CookieOptions
            {
                HttpOnly = true,
                Secure = isHttps,
                SameSite = isHttps ? SameSiteMode.None : SameSiteMode.Lax,
                Domain = domain,
                Expires = expires,
                Path = "/"
            };
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterDto model)
        {
            if (!ModelState.IsValid)
            {
                if (ModelState.TryGetValue(nameof(RegisterDto.AgeConfirmed), out var ageConfirmation) &&
                    ageConfirmation.Errors.Count > 0)
                {
                    return BadRequest(new AuthResponseDto(false, "Age confirmation is required to create an account.", string.Empty, null));
                }

                return BadRequest(ModelState);
            }

            var turnstileOk = await _turnstile.VerifyAsync(model.TurnstileToken, HttpContext?.Connection?.RemoteIpAddress?.ToString());
            if (!turnstileOk)
            {
                return BadRequest(new AuthResponseDto(false, "Bot verification failed. Please try again.", string.Empty, null));
            }

            var result = await _authService.RegisterAsync(model);
            if (!result.IsSuccess)
            {
                return BadRequest(result);
            }

            // Fire-and-forget emails (welcome + verification) so signup stays fast and resilient.
            QueueSignupEmails(model.Email);

            // Set secure JWT cookie for session
            var cookieName = _configuration["Authentication:TokenCookieName"] ?? "AuthToken";
            var cookieOptions = BuildAuthCookieOptions(DateTimeOffset.UtcNow.AddHours(12));
            if (!string.IsNullOrEmpty(result.Token))
            {
                Response.Cookies.Append(cookieName, result.Token, cookieOptions);
            }

            return Ok(result);
        }

        [HttpGet("account-status")]
        [Authorize]
        public async Task<IActionResult> GetAccountStatus()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Unauthorized(new { success = false, error = new { code = "Unauthorized", message = "User not authenticated." } });
            }

            return Ok(new
            {
                success = true,
                data = new { emailConfirmed = user.EmailConfirmed },
                error = (object?)null
            });
        }

        [HttpPost("resend-confirmation-email")]
        [Authorize]
        public async Task<IActionResult> ResendConfirmationEmail()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Unauthorized(new { success = false, error = new { code = "Unauthorized", message = "User not authenticated." } });
            }

            if (user.EmailConfirmed)
            {
                return Ok(new { success = true, data = new { emailConfirmed = true }, error = (object?)null });
            }

            if (string.IsNullOrWhiteSpace(user.Email))
            {
                return BadRequest(new { success = false, error = new { code = "MissingEmail", message = "User email address is missing." } });
            }

            var throttleKey = $"email-confirm-resend:{user.Id}";
            if (_cache.TryGetValue(throttleKey, out _))
            {
                return StatusCode(429, new
                {
                    success = false,
                    error = new
                    {
                        code = "TooManyRequests",
                        message = "Please wait a moment before requesting another verification email."
                    }
                });
            }

            try
            {
                var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);
                var encodedToken = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));
                await _emailNotificationService.SendEmailVerificationAsync(user.Id, user.Email, encodedToken);
                _cache.Set(throttleKey, true, TimeSpan.FromMinutes(1));
                return Ok(new { success = true, data = new { emailConfirmed = false }, error = (object?)null });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to resend email verification link for user {UserId}", Sid(user.Id));
                return StatusCode(500, new { success = false, error = new { code = "EmailSendFailed", message = "Failed to send verification email. Please try again later." } });
            }
        }

        [HttpPost("confirm-email")]
        [AllowAnonymous]
        public async Task<IActionResult> ConfirmEmail([FromBody] ConfirmEmailRequest payload)
        {
            if (payload == null || string.IsNullOrWhiteSpace(payload.UserId) || string.IsNullOrWhiteSpace(payload.Token))
            {
                return BadRequest(new { success = false, error = new { code = "InvalidRequest", message = "Missing userId or token." } });
            }

            var user = await _userManager.FindByIdAsync(payload.UserId);
            if (user == null)
            {
                return BadRequest(new { success = false, error = new { code = "UserNotFound", message = "User not found." } });
            }

            string decodedToken;
            try
            {
                decodedToken = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(payload.Token));
            }
            catch
            {
                // Backward compatibility: token might already be raw/unencoded
                decodedToken = payload.Token;
            }

            var confirmResult = await _userManager.ConfirmEmailAsync(user, decodedToken);
            if (!confirmResult.Succeeded)
            {
                var message = string.Join("; ", confirmResult.Errors.Select(e => e.Description));
                return BadRequest(new
                {
                    success = false,
                    error = new
                    {
                        code = "ConfirmEmailFailed",
                        message = string.IsNullOrWhiteSpace(message) ? "Email confirmation failed." : message
                    }
                });
            }

            return Ok(new { success = true, data = new { emailConfirmed = true }, error = (object?)null });
        }

        // Browser-friendly verification endpoint for email links (confirms then redirects to the UI).
        [HttpGet("confirm-email")]
        [AllowAnonymous]
        public async Task<IActionResult> ConfirmEmailRedirect([FromQuery] string? userId, [FromQuery] string? token)
        {
            var frontendBaseUrl = _configuration["AppBaseUrl"] ?? "http://localhost:4200";
            var baseRedirect = $"{frontendBaseUrl.TrimEnd('/')}/auth/confirm-email";

            if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(token))
            {
                return Redirect($"{baseRedirect}?result=failed");
            }

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                return Redirect($"{baseRedirect}?result=failed");
            }

            string decodedToken;
            try
            {
                decodedToken = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(token));
            }
            catch
            {
                decodedToken = token;
            }

            var confirmResult = await _userManager.ConfirmEmailAsync(user, decodedToken);
            if (!confirmResult.Succeeded)
            {
                return Redirect($"{baseRedirect}?result=failed");
            }

            return Redirect($"{baseRedirect}?result=success");
        }

        // Development-only helper: confirms the current user's email without a token.
        // This is used for local testing when email delivery is disabled.
        [HttpPost("dev/confirm-email")]
        [Authorize]
        [ApiExplorerSettings(IgnoreApi = true)]
        public async Task<IActionResult> DevConfirmEmail()
        {
            if (!_environment.IsDevelopment())
            {
                return NotFound();
            }

            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Unauthorized(new { success = false, error = new { code = "Unauthorized", message = "User not authenticated." } });
            }

            if (!user.EmailConfirmed)
            {
                user.EmailConfirmed = true;
                var update = await _userManager.UpdateAsync(user);
                if (!update.Succeeded)
                {
                    var message = string.Join("; ", update.Errors.Select(e => e.Description));
                    return StatusCode(500, new { success = false, error = new { code = "UpdateFailed", message = string.IsNullOrWhiteSpace(message) ? "Failed to confirm email." : message } });
                }
            }

            return Ok(new { success = true, data = new { emailConfirmed = true }, error = (object?)null });
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

            // Set secure JWT cookie for session
            var cookieName = _configuration["Authentication:TokenCookieName"] ?? "AuthToken";
            var cookieOptions = BuildAuthCookieOptions(DateTimeOffset.UtcNow.AddHours(12));
            if (!string.IsNullOrEmpty(result.Token))
            {
                Response.Cookies.Append(cookieName, result.Token, cookieOptions);
            }

            return Ok(result);
        }

        [HttpPost("logout")]
        public async Task<IActionResult> Logout()
        {
            try
            {
                await _signInManager.SignOutAsync();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Logout sign-out failed: {ErrorMessage}", S(ex.Message));
            }

            var cookieName = _configuration["Authentication:TokenCookieName"] ?? "AuthToken";
            var cookieOptions = BuildAuthCookieOptions();

            try
            {
                Response.Cookies.Delete(cookieName, cookieOptions);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to delete auth cookie during logout: {ErrorMessage}", S(ex.Message));
            }

            return Ok(new { success = true });
        }

        [HttpGet("google-oauth-url")]
        public IActionResult GetGoogleOAuthUrl(string returnUrl = "/app/dashboard")
        {
            var (clientId, _) = GetGoogleClientSettings();
            if (string.IsNullOrEmpty(clientId))
            {
                return BadRequest(new { error = "Google OAuth is not configured" });
            }

            var backendBaseUrl = ResolveBackendBaseUrl(out var backendBaseUrlSource);
            var redirectUri = BuildGoogleRedirectUri(backendBaseUrl);

            // Generate state parameter for security
            var state = Guid.NewGuid().ToString();
            var safeReturnUrl = NormalizeReturnUrl(returnUrl);

            try
            {
                HttpContext.Session.SetString("oauth_state", state);
                HttpContext.Session.SetString("oauth_return_url", safeReturnUrl);
            }
            catch (Exception ex)
            {
                // Environment-aware session error handling
                _logger.LogWarning(ex, "Session initialization failed in OAuth URL generation: {ErrorMessage}", S(ex.Message));

                if (_environment.IsDevelopment())
                {
                    // In development, log but continue without session - reduced security but functional OAuth
                    _logger.LogWarning("Development environment: Continuing OAuth flow without session state (reduced CSRF protection)");

                    // Continue without session - skip setting session state
                    // OAuth will work but with reduced CSRF protection in development
                }
                else
                {
                    // In production, session failure is a configuration issue that should be addressed
                    _logger.LogError("Production environment: Session initialization failed - this indicates a configuration issue");
                    return BadRequest(new
                    {
                        error = "Session initialization failed. Please check HTTPS and session configuration.",
                        details = _environment.IsDevelopment() ? ex.Message : "Contact support if this issue persists"
                    });
                }
            }

            // Manually construct the Google OAuth URL
            var authUrl = $"https://accounts.google.com/o/oauth2/v2/auth?" +
                $"client_id={Uri.EscapeDataString(clientId)}&" +
                $"redirect_uri={Uri.EscapeDataString(redirectUri)}&" +
                $"response_type=code&" +
                $"scope={Uri.EscapeDataString("openid profile email")}&" +
                $"state={Uri.EscapeDataString(state)}";

            if (_environment.IsDevelopment())
            {
                _logger.LogInformation(
                    "Google OAuth URL generated (dev): backendBaseUrlSource={BackendBaseUrlSource} clientId={ClientId}",
                    LogSafe(backendBaseUrlSource, 64),
                    Sid(clientId));
                return Ok(new
                {
                    authUrl,
                    redirectUri,
                    backendBaseUrl,
                    backendBaseUrlSource,
                    clientId = Sid(clientId)
                });
            }

            return Ok(new { authUrl });
        }

        [HttpGet("external-login/{provider}")]
        public IActionResult ExternalLogin(string provider, string returnUrl = "/app/dashboard", bool ageConfirmed = false)
        {
            if (!string.Equals(provider, "google", StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest(new { error = $"{provider} OAuth not implemented yet" });
            }

            var safeReturnUrl = NormalizeReturnUrl(returnUrl);

            if (!ageConfirmed)
            {
                var frontendBaseUrl = GetFrontendBaseUrl();
                var encodedReturnUrl = Uri.EscapeDataString(safeReturnUrl);
                return Redirect($"{frontendBaseUrl}/auth/login?error=age_confirmation_required&returnUrl={encodedReturnUrl}");
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
                HttpContext.Session.SetString("oauth_return_url", safeReturnUrl);
            }
            catch (Exception ex)
            {
                // Environment-aware session error handling
                _logger.LogWarning(ex, "Session initialization failed in external login: {ErrorMessage}", S(ex.Message));

                if (_environment.IsDevelopment())
                {
                    // In development, log but continue without session - reduced security but functional OAuth
                    _logger.LogWarning("Development environment: Continuing OAuth flow without session state (reduced CSRF protection)");

                    // Continue without session - skip setting session state
                    // OAuth will work but with reduced CSRF protection in development
                }
                else
                {
                    // In production, session failure is a configuration issue that should be addressed
                    _logger.LogError("Production environment: Session initialization failed - this indicates a configuration issue");
                    return BadRequest(new
                    {
                        error = "Session initialization failed. Please check HTTPS and session configuration.",
                        details = _environment.IsDevelopment() ? ex.Message : "Contact support if this issue persists"
                    });
                }
            }

            var backendBaseUrl = ResolveBackendBaseUrl();
            var redirectUri = BuildGoogleRedirectUri(backendBaseUrl);

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

            var frontendBaseUrl = GetFrontendBaseUrl();

            string? returnUrl = null;
            string? sessionState = null;

            try
            {
                returnUrl = HttpContext.Session.GetString("oauth_return_url") ?? "/app/dashboard";
                sessionState = HttpContext.Session.GetString("oauth_state");
            }
            catch (Exception)
            {
                returnUrl = "/app/dashboard"; // Default fallback
                sessionState = null; // Will trigger session_expired error below
            }

            returnUrl = NormalizeReturnUrl(returnUrl);

            // Handle OAuth errors
            if (!string.IsNullOrEmpty(error))
            {
                _logger.LogWarning("OAuth callback returned error from provider: {OAuthError}", S(error));
                return Redirect($"{frontendBaseUrl}/auth/login?error=oauth_error");
            }

            // Validate state parameter - CRITICAL SECURITY CHECK
            // sessionState is already retrieved above with error handling

            // SECURITY: Always validate state parameter to prevent Login CSRF attacks
            // Environment-aware state validation
            if (string.IsNullOrEmpty(sessionState))
            {
                if (_environment.IsDevelopment())
                {
                    // In development, session might not be available - log warning but continue
                    _logger.LogWarning("Development environment: Session state missing - OAuth callback continuing with reduced CSRF protection");

                    // Still validate that state parameter was provided by OAuth provider
                    if (string.IsNullOrEmpty(state))
                    {
                        _logger.LogError("OAuth callback missing state parameter entirely - rejecting");
                        return Redirect($"{frontendBaseUrl}/auth/login?error=missing_state");
                    }

                    // Continue with reduced security - log this for visibility
                    _logger.LogWarning("Development OAuth callback proceeding without session state validation (CSRF protection reduced)");
                }
                else
                {
                    // In production, session state is required - reject the request
                    _logger.LogError("Production environment: Session state missing - potential CSRF attack or session configuration issue");
                    return Redirect($"{frontendBaseUrl}/auth/login?error=session_expired");
                }
            }
            else
            {
                // Session state available - perform full validation

                // If state parameter is missing from callback, this is suspicious - reject
                if (string.IsNullOrEmpty(state))
                {
                    _logger.LogError("OAuth callback missing state parameter - potential CSRF attack");
                    return Redirect($"{frontendBaseUrl}/auth/login?error=missing_state");
                }

                // Compare state values - must match exactly
                if (state != sessionState)
                {
                    _logger.LogError("OAuth state mismatch - potential CSRF attack");
                    return Redirect($"{frontendBaseUrl}/auth/login?error=invalid_state");
                }

                _logger.LogInformation("OAuth state validation passed successfully");
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

                // Set secure, HttpOnly cookie for JWT so the browser can send it on subsequent API requests
                var cookieName = _configuration["Authentication:TokenCookieName"] ?? "AuthToken";
                var cookieOptions = BuildAuthCookieOptions(DateTimeOffset.UtcNow.AddHours(12));
                Response.Cookies.Append(cookieName, tokenInfo.Token, cookieOptions);

                // In development, include token for frontend to set cookie on 4200 origin via proxy
                if (_environment.IsDevelopment())
                {
                    var separator = returnUrl.Contains('?', StringComparison.Ordinal) ? "&" : "?";
                    return Redirect($"{frontendBaseUrl}{returnUrl}{separator}token={Uri.EscapeDataString(tokenInfo.Token)}");
                }
                // In production, cookie-only flow
                return Redirect($"{frontendBaseUrl}{returnUrl}");
            }
            catch (Exception)
            {
                return Redirect($"{frontendBaseUrl}/auth/login?error=oauth_processing_failed");
            }
        }

        private async Task<GoogleTokenResponse?> ExchangeCodeForTokenAsync(string code)
        {
            var (clientId, clientSecret) = GetGoogleClientSettings();
            var backendBaseUrl = ResolveBackendBaseUrl();
            var redirectUri = BuildGoogleRedirectUri(backendBaseUrl);

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
                if (_environment.IsDevelopment())
                {
                    var errorBody = await response.Content.ReadAsStringAsync();
                    _logger.LogWarning(
                        "Google token exchange failed: status={StatusCode} redirectUri={RedirectUri} body={Body}",
                        (int)response.StatusCode,
                        S(redirectUri),
                        S(errorBody));
                }
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
            catch (Exception)
            {
                // Fall through to manual configuration reading
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

        private string ResolveBackendBaseUrl() => ResolveBackendBaseUrl(out _);

        private string BuildGoogleRedirectUri(string backendBaseUrl)
        {
            var redirectUri = $"{backendBaseUrl}/api/auth/external-login-callback";
            return AppendNgrokSkipWarning(redirectUri);
        }

        private string AppendNgrokSkipWarning(string url)
        {
            if (!_environment.IsDevelopment())
            {
                return url;
            }

            if (!url.Contains("ngrok", StringComparison.OrdinalIgnoreCase))
            {
                return url;
            }

            var uri = new Uri(url);
            var query = QueryHelpers.ParseQuery(uri.Query);
            if (query.ContainsKey("ngrok-skip-browser-warning"))
            {
                return url;
            }

            return QueryHelpers.AddQueryString(url, "ngrok-skip-browser-warning", "true");
        }

        private string ResolveBackendBaseUrl(out string source)
        {
            static string Normalize(string url) => url.TrimEnd('/');
            source = "request";

            // First check if we're in Azure production environment
            var azureWebsiteName = Environment.GetEnvironmentVariable("WEBSITE_SITE_NAME");

            if (!string.IsNullOrEmpty(azureWebsiteName))
            {
                // We're definitely in Azure - use the production OAuth base URL
                var productionOAuthBase = _configuration["Authentication:OAuth:BaseUrl"];

                if (!string.IsNullOrWhiteSpace(productionOAuthBase))
                {
                    source = "azure-config";
                    return Normalize(productionOAuthBase);
                }
            }

            // If forwarded headers are present, prefer them
            var forwardedProto = Request.Headers["X-Forwarded-Proto"].FirstOrDefault();
            var forwardedHost = Request.Headers["X-Forwarded-Host"].FirstOrDefault();

            if (!string.IsNullOrEmpty(forwardedProto) && !string.IsNullOrEmpty(forwardedHost))
            {
                var forwardedUrl = $"{forwardedProto}://{forwardedHost}";
                source = "forwarded-headers";
                return Normalize(forwardedUrl);
            }

            // Prefer explicit env override
            var envBase = Environment.GetEnvironmentVariable("OAUTH_BASE_URL") ?? _configuration["OAUTH_BASE_URL"];

            if (!string.IsNullOrWhiteSpace(envBase))
            {
                source = "env";
                return Normalize(envBase);
            }

            // Prefer configured base URL, even for localhost requests (important when the API is behind a dev proxy)
            var cfgBase = _configuration["Authentication:OAuth:BaseUrl"];

            if (!string.IsNullOrWhiteSpace(cfgBase))
            {
                source = "config";
                return Normalize(cfgBase);
            }

            // Development fallback: Jwt:ValidIssuer typically points to the API origin
            if (_environment.IsDevelopment())
            {
                var jwtIssuer = _configuration["Jwt:ValidIssuer"];

                if (!string.IsNullOrWhiteSpace(jwtIssuer))
                {
                    source = "jwt-valid-issuer";
                    return Normalize(jwtIssuer);
                }
            }

            // Check if we're clearly in development (localhost)
            var isLocalHost = Request.Host.Host.Contains("localhost") || Request.Host.Host.Contains("127.0.0.1");
            if (isLocalHost)
            {
                var localUrl = $"{Request.Scheme}://{Request.Host.Value}";
                source = "request-local";
                return Normalize(localUrl);
            }

            // Last resort - use current request
            var fallbackUrl = $"{Request.Scheme}://{Request.Host.Value}";
            return Normalize(fallbackUrl);
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
                        Credits = 5,
                        LastCreditReset = DateTime.UtcNow,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    };

                    _context.UserProfiles.Add(userProfile);
                    await _context.SaveChangesAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to create user profile during OAuth user creation");
                    throw; // Re-throw to handle at higher level
                }

                try
                {
                    QueueWelcomeEmail(user.Id);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to send welcome email for OAuth user {UserId}", Sid(user.Id));
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
                            Credits = 5,
                            LastCreditReset = DateTime.UtcNow,
                            CreatedAt = DateTime.UtcNow,
                            UpdatedAt = DateTime.UtcNow
                        };

                        _context.UserProfiles.Add(userProfile);
                        await _context.SaveChangesAsync();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to create missing user profile for existing user");
                        throw; // Re-throw to handle at higher level
                    }
                }
                // Profile already exists, no action needed
            }

            return user;
        }

        [HttpGet("validate-session")]
        [Authorize]
        public IActionResult ValidateSession()
        {
            // If the request is authenticated via JWT (cookie-based), return 204.
            // Otherwise the [Authorize] attribute will result in 401.
            return NoContent();
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
                if (ModelState.TryGetValue(nameof(ProfileCompletionDto.AgeConfirmed), out var ageConfirmation) &&
                    ageConfirmation.Errors.Count > 0)
                {
                    return BadRequest(new { success = false, error = "Age confirmation is required to complete profile." });
                }

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
        /// <summary>
        /// Get the appropriate frontend base URL based on current request context
        /// </summary>
        private string GetFrontendBaseUrl()
        {
            // Check environment variable first (used by Docker and local dev)
            var envVar = Environment.GetEnvironmentVariable("APP_BASE_URL");
            if (!string.IsNullOrWhiteSpace(envVar))
            {
                return envVar.TrimEnd('/');
            }

            // Fall back to configuration (appsettings.json)
            var configured = _configuration["AppBaseUrl"];
            if (!string.IsNullOrWhiteSpace(configured))
            {
                return configured.TrimEnd('/');
            }

            return _environment.IsDevelopment() ? "http://localhost:4200" : "https://aiprofilephotomaker.com";
        }

        private string NormalizeReturnUrl(string? returnUrl)
        {
            if (string.IsNullOrWhiteSpace(returnUrl))
            {
                return "/app/dashboard";
            }

            // Prevent open-redirects and unexpected protocol/host injection.
            if (!Url.IsLocalUrl(returnUrl))
            {
                return "/app/dashboard";
            }

            // Url.IsLocalUrl also allows "~/" which isn't useful for our SPA routing.
            if (returnUrl.StartsWith("~/", StringComparison.Ordinal))
            {
                return "/" + returnUrl[2..];
            }

            return returnUrl.StartsWith("/", StringComparison.Ordinal) ? returnUrl : "/app/dashboard";
        }


        [HttpPost("set-cookie")]
        [ApiExplorerSettings(IgnoreApi = true)]
        public IActionResult SetCookie([FromBody] SetCookieRequest payload)
        {
            if (!_environment.IsDevelopment())
            {
                return NotFound();
            }
            try
            {
                // Use strongly-typed binding to avoid dynamic runtime binder issues
                var token = payload?.Token ?? string.Empty;
                if (string.IsNullOrWhiteSpace(token))
                {
                    return BadRequest(new { error = "Missing token" });
                }

                var cookieName = _configuration["Authentication:TokenCookieName"] ?? "AuthToken";
                var isSecureRequest = Request.IsHttps;
                var cookieOptions = new CookieOptions
                {
                    HttpOnly = true,
                    Secure = isSecureRequest,
                    SameSite = isSecureRequest
                        ? SameSiteMode.None // required for cross-site redirects when running behind https (e.g., ngrok)
                        : SameSiteMode.Lax, // allow local http dev without browsers rejecting SameSite=None without Secure
                    Domain = null, // Development-only endpoint, keep local
                    Expires = DateTimeOffset.UtcNow.AddHours(12),
                    Path = "/"
                };
                Response.Cookies.Append(cookieName, token, cookieOptions);
                return Ok(new { success = true });
            }
            catch
            {
                return StatusCode(500, new { error = "Failed to set cookie" });
            }
        }
    }

    public class ConfirmEmailRequest
    {
        public string UserId { get; set; } = string.Empty;
        public string Token { get; set; } = string.Empty;
    }

    // DTOs for Google OAuth
    public class SetCookieRequest
    {
        // Accept both "token" and "Token" JSON names (case-insensitive by default)
        public string Token { get; set; } = string.Empty;
    }
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
