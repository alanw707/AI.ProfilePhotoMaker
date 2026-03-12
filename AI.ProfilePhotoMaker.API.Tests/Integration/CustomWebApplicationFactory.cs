using System.Net.Http.Headers;
using System.Text;
using System.Linq;
using Azure.Storage.Blobs;
using AI.ProfilePhotoMaker.API;
using AI.ProfilePhotoMaker.API.Constants;
using AI.ProfilePhotoMaker.API.Data;
using AI.ProfilePhotoMaker.API.Models;
using AI.ProfilePhotoMaker.API.Models.DTOs;
using AI.ProfilePhotoMaker.API.Tests.Infrastructure;
using AI.ProfilePhotoMaker.API.Services.Authentication;
using AI.ProfilePhotoMaker.API.Services.Authentication.interfaces;
using AI.ProfilePhotoMaker.API.Services.Database;
using AI.ProfilePhotoMaker.API.Services;
using AI.ProfilePhotoMaker.API.Services.Payments;
using AI.ProfilePhotoMaker.API.Services.ImageProcessing;
using AI.ProfilePhotoMaker.API.Services.Notifications;
using AI.ProfilePhotoMaker.API.Services.Security;
using AI.ProfilePhotoMaker.API.Services.Storage;
using Azure.Storage.Sas;
using AI.ProfilePhotoMaker.API.Middleware;
using AI.ProfilePhotoMaker.API.Configuration;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Serilog;

namespace AI.ProfilePhotoMaker.API.Tests.Integration;

