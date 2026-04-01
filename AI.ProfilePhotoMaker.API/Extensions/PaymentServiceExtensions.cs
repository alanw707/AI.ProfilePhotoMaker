using AI.ProfilePhotoMaker.API.Configuration;
using AI.ProfilePhotoMaker.API.Services;
using AI.ProfilePhotoMaker.API.Services.Payments;
using Microsoft.Extensions.Options;

namespace AI.ProfilePhotoMaker.API.Extensions;

/// <summary>
/// Extension methods for payment and Stripe service configuration
/// </summary>
public static class PaymentServiceExtensions
{
    /// <summary>
    /// Add payment, Stripe, and related option services to the service collection
    /// </summary>
    public static IServiceCollection AddPaymentServices(this IServiceCollection services, IConfiguration configuration)
    {
        // Options binding
        services.Configure<EmailOptions>(configuration.GetSection(EmailOptions.SectionName));
        services.Configure<StripeOptions>(configuration.GetSection(StripeOptions.SectionName));
        services.Configure<RetentionPolicyBackgroundServiceOptions>(
            configuration.GetSection(RetentionPolicyBackgroundServiceOptions.SectionName));
        services.Configure<RetentionNotificationOptions>(
            configuration.GetSection(RetentionNotificationOptions.SectionName));
        services.Configure<AbandonedUploadOptions>(
            configuration.GetSection(AbandonedUploadOptions.SectionName));
        services.AddSingleton<TimeProvider>(TimeProvider.System);

        services.PostConfigure<StripeOptions>(options =>
        {
            if (string.IsNullOrWhiteSpace(options.PublishableKey))
            {
                options.PublishableKey = Environment.GetEnvironmentVariable(EnvironmentConfiguration.STRIPE_PUBLISHABLE_KEY) ?? string.Empty;
            }
            if (string.IsNullOrWhiteSpace(options.SecretKey))
            {
                options.SecretKey = Environment.GetEnvironmentVariable(EnvironmentConfiguration.STRIPE_SECRET_KEY) ?? string.Empty;
            }
            if (string.IsNullOrWhiteSpace(options.WebhookSecret))
            {
                options.WebhookSecret = Environment.GetEnvironmentVariable(EnvironmentConfiguration.STRIPE_WEBHOOK_SECRET) ?? string.Empty;
            }
            options.PublishableKey = options.PublishableKey?.Trim() ?? string.Empty;
            options.SecretKey = options.SecretKey?.Trim() ?? string.Empty;
            options.WebhookSecret = options.WebhookSecret?.Trim() ?? string.Empty;
        });

        services.Configure<PaymentSimulationOptions>(configuration.GetSection(PaymentSimulationOptions.SectionName));

        services.AddSingleton(provider =>
        {
            var stripeOptions = provider.GetRequiredService<IOptions<StripeOptions>>().Value;
            var appInfo = new Stripe.AppInfo
            {
                Name = "AI Profile Photo Maker API",
                Version = typeof(Program).Assembly.GetName().Version?.ToString() ?? "1.0.0",
                Url = "https://github.com/alanwsmith/AI.ProfilePhotoMaker"
            };
            Stripe.StripeConfiguration.AppInfo = appInfo;
            var secretKey = StripeClientFactory.GetSafeSecretKey(stripeOptions.SecretKey);
            Stripe.StripeConfiguration.ApiKey = secretKey;
            return StripeClientFactory.Create(secretKey);
        });

        services.AddScoped<IStripePaymentService, StripePaymentService>();
        services.AddScoped<IStripeWebhookService, StripeWebhookService>();
        // Email notifications (registered via HttpClient below)
        services.AddScoped<IPendingGenerationService, PendingGenerationService>();

        services.Configure<LegacyCompatibilityOptions>(configuration.GetSection(LegacyCompatibilityOptions.SectionName));

        return services;
    }
}
