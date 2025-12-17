namespace AI.ProfilePhotoMaker.API.Services;

public sealed class StartupDiagnosticsHostedService : IHostedService
{
    private readonly ILogger<StartupDiagnosticsHostedService> _logger;
    private readonly IConfiguration _configuration;
    private readonly IWebHostEnvironment _environment;

    public StartupDiagnosticsHostedService(
        ILogger<StartupDiagnosticsHostedService> logger,
        IConfiguration configuration,
        IWebHostEnvironment environment)
    {
        _logger = logger;
        _configuration = configuration;
        _environment = environment;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        var appBaseUrl = Environment.GetEnvironmentVariable("APP_BASE_URL") ?? _configuration["AppBaseUrl"];
        var externalApiBaseUrl = Environment.GetEnvironmentVariable("EXTERNAL_API_BASE_URL") ?? _configuration["ExternalApiBaseUrl"];
        var oauthBaseUrlEnv = Environment.GetEnvironmentVariable("OAUTH_BASE_URL") ?? _configuration["OAUTH_BASE_URL"];
        var oauthBaseUrlCfg = _configuration["Authentication:OAuth:BaseUrl"];

        var effectiveOauthBaseUrl = !string.IsNullOrWhiteSpace(oauthBaseUrlEnv)
            ? oauthBaseUrlEnv
            : (!string.IsNullOrWhiteSpace(oauthBaseUrlCfg) ? oauthBaseUrlCfg : null);

        var expectedRedirectUri = !string.IsNullOrWhiteSpace(effectiveOauthBaseUrl)
            ? $"{effectiveOauthBaseUrl.TrimEnd('/')}/api/auth/external-login-callback"
            : null;

        if (_environment.IsDevelopment())
        {
            _logger.LogInformation(
                "Startup OAuth config (dev): AppBaseUrl={AppBaseUrl} ExternalApiBaseUrl={ExternalApiBaseUrl} OAUTH_BASE_URL={OauthBaseUrlEnv} Authentication:OAuth:BaseUrl={OauthBaseUrlCfg} ExpectedRedirectUri={ExpectedRedirectUri}",
                appBaseUrl,
                externalApiBaseUrl,
                oauthBaseUrlEnv,
                oauthBaseUrlCfg,
                expectedRedirectUri);
        }
        else
        {
            _logger.LogDebug(
                "Startup OAuth config: AppBaseUrl={AppBaseUrl} ExternalApiBaseUrl={ExternalApiBaseUrl} OAUTH_BASE_URL={OauthBaseUrlEnv} Authentication:OAuth:BaseUrl={OauthBaseUrlCfg} ExpectedRedirectUri={ExpectedRedirectUri}",
                appBaseUrl,
                externalApiBaseUrl,
                oauthBaseUrlEnv,
                oauthBaseUrlCfg,
                expectedRedirectUri);
        }

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