public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    // Use a static database name to ensure all scopes share the same in-memory database
    private readonly string _databaseName = $"TestDb_{Guid.NewGuid()}_{DateTime.UtcNow.Ticks}";

    protected override IHostBuilder CreateHostBuilder()
    {
        // Set test environment variables
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Testing");
        Environment.SetEnvironmentVariable("RUNNING_IN_TESTS", "true");
        Environment.SetEnvironmentVariable("ENABLE_REPLICATE_MOCK", "false");
        Environment.SetEnvironmentVariable("JWT_SECRET", new string('X', 40));
        Environment.SetEnvironmentVariable("REPLICATE_API_TOKEN", "r8_dummy_token_abcdefghijklmnopqrstuvwxyz");
        Environment.SetEnvironmentVariable("REPLICATE_WEBHOOK_SECRET", "whsec_dummy_secret_123456");
        Environment.SetEnvironmentVariable("STRIPE_SECRET_KEY", "sk_test_1234567890");
        Environment.SetEnvironmentVariable("GOOGLE_CLIENT_ID", "1234567890-test.apps.googleusercontent.com");
        Environment.SetEnvironmentVariable("GOOGLE_CLIENT_SECRET", "GOCSPX-dummy-secret-1234567890");
        Environment.SetEnvironmentVariable("AZURE_STORAGE_CONNECTION_STRING", "UseDevelopmentStorage=true");
        Environment.SetEnvironmentVariable("OPENAI__ApiKey", "test-openai-key");

        return Host.CreateDefaultBuilder()
            .ConfigureWebHostDefaults(webBuilder =>
            {
                webBuilder.UseTestServer();
                webBuilder.UseEnvironment("Testing");
                webBuilder.ConfigureServices(ConfigureTestServices);
                webBuilder.Configure(ConfigureTestPipeline);
            })
            .ConfigureLogging(logging => logging.ClearProviders().AddConsole());
    }

    private void ConfigureTestServices(IServiceCollection services)
    {
        // Add basic ASP.NET Core services
        services.AddControllers()
            .AddApplicationPart(typeof(AI.ProfilePhotoMaker.API.Controllers.ReplicateController).Assembly)
            .AddJsonOptions(options =>
            {
                options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
            });

        services.Configure<ApiBehaviorOptions>(options =>
        {
            options.InvalidModelStateResponseFactory = context =>
            {
                var routeValues = context.ActionDescriptor.RouteValues;
                var isAuthLogin = routeValues.TryGetValue("controller", out var controller) &&
                                  routeValues.TryGetValue("action", out var action) &&
                                  string.Equals(controller, "Auth", StringComparison.OrdinalIgnoreCase) &&
                                  string.Equals(action, "Login", StringComparison.OrdinalIgnoreCase);

                if (isAuthLogin)
                {
                    var ageGateFailed = context.ModelState
                        .Where(entry => entry.Key.Contains(nameof(LoginDto.AgeConfirmed), StringComparison.OrdinalIgnoreCase))
                        .SelectMany(entry => entry.Value?.Errors ?? Enumerable.Empty<ModelError>())
                        .Any();

                    if (ageGateFailed)
                    {
                        return new BadRequestObjectResult(new AuthResponseDto(
                            false,
                            AuthValidationMessages.AgeConfirmationRequiredToSignIn,
                            string.Empty,
                            null));
                    }
                }

                return new BadRequestObjectResult(new ValidationProblemDetails(context.ModelState));
            };
        });

        // Add InMemory database with shared name for test consistency
        services.AddDbContext<ApplicationDbContext>(options =>
        {
            options.UseInMemoryDatabase(_databaseName);
        });

        // Add Identity
        services.AddIdentity<ApplicationUser, IdentityRole>()
            .AddEntityFrameworkStores<ApplicationDbContext>()
            .AddDefaultTokenProviders();

        // Configure Identity options
        services.Configure<IdentityOptions>(options =>
        {
            options.Password.RequireDigit = false;
            options.Password.RequireLowercase = false;
            options.Password.RequireNonAlphanumeric = false;
            options.Password.RequireUppercase = false;
            options.Password.RequiredLength = 6;
            options.User.RequireUniqueEmail = true;
        });

        // Add test authentication
        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = TestAuthHandler.SchemeName;
            options.DefaultChallengeScheme = TestAuthHandler.SchemeName;
        }).AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, options => { });

        // Add essential services
        services.AddHttpContextAccessor();
        services.AddMemoryCache();
        services.AddDistributedMemoryCache();
        services.AddHttpClient();
        services.AddSingleton<FakeOpenAiHttpMessageHandler>();
        services.AddSingleton<IHttpClientFactory, FakeHttpClientFactory>();
        services.AddSingleton(sp => new HttpClient(sp.GetRequiredService<FakeOpenAiHttpMessageHandler>(), disposeHandler: false));
        services.AddSingleton<ITurnstileVerificationService, AllowAllTurnstileVerificationService>();
        services.AddSingleton<IEmailNotificationService, NoopEmailNotificationService>();

        // Add application services with mocks
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<AI.ProfilePhotoMaker.API.Services.IBasicTierService, AI.ProfilePhotoMaker.API.Services.BasicTierService>();
        services.AddScoped<AI.ProfilePhotoMaker.API.Services.IUserContextService, AI.ProfilePhotoMaker.API.Services.UserContextService>();
        services.AddScoped<AI.ProfilePhotoMaker.API.Data.IUserProfileRepository, AI.ProfilePhotoMaker.API.Data.UserProfileRepository>();
        services.AddScoped<AI.ProfilePhotoMaker.API.Services.IPendingGenerationService, AI.ProfilePhotoMaker.API.Services.PendingGenerationService>();
        services.AddScoped<IAdminService, AdminService>();
        services.AddScoped<ICreditPackageService, FakeCreditPackageService>();
        services.AddScoped<IStripePaymentService, FakeStripePaymentService>();
        services.AddScoped<ICouponService, FakeCouponService>();
        services.AddScoped<OpenAIImageGenerationService>(sp =>
            new OpenAIImageGenerationService(
                new HttpClient(sp.GetRequiredService<FakeOpenAiHttpMessageHandler>(), disposeHandler: false),
                sp.GetRequiredService<IHttpClientFactory>(),
                sp.GetRequiredService<IConfiguration>(),
                sp.GetRequiredService<ILogger<OpenAIImageGenerationService>>(),
                sp.GetRequiredService<IStorageService>()));

        // Add mock Replicate services
        services.AddScoped<IReplicateApiClient, MockReplicateApiClient>();
        services.AddScoped<AI.ProfilePhotoMaker.API.Services.IModelDiscoveryService, FakeModelDiscoveryService>();
        services.AddSingleton(new Replicate.ReplicateApi("r8_dummy_token_for_tests"));

        // Add polling services for testing (with faster intervals)
        services.AddScoped<ITrainingPollingService, TestTrainingPollingService>();
        // Don't add the background service in tests - it would interfere with test timing

        // Add fake services to satisfy dependencies
        services.AddScoped<IStorageService, FakeStorageService>();
        services.AddScoped<StoragePathResolver>();
        services.AddSingleton<AI.ProfilePhotoMaker.API.Services.Database.IDatabaseProviderService, FakeDatabaseProviderService>();

        services.AddOptions<StripeOptions>()
            .Configure(options =>
            {
                options.PublishableKey = "pk_test";
                options.SecretKey = "sk_test";
                options.WebhookSecret = "whsec_test";
            });
        services.AddOptions<PaymentSimulationOptions>()
            .Configure(options =>
            {
                options.Enabled = true;
                options.SkipStripeIntegration = true;
            });

        // Seed initial data after services are built
        var sp = services.BuildServiceProvider();
        using var scope = sp.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.Database.EnsureCreated();
    }

    private void ConfigureTestPipeline(IApplicationBuilder app)
    {
        // Add essential middleware for testing
        app.UseRouting();
        app.UseAuthentication();
        app.UseAuthorization();
        app.UseEndpoints(endpoints =>
        {
            endpoints.MapControllers();
        });
    }

    public HttpClient CreateAuthenticatedClient()
    {
        var client = CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return client;
    }

    /// <summary>
    /// Ensures the test user profile exists with sufficient credits
    /// Call this from tests that need fresh user setup
    /// </summary>
    public async Task EnsureTestUserWithCreditsAsync(string userId = "test-user-1", int credits = 100)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        // Remove any existing user profile to start fresh
        var existingProfile = await db.UserProfiles.FirstOrDefaultAsync(u => u.UserId == userId);
        if (existingProfile != null)
        {
            db.UserProfiles.Remove(existingProfile);
            await db.SaveChangesAsync();
        }

        // Add fresh user profile with credits
        db.UserProfiles.Add(new UserProfile
        {
            UserId = userId,
            Credits = credits,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            LastCreditReset = DateTime.UtcNow.AddDays(-8), // Ensure no weekly reset interferes
            SubscriptionTier = SubscriptionTier.Basic
        });

        await db.SaveChangesAsync();

        // Verify the data was saved correctly
        var savedProfile = await db.UserProfiles.FirstOrDefaultAsync(u => u.UserId == userId);
        if (savedProfile?.Credits != credits)
        {
            throw new InvalidOperationException($"Failed to set up test user credits. Expected: {credits}, Actual: {savedProfile?.Credits}");
        }
    }

    private sealed class FakeDatabaseProviderService : AI.ProfilePhotoMaker.API.Services.Database.IDatabaseProviderService
    {
        public void ConfigureDbContextOptions<TContext>(DbContextOptionsBuilder<TContext> options, string? connectionString = null) where TContext : DbContext
        {
            options.UseInMemoryDatabase("TestDb_SharedInstance");
        }

        public string GetConnectionString() => "DataSource=:memory:";

        public Task<bool> CanConnectAsync() => Task.FromResult(true);

        public AI.ProfilePhotoMaker.API.Services.Database.DatabaseProvider GetDatabaseProvider() => AI.ProfilePhotoMaker.API.Services.Database.DatabaseProvider.SQLite;

        public AI.ProfilePhotoMaker.API.Services.Database.DatabaseProviderConfig GetProviderConfig() => new()
        {
            Provider = AI.ProfilePhotoMaker.API.Services.Database.DatabaseProvider.SQLite,
            ConnectionString = GetConnectionString(),
            EnableDetailedErrors = true,
            EnableSensitiveDataLogging = true
        };
    }

    private sealed class AllowAllTurnstileVerificationService : ITurnstileVerificationService
    {
        public bool IsEnabled => false;

        public Task<bool> VerifyAsync(string? token, string? remoteIp)
        {
            return Task.FromResult(true);
        }
    }

    private sealed class NoopEmailNotificationService : IEmailNotificationService
    {
        public Task SendTrainingCompletedAsync(string userId, string? email, string? modelName, string? modelVersion) => Task.CompletedTask;
        public Task SendGenerationCompletedAsync(string userId, string? email, string? style, int imageCount, string? jobId = null) => Task.CompletedTask;
        public Task SendGenerationFailedAsync(string userId, string? email, string? style, string? error, string? jobId = null) => Task.CompletedTask;
        public Task SendPurchaseReceiptAsync(string userId, string? email, CreditPurchase purchase) => Task.CompletedTask;
        public Task SendSupportFeedbackReceivedAsync(string userId, string? userEmail, FeedbackSubmission submission) => Task.CompletedTask;
        public Task SendEmailVerificationAsync(string userId, string? email, string encodedToken) => Task.CompletedTask;
        public Task SendWelcomeAsync(string userId, string? email, string? firstName = null) => Task.CompletedTask;
        public Task SendRetentionDeletionWarningAsync(string userId, string? email, int imageCount, DateTime deletionDate, int daysUntilDeletion) => Task.CompletedTask;
        public Task SendAbandonedUploadNudgeAsync(string userId, string? email, string? firstName = null) => Task.CompletedTask;
    }

    private sealed class FakeStorageService : IStorageService
    {
        public Task<string> SaveImageAsync(Stream imageStream, string fileName, string userId, string folderType = "generated")
            => Task.FromResult($"https://fake-storage.com/{userId}/{folderType}/{fileName}");

        public Task<string> SaveImageToPathAsync(Stream imageStream, string storagePath)
            => Task.FromResult(storagePath);

        public Task<Stream?> GetImageAsync(string storagePath)
            => Task.FromResult<Stream?>(new MemoryStream());

        public Task<bool> DeleteImageAsync(string storagePath) => Task.FromResult(true);

        public Task<bool> ExistsAsync(string storagePath) => Task.FromResult(true);

        public string GetImageUrl(string storagePath) => $"https://fake-storage.com/{storagePath}";

        public Task<List<string>> ListUserImagesAsync(string userId)
            => Task.FromResult(new List<string> { $"https://fake-storage.com/{userId}/generated/sample.jpg" });

        public Task<StorageFileInfo?> GetFileInfoAsync(string storagePath)
            => Task.FromResult<StorageFileInfo?>(new StorageFileInfo
            {
                FileName = Path.GetFileName(storagePath),
                Size = 1024,
                CreatedAt = DateTime.UtcNow,
                ModifiedAt = DateTime.UtcNow,
                ContentType = "image/jpeg"
            });

        public Task<string> GenerateSasUrlAsync(string storagePath, TimeSpan expiry, Azure.Storage.Sas.BlobSasPermissions permissions = Azure.Storage.Sas.BlobSasPermissions.Read)
            => Task.FromResult($"https://fake-storage.com/{storagePath}?sas=fake-sas-token");

        public Task<string> SaveZipAsync(Stream zipStream, string storagePath)
            => Task.FromResult(storagePath);

        public Task<bool> DeleteDirectoryAsync(string directoryPath) => Task.FromResult(true);

        public Task<List<string>> ListFilesAsync(string prefix)
            => Task.FromResult(new List<string> { $"{prefix}/sample-file.jpg" });
    }

    private sealed class FakeCouponService : ICouponService
    {
        public Task<(bool IsValid, string Message, decimal DiscountAmount)> ValidateCouponAsync(string code, string userId, decimal originalPrice)
        {
            return Task.FromResult((false, "Coupon not available in test factory", 0m));
        }

        public Task<bool> RedeemCouponAsync(string code, string userId, decimal originalPrice, decimal discountApplied)
        {
            return Task.FromResult(true);
        }
    }

    private sealed class FakeModelDiscoveryService : AI.ProfilePhotoMaker.API.Services.IModelDiscoveryService
    {
        public Task<AI.ProfilePhotoMaker.API.Services.ModelSyncResult> DiscoverAndSyncUserModelsAsync(string userId)
            => Task.FromResult(new AI.ProfilePhotoMaker.API.Services.ModelSyncResult { Success = true, Message = "Skipped in tests" });

        public Task<AI.ProfilePhotoMaker.API.Services.ModelSyncStatus> GetModelSyncStatusAsync(string userId)
            => Task.FromResult(new AI.ProfilePhotoMaker.API.Services.ModelSyncStatus { UserId = userId, LastSyncAttempt = DateTime.UtcNow });

        public Task<AI.ProfilePhotoMaker.API.Services.ModelVersionRepairResult> RepairModelVersionsAsync(string userId)
            => Task.FromResult(new AI.ProfilePhotoMaker.API.Services.ModelVersionRepairResult { Success = true });

        public Task<bool> SyncSpecificModelAsync(string userId, string modelId, string versionId)
            => Task.FromResult(true);

        public Task<bool> OverrideModelDeletionAsync(string userId, string modelId, string reason, string overriddenBy)
            => Task.FromResult(true);

        public Task<AI.ProfilePhotoMaker.API.Services.QuickModelCheckResult> QuickDatabaseCheckAsync(string userId)
            => Task.FromResult(new AI.ProfilePhotoMaker.API.Services.QuickModelCheckResult { HasModel = false, LastSyncCheck = DateTime.UtcNow });
    }

    /// <summary>
    /// Test version of TrainingPollingService that works with MockReplicateApiClient
    /// and integrates with the test timing requirements
    /// </summary>
    private sealed class TestTrainingPollingService : ITrainingPollingService
    {
        private readonly ILogger<TestTrainingPollingService> _logger;
        private readonly ApplicationDbContext _dbContext;
        private readonly IReplicateApiClient _replicateApiClient;
        private readonly IServiceScopeFactory _serviceScopeFactory;

        public TestTrainingPollingService(
            ILogger<TestTrainingPollingService> logger,
            ApplicationDbContext dbContext,
            IReplicateApiClient replicateApiClient,
            IServiceScopeFactory serviceScopeFactory)
        {
            _logger = logger;
            _dbContext = dbContext;
            _replicateApiClient = replicateApiClient;
            _serviceScopeFactory = serviceScopeFactory;
        }

        public async Task StartPollingForTraining(string trainingId, string userId)
        {
            _logger.LogInformation("Starting test polling for training {TrainingId} for user {UserId}", trainingId, userId);

            // In tests, we can immediately check and process since MockReplicateApiClient
            // sets up completion after 300ms delay
            await Task.Delay(500); // Wait for mock completion

            if (await IsTrainingComplete(trainingId))
            {
                await ProcessTrainingCompletion(trainingId);
            }
        }

        public async Task<bool> IsTrainingComplete(string trainingId)
        {
            try
            {
                var status = await _replicateApiClient.GetTrainingStatusAsync(trainingId);
                var completed = status.IsCompleted ||
                               status.Status?.ToLower() == "succeeded" ||
                               status.Status?.ToLower() == "failed" ||
                               status.Status?.ToLower() == "canceled";

                _logger.LogInformation("Training {TrainingId} status: {Status}, completed: {Completed}",
                    trainingId, status.Status, completed);

                return completed;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking training status for {TrainingId}", trainingId);
                return false;
            }
        }

        public async Task ProcessTrainingCompletion(string trainingId)
        {
            using var scope = _serviceScopeFactory.CreateScope();
            var scopedDbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var scopedReplicateClient = scope.ServiceProvider.GetRequiredService<IReplicateApiClient>();

            try
            {
                _logger.LogInformation("Processing training completion for training {TrainingId}", trainingId);

                // Get the current training status from Replicate
                var trainingStatus = await scopedReplicateClient.GetTrainingStatusAsync(trainingId);

                // Find the model creation request by training ID
                var modelRequest = await scopedDbContext.ModelCreationRequests
                    .FirstOrDefaultAsync(r => r.PendingTrainingRequestId == trainingId);

                if (modelRequest == null)
                {
                    _logger.LogWarning("No model creation request found for training ID {TrainingId}", trainingId);
                    return;
                }

                _logger.LogInformation("Found model creation request {RequestId} for training {TrainingId}",
                    modelRequest.Id, trainingId);

                // Update the model creation request based on training status
                if (trainingStatus.Status?.ToLower() == "succeeded")
                {
                    _logger.LogInformation("Training {TrainingId} completed successfully", trainingId);

                    // Extract the trained model version from the training result
                    var trainedModelVersion = trainingStatus.Version;
                    if (string.IsNullOrEmpty(trainedModelVersion))
                    {
                        _logger.LogError("Training completed but no model version found in response for training {TrainingId}", trainingId);
                        modelRequest.Status = ModelCreationStatus.Failed;
                        modelRequest.ErrorMessage = "Training completed but no model version found in response";
                        modelRequest.CompletedAt = DateTime.UtcNow;
                    }
                    else
                    {
                        // Update model creation request to Ready status
                        modelRequest.Status = ModelCreationStatus.Ready;
                        modelRequest.TrainedModelVersion = trainedModelVersion;
                        modelRequest.CompletedAt = DateTime.UtcNow;
                        modelRequest.ErrorMessage = null;

                        _logger.LogInformation("Model creation request {RequestId} marked as Ready with version {Version}",
                            modelRequest.Id, trainedModelVersion);
                    }
                }
                else if (trainingStatus.Status?.ToLower() == "failed" || trainingStatus.Status?.ToLower() == "canceled")
                {
                    _logger.LogWarning("Training {TrainingId} failed or was canceled with status {Status}", trainingId, trainingStatus.Status);

                    modelRequest.Status = ModelCreationStatus.Failed;
                    modelRequest.ErrorMessage = trainingStatus.Error ?? $"Training failed with status: {trainingStatus.Status}";
                    modelRequest.CompletedAt = DateTime.UtcNow;
                }
                else
                {
                    _logger.LogInformation("Training {TrainingId} not yet complete, status: {Status}", trainingId, trainingStatus.Status);
                    return; // Training still in progress, don't save changes
                }

                // Save the updated model creation request
                await scopedDbContext.SaveChangesAsync();

                _logger.LogInformation("Successfully processed training completion for {TrainingId}, status: {Status}",
                    trainingId, modelRequest.Status);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing training completion for {TrainingId}", trainingId);
            }
        }
    }
}
