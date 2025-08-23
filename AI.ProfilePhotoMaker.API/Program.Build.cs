using System.Text;
using Azure.Storage.Blobs;
using AI.ProfilePhotoMaker.API.Data;
using AI.ProfilePhotoMaker.API.Extensions;
using AI.ProfilePhotoMaker.API.Models;
using AI.ProfilePhotoMaker.API.Services.Authentication;
using AI.ProfilePhotoMaker.API.Services.Authentication.interfaces;
using AI.ProfilePhotoMaker.API.Services.Database;
using AI.ProfilePhotoMaker.API.Services;
using AI.ProfilePhotoMaker.API.Services.ImageProcessing;
using AI.ProfilePhotoMaker.API.Services.Payment;
using AI.ProfilePhotoMaker.API.Services.Storage;
using AI.ProfilePhotoMaker.API.Services.Monitoring;
using AI.ProfilePhotoMaker.API.Middleware;
using AI.ProfilePhotoMaker.API.Configuration;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.TestHost;
using Serilog;

public partial class Program
{

    /// <summary>
    /// Loads environment variables from .env files based on environment
    /// </summary>
    static void LoadEnvironmentVariables(IWebHostEnvironment environment)
    {
        try
        {
            // Look for .env files in the solution root directory (parent of API directory)
            var contentRoot = environment.ContentRootPath;
            var solutionRoot = Directory.GetParent(contentRoot)?.FullName ?? contentRoot;


            var envFiles = new[]
            {
                ".env",
                $".env.{environment.EnvironmentName.ToLower()}",
                ".env.local",
                $".env.{environment.EnvironmentName.ToLower()}.local"
            };

            bool anyFileFound = false;
            foreach (var envFile in envFiles)
            {
                // First try solution root directory
                var envFilePath = Path.Combine(solutionRoot, envFile);
                if (File.Exists(envFilePath))
                {
                    LoadEnvFile(envFilePath);
                    anyFileFound = true;
                }
                else
                {
                    // Fallback to API directory for compatibility
                    envFilePath = Path.Combine(contentRoot, envFile);
                    if (File.Exists(envFilePath))
                    {
                        LoadEnvFile(envFilePath);
                        anyFileFound = true;
                    }
                }
            }

            if (!anyFileFound)
            {
            }
        }
        catch (Exception)
        {
        }
    }

