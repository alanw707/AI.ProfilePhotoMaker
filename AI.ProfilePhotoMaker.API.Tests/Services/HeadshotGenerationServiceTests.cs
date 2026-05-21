using AI.ProfilePhotoMaker.API.Data;
using AI.ProfilePhotoMaker.API.Models;
using AI.ProfilePhotoMaker.API.Models.DTOs;
using AI.ProfilePhotoMaker.API.Services;
using AI.ProfilePhotoMaker.API.Services.ImageProcessing;
using AI.ProfilePhotoMaker.API.Services.Storage;
using Azure.Storage.Sas;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace AI.ProfilePhotoMaker.API.Tests.Services;

public class HeadshotGenerationServiceTests
{
    [Fact]
    public async Task GenerateHeadshotAsync_StoresGeneratedImageMetadataAndConsumesCredits()
    {
        using var context = CreateContext();
        var userId = await SeedUserProfileAsync(context, credits: 3);
        var sourcePath = $"dev/enhanced/{userId}/source.png";
        var storage = new FakeStorageService();
        var provider = new FakeHeadshotProvider("data:image/png;base64," + Convert.ToBase64String([1, 2, 3]));
        var service = CreateService(context, storage, provider);

        var result = await service.GenerateHeadshotAsync(new HeadshotGenerationRequestDto
        {
            ImageStoragePath = sourcePath,
            Style = "professional",
            Background = "auto",
            ClientRequestId = "request-123"
        }, userId);

        Assert.True(result.Success);
        Assert.Equal("openai", result.Provider);
        Assert.Equal("gpt-image-2", result.Model);
        Assert.Equal(1, result.CreditsCost);
        Assert.Equal(2, result.RemainingCredits);
        Assert.Contains($"dev/generated/{userId}/", result.StoragePath);

        var image = await context.ProcessedImages.SingleAsync(i => i.Id == result.ProcessedImageId);
        Assert.Equal(sourcePath, image.OriginalImageUrl);
        Assert.Equal(result.StoragePath, image.ProcessedImageUrl);
        Assert.True(image.IsGenerated);
        Assert.False(image.IsOriginalUpload);
        Assert.Equal("openai", image.Provider);
        Assert.Equal("gpt-image-2", image.ProviderModel);
        Assert.Equal("instant_headshot", image.GenerationMode);
        Assert.Equal("prompt-v1", image.PromptVersion);
        Assert.Equal(1, image.CreditCost);
        Assert.Equal("succeeded", image.GenerationStatus);
        Assert.StartsWith("instant_headshot_generation:", image.CorrelationId);
        Assert.Equal(image.CorrelationId, result.CorrelationId);
    }

    [Fact]
    public async Task GenerateHeadshotAsync_ReturnsExistingResultForDuplicateClientRequestWithoutConsumingCreditsAgain()
    {
        using var context = CreateContext();
        var userId = await SeedUserProfileAsync(context, credits: 3);
        var sourcePath = $"dev/enhanced/{userId}/source.png";
        var storage = new FakeStorageService();
        var provider = new FakeHeadshotProvider("data:image/png;base64," + Convert.ToBase64String([1, 2, 3]));
        var service = CreateService(context, storage, provider);
        var request = new HeadshotGenerationRequestDto
        {
            ImageStoragePath = sourcePath,
            Style = "professional",
            Background = "auto",
            ClientRequestId = "retry-safe-request"
        };

        var first = await service.GenerateHeadshotAsync(request, userId);
        var second = await service.GenerateHeadshotAsync(request, userId);

        Assert.Equal(first.ProcessedImageId, second.ProcessedImageId);
        Assert.Equal(first.CorrelationId, second.CorrelationId);
        Assert.Equal(0, second.CreditsCost);
        Assert.Equal(2, second.RemainingCredits);
        var profile = await context.UserProfiles.SingleAsync(p => p.UserId == userId);
        Assert.Equal(2, profile.Credits);
        Assert.Single(context.ProcessedImages);
        Assert.Single(context.UsageLogs.Where(l => l.Action == "instant_headshot_generation"));
    }

    [Fact]
    public async Task GenerateHeadshotAsync_ReturnsExistingResultForDuplicateClientRequestAfterCreditsAreSpent()
    {
        using var context = CreateContext();
        var userId = await SeedUserProfileAsync(context, credits: 1);
        var sourcePath = $"dev/enhanced/{userId}/source.png";
        var service = CreateService(
            context,
            new FakeStorageService(),
            new FakeHeadshotProvider("data:image/png;base64," + Convert.ToBase64String([1, 2, 3])));
        var request = new HeadshotGenerationRequestDto
        {
            ImageStoragePath = sourcePath,
            Style = "professional",
            Background = "auto",
            ClientRequestId = "spent-last-credit-retry"
        };

        var first = await service.GenerateHeadshotAsync(request, userId);
        var second = await service.GenerateHeadshotAsync(request, userId);

        Assert.Equal(first.ProcessedImageId, second.ProcessedImageId);
        Assert.Equal(first.CorrelationId, second.CorrelationId);
        Assert.Equal(0, second.CreditsCost);
        Assert.Equal(0, second.RemainingCredits);
        var profile = await context.UserProfiles.SingleAsync(p => p.UserId == userId);
        Assert.Equal(0, profile.Credits);
        Assert.Single(context.ProcessedImages);
    }

