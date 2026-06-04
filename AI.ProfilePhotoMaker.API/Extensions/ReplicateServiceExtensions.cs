using AI.ProfilePhotoMaker.API.Configuration;
using AI.ProfilePhotoMaker.API.Data;
using AI.ProfilePhotoMaker.API.Services;
using AI.ProfilePhotoMaker.API.Services.ImageProcessing;
using AI.ProfilePhotoMaker.API.Services.Storage;

namespace AI.ProfilePhotoMaker.API.Extensions;

/// <summary>
/// Extension methods for Replicate API service configuration
/// </summary>
public static class ReplicateServiceExtensions
{
    /// <summary>
    /// Add Replicate API services to the service collection, with optional mock mode
    /// </summary>
    public static IServiceCollection AddReplicateServices(this IServiceCollection services, IConfiguration configuration)
    {
        // Register Replicate SDK (skip in mock mode)
        var enableReplicateMock = (Environment.GetEnvironmentVariable("ENABLE_REPLICATE_MOCK") ?? string.Empty)
            .Equals("true", StringComparison.OrdinalIgnoreCase);
        if (!enableReplicateMock)
        {
            services.AddSingleton<Replicate.ReplicateApi>(provider =>
            {
                var cfg = provider.GetRequiredService<IConfiguration>();
                var envToken = (Environment.GetEnvironmentVariable(EnvironmentConfiguration.REPLICATE_API_TOKEN) ?? string.Empty).Trim();
                var cfgToken = (cfg["Replicate:ApiToken"] ?? string.Empty).Trim();
                var apiToken = !string.IsNullOrWhiteSpace(envToken) ? envToken : cfgToken;
                if (string.IsNullOrEmpty(apiToken))
                {
                    throw new InvalidOperationException("Replicate API token not configured");
                }
                return new Replicate.ReplicateApi(apiToken);
            });
        }

        // Register Replicate services with optional mock
        if (enableReplicateMock)
        {
            services.AddScoped<IReplicateApiClient, MockReplicateApiClient>();
            // Register sub-interfaces forwarding to the facade
            services.AddScoped<IReplicateModelService>(sp => sp.GetRequiredService<IReplicateApiClient>());
            services.AddScoped<IReplicateTrainingService>(sp => sp.GetRequiredService<IReplicateApiClient>());
            services.AddScoped<IReplicatePredictionService>(sp => sp.GetRequiredService<IReplicateApiClient>());
            services.AddScoped<IReplicateEnhancementService>(sp => sp.GetRequiredService<IReplicateApiClient>());
            Console.WriteLine("Mock mode enabled - skipping ModelDiscoveryService registration");
        }
        else
        {
            services.AddHttpClient<IReplicateApiClient, ReplicateApiClient>(client =>
            {
                client.Timeout = TimeSpan.FromMinutes(2);
            });
            // Register sub-interfaces forwarding to the facade
            services.AddScoped<IReplicateModelService>(sp => sp.GetRequiredService<IReplicateApiClient>());
            services.AddScoped<IReplicateTrainingService>(sp => sp.GetRequiredService<IReplicateApiClient>());
            services.AddScoped<IReplicatePredictionService>(sp => sp.GetRequiredService<IReplicateApiClient>());
            services.AddScoped<IReplicateEnhancementService>(sp => sp.GetRequiredService<IReplicateApiClient>());

            services.Configure<ModelDiscoveryRobustnessOptions>(options =>
            {
                options.MaxRetryAttempts = 3;
                options.BaseRetryDelayMs = 1000;
                options.MaxRetryDelayMs = 10000;
                options.CircuitBreakerFailureThreshold = 5;
                options.CircuitBreakerTimeoutMs = 60000;
            });

            services.AddSingleton<ModelDiscoveryRobustnessService>();
            services.AddScoped<IModelDiscoveryService, ModelDiscoveryService>();
        }

        services.AddHttpClient<OpenAIImageGenerationService>(client =>
        {
            client.Timeout = TimeSpan.FromMinutes(5);
        });
        services.AddScoped<IHeadshotGenerationProvider, OpenAIHeadshotGenerationProvider>();
        services.AddScoped<IHeadshotGenerationService>(sp => new HeadshotGenerationService(
            sp.GetRequiredService<ApplicationDbContext>(),
            sp.GetRequiredService<IBasicTierService>(),
            sp.GetRequiredService<IHeadshotGenerationProvider>(),
            sp.GetRequiredService<IStorageService>(),
            sp.GetRequiredService<StoragePathResolver>(),
            sp.GetRequiredService<IHttpClientFactory>(),
            sp.GetRequiredService<IConfiguration>(),
            sp.GetRequiredService<ILogger<HeadshotGenerationService>>(),
            sp.GetRequiredService<IOutcomePackageService>()));

        services.AddScoped<IWebhookUrlResolver, WebhookUrlResolver>();
        services.AddHttpClient<WebhookUrlResolver>();
        services.AddHttpClient<IImageDownloadService, ImageDownloadService>();
        services.AddScoped<IUserProfileRepository, UserProfileRepository>();

        return services;
    }
}