    /// <summary>
    /// Loads environment variables from a specific .env file
    /// </summary>
    static void LoadEnvFile(string filePath)
    {
        var lines = File.ReadAllLines(filePath);
        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#"))
                continue;

            var parts = line.Split('=', 2);
            if (parts.Length != 2)
                continue;

            var key = parts[0].Trim();
            var value = parts[1].Trim();

            // Remove surrounding quotes if present
            if ((value.StartsWith("\"") && value.EndsWith("\"")) ||
                (value.StartsWith("'") && value.EndsWith("'")))
            {
                value = value[1..^1];
            }

            // Only set if not already set (environment variables take precedence)
            if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable(key)))
            {
                Environment.SetEnvironmentVariable(key, value);
            }
        }
    }

    /// <summary>
    /// Validates webhook URL configuration on startup and logs the results
    /// </summary>
    static async Task ValidateWebhookConfigurationAsync(WebApplication app)
    {
        try
        {
            using var scope = app.Services.CreateScope();
            var webhookUrlResolver = scope.ServiceProvider.GetRequiredService<IWebhookUrlResolver>();
            var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
            var environment = scope.ServiceProvider.GetRequiredService<IWebHostEnvironment>();

            logger.LogInformation("🔗 Validating webhook URL configuration for {Environment} environment...", environment.EnvironmentName);

            // Get the webhook base URL
            var webhookBaseUrl = await webhookUrlResolver.GetWebhookBaseUrlAsync();

            if (webhookBaseUrl == null)
            {
                if (environment.IsDevelopment())
                {
                    logger.LogWarning("⚠️  Webhook URLs are disabled in development. Consider setting up ngrok for webhook testing.");
                    logger.LogInformation("💡 To enable webhooks in development:");
                    logger.LogInformation("   1. Start ngrok: ngrok http 5000");
                    logger.LogInformation("   2. Set Webhooks:NgrokTunnelUrl in appsettings.Development.json");
                    logger.LogInformation("   3. Or set Webhooks:BaseUrl to your preferred HTTPS endpoint");
                }
                else
                {
                    logger.LogError("❌ Webhook URLs are disabled in production! This may affect functionality.");
                    logger.LogError("🔧 Ensure AppBaseUrl is configured with an HTTPS URL in production.");
                }
                return;
            }

            logger.LogInformation("✅ Webhook base URL resolved: {WebhookBaseUrl}", webhookBaseUrl);

            // Test a sample webhook URL
            var sampleWebhookUrl = await webhookUrlResolver.GetWebhookUrlAsync("/api/webhooks/replicate/prediction-complete");
            logger.LogInformation("📨 Sample webhook URL: {SampleWebhookUrl}", sampleWebhookUrl);

            // Validate the webhook URL is accessible (optional validation)
            var isValid = await webhookUrlResolver.ValidateWebhookUrlAsync();
            if (isValid)
            {
                logger.LogInformation("✅ Webhook URL validation passed - endpoints are reachable");
            }
            else
            {
                if (environment.IsDevelopment())
                {
                    logger.LogWarning("⚠️  Webhook URL validation failed - endpoints may not be reachable yet. This is normal if ngrok is not running.");
                }
                else
                {
                    logger.LogWarning("⚠️  Webhook URL validation failed - please ensure your production endpoints are accessible");
                }
            }

            // Log environment-specific guidance
            if (environment.IsDevelopment())
            {
                logger.LogInformation("🔧 Development webhook configuration:");
                logger.LogInformation("   • Webhooks will work if HTTPS is configured (ngrok, local HTTPS, etc.)");
                logger.LogInformation("   • HTTP webhooks are disabled for security (Replicate API requirement)");
                logger.LogInformation("   • Configure Webhooks:NgrokTunnelUrl for manual ngrok URL override");
            }
            else
            {
                logger.LogInformation("🚀 Production webhook configuration active");
                logger.LogInformation("   • Webhooks enabled for HTTPS environments");
                logger.LogInformation("   • Using AppBaseUrl: {AppBaseUrl}", app.Configuration["AppBaseUrl"]);
            }
        }
        catch (Exception ex)
        {
            var logger = app.Services.GetRequiredService<ILogger<Program>>();
            logger.LogError(ex, "❌ Failed to validate webhook configuration during startup");
        }
    }

    /// <summary>
    /// Validates Replicate configuration on startup to catch missing required settings
    /// </summary>
    static Task ValidateReplicateConfigurationAsync(WebApplication app)
    {
        try
        {
            using var scope = app.Services.CreateScope();
            var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
            var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
            var environment = scope.ServiceProvider.GetRequiredService<IWebHostEnvironment>();

            logger.LogInformation("🤖 Validating Replicate configuration for {Environment} environment...", environment.EnvironmentName);

            var configurationErrors = new List<string>();
            var configurationWarnings = new List<string>();

            // Check required Replicate settings
            var apiToken = configuration["Replicate:ApiToken"];
            if (string.IsNullOrEmpty(apiToken) || apiToken.StartsWith("REPLACE_WITH_", StringComparison.OrdinalIgnoreCase))
            {
                configurationErrors.Add("Replicate:ApiToken is missing or contains placeholder value");
            }
            else
            {
                logger.LogInformation("✅ Replicate API Token is configured");
            }

            var fluxTrainingModelId = configuration["Replicate:FluxTrainingModelId"];
            if (string.IsNullOrEmpty(fluxTrainingModelId))
            {
                configurationWarnings.Add("Replicate:FluxTrainingModelId is missing - model training may fail");
            }
            else
            {
                logger.LogInformation("✅ Flux Training Model ID: {ModelId}", fluxTrainingModelId);
            }

            var fluxGenerationModelId = configuration["Replicate:FluxGenerationModelId"];
            if (string.IsNullOrEmpty(fluxGenerationModelId))
            {
                configurationWarnings.Add("Replicate:FluxGenerationModelId is missing - basic image generation may fail");
            }
            else
            {
                logger.LogInformation("✅ Flux Generation Model ID: {ModelId}", fluxGenerationModelId);
            }

            var fluxKontextProModelId = configuration["Replicate:FluxKontextProModelId"];
            if (string.IsNullOrEmpty(fluxKontextProModelId))
            {
                configurationErrors.Add("Replicate:FluxKontextProModelId is missing - photo enhancement will fail");
            }
            else
            {
                logger.LogInformation("✅ Flux Kontext Pro Model ID: {ModelId}", fluxKontextProModelId);
            }

            var webhookSecret = configuration["Replicate:WebhookSecret"];
            if (string.IsNullOrEmpty(webhookSecret) || webhookSecret.StartsWith("REPLACE_WITH_", StringComparison.OrdinalIgnoreCase))
            {
                configurationErrors.Add("Replicate:WebhookSecret is missing or contains placeholder value");
            }
            else
            {
                logger.LogInformation("✅ Replicate Webhook Secret is configured");
            }

            // Report configuration status
            if (configurationErrors.Any())
            {
                logger.LogError("❌ Critical Replicate configuration errors found:");
                foreach (var error in configurationErrors)
                {
                    logger.LogError("   • {Error}", error);
                }

                if (environment.IsProduction())
                {
                    logger.LogError("🚨 Production deployment detected with critical configuration errors!");
                    logger.LogError("🔧 Please configure the missing Replicate settings before proceeding.");
                }
                else
                {
                    logger.LogWarning("⚠️  Development environment with configuration errors - some features will not work");
                }
            }

            if (configurationWarnings.Any())
            {
                logger.LogWarning("⚠️  Replicate configuration warnings:");
                foreach (var warning in configurationWarnings)
                {
                    logger.LogWarning("   • {Warning}", warning);
                }
            }

            if (!configurationErrors.Any() && !configurationWarnings.Any())
            {
                logger.LogInformation("✅ All Replicate configuration settings are properly configured");
            }

            // Environment-specific guidance
            if (environment.IsDevelopment())
            {
                logger.LogInformation("🔧 Development Replicate configuration:");
                logger.LogInformation("   • Configure settings in appsettings.Development.json or user secrets");
                logger.LogInformation("   • Use 'dotnet user-secrets set \"Replicate:ApiToken\" \"your-token\"' for sensitive values");
            }
            else
            {
                logger.LogInformation("🚀 Production Replicate configuration:");
                logger.LogInformation("   • Ensure all secrets are properly configured via environment variables");
                logger.LogInformation("   • Verify Azure Key Vault or container secrets are accessible");
            }
        }
        catch (Exception ex)
        {
            var logger = app.Services.GetRequiredService<ILogger<Program>>();
            logger.LogError(ex, "❌ Failed to validate Replicate configuration during startup");
        }
        return Task.CompletedTask;
    }
}