    [Fact]
    public async Task GenerateHeadshotAsync_RefundsCreditsWhenProviderFails()
    {
        using var context = CreateContext();
        var userId = await SeedUserProfileAsync(context, credits: 3);
        var sourcePath = $"dev/enhanced/{userId}/source.png";
        var storage = new FakeStorageService();
        var provider = new FakeHeadshotProvider(null, success: false);
        var service = CreateService(context, storage, provider);

        var ex = await Assert.ThrowsAsync<HeadshotGenerationException>(() =>
            service.GenerateHeadshotAsync(new HeadshotGenerationRequestDto
            {
                ImageStoragePath = sourcePath,
                Style = "professional",
                Background = "auto"
            }, userId));

        Assert.Equal("ProviderGenerationFailed", ex.Code);
        var profile = await context.UserProfiles.SingleAsync(p => p.UserId == userId);
        Assert.Equal(3, profile.Credits);
        Assert.Empty(context.ProcessedImages);
        Assert.Contains(context.UsageLogs, l => l.Action == "instant_headshot_generation_refund");
    }

    [Fact]
    public async Task GenerateHeadshotAsync_RejectsStoragePathOutsideCurrentUserPrefixes()
    {
        using var context = CreateContext();
        var userId = await SeedUserProfileAsync(context, credits: 3);
        var otherUserPath = "dev/enhanced/other-user/source.png";
        var service = CreateService(context, new FakeStorageService(), new FakeHeadshotProvider("data:image/png;base64,AQID"));

        var ex = await Assert.ThrowsAsync<HeadshotGenerationException>(() =>
            service.GenerateHeadshotAsync(new HeadshotGenerationRequestDto
            {
                ImageStoragePath = otherUserPath
            }, userId));

        Assert.Equal("InvalidImageSource", ex.Code);
    }

    private static HeadshotGenerationService CreateService(
        ApplicationDbContext context,
        IStorageService storage,
        IHeadshotGenerationProvider provider)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Storage:EnvironmentPrefix"] = "dev"
            })
            .Build();
        var env = new FakeWebHostEnvironment();
        var resolver = new StoragePathResolver(env, config, NullLogger<StoragePathResolver>.Instance);
        return new HeadshotGenerationService(
            context,
            new BasicTierService(context, NullLogger<BasicTierService>.Instance),
            provider,
            storage,
            resolver,
            new FakeHttpClientFactory(),
            config,
            NullLogger<HeadshotGenerationService>.Instance);
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(options);
    }

    private static async Task<string> SeedUserProfileAsync(ApplicationDbContext context, int credits)
    {
        var userId = Guid.NewGuid().ToString();
        var user = new ApplicationUser
        {
            Id = userId,
            UserName = $"user_{userId}",
            NormalizedUserName = $"USER_{userId}",
            Email = $"{userId}@example.com",
            NormalizedEmail = $"{userId}@EXAMPLE.COM"
        };

        context.Users.Add(user);
        context.Styles.Add(new Style
        {
            Name = "linkedin",
            Description = "LinkedIn professional style",
            PromptTemplate = "professional portrait of a {gender} {ethnicity}, {subject}, clean neutral background, confident approachable expression",
            NegativePromptTemplate = "distorted face, unrealistic features",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        context.UserProfiles.Add(new UserProfile
        {
            UserId = userId,
            User = user,
            Credits = credits,
            LastCreditReset = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();
        return userId;
    }

    private sealed class FakeHeadshotProvider : IHeadshotGenerationProvider
    {
        private readonly string? _output;
        private readonly bool _success;

        public FakeHeadshotProvider(string? output, bool success = true)
        {
            _output = output;
            _success = success;
        }

        public string ProviderName => "openai";
        public string ModelName => "gpt-image-2";

        public Task<HeadshotGenerationResult> GenerateAsync(HeadshotGenerationRequest request, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new HeadshotGenerationResult
            {
                Success = _success,
                DataUrlOrUrl = _output ?? string.Empty,
                Provider = ProviderName,
                Model = ModelName,
                PromptVersion = "prompt-v1",
                FailureCode = _success ? null : "ProviderGenerationFailed",
                FailureMessage = _success ? null : "provider failed"
            });
        }
    }

    private sealed class FakeStorageService : IStorageService
    {
        public Task<string> SaveImageAsync(Stream imageStream, string fileName, string userId, string folderType = "generated") =>
            Task.FromResult($"dev/{folderType}/{userId}/{fileName}");

        public Task<string> SaveImageToPathAsync(Stream imageStream, string storagePath) => Task.FromResult(storagePath);
        public Task<Stream?> GetImageAsync(string storagePath) => Task.FromResult<Stream?>(new MemoryStream([1, 2, 3]));
        public Task<bool> DeleteImageAsync(string storagePath) => Task.FromResult(true);
        public Task<bool> ExistsAsync(string storagePath) => Task.FromResult(true);
        public string GetImageUrl(string storagePath) => $"https://cdn.example.test/{storagePath}";
        public Task<List<string>> ListUserImagesAsync(string userId) => Task.FromResult(new List<string>());
        public Task<StorageFileInfo?> GetFileInfoAsync(string storagePath) => Task.FromResult<StorageFileInfo?>(null);
        public Task<string> GenerateSasUrlAsync(string storagePath, TimeSpan expiry, BlobSasPermissions permissions = BlobSasPermissions.Read) => Task.FromResult(GetImageUrl(storagePath));
        public Task<string> SaveZipAsync(Stream zipStream, string storagePath) => Task.FromResult(storagePath);
        public Task<bool> DeleteDirectoryAsync(string directoryPath) => Task.FromResult(true);
        public Task<List<string>> ListFilesAsync(string prefix) => Task.FromResult(new List<string>());
    }

    private sealed class FakeHttpClientFactory : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new();
    }

    private sealed class FakeWebHostEnvironment : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "Tests";
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string WebRootPath { get; set; } = string.Empty;
        public string EnvironmentName { get; set; } = "Development";
        public string ContentRootPath { get; set; } = Directory.GetCurrentDirectory();
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
