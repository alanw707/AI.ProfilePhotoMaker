using AI.ProfilePhotoMaker.API.Data;
using AI.ProfilePhotoMaker.API.Models;
using AI.ProfilePhotoMaker.API.Models.DTOs;
using AI.ProfilePhotoMaker.API.Services;
using AI.ProfilePhotoMaker.API.Services.ImageProcessing;
using AI.ProfilePhotoMaker.API.Services.Storage;
using Azure.Storage.Sas;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Data.Sqlite;
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
        Assert.StartsWith("prompt-v1", image.PromptVersion);
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
    public async Task GenerateHeadshotAsync_AppliesUseCaseRecipesDeterministically()
    {
        using var context = CreateContext();
        var userId = await SeedUserProfileAsync(context, credits: 6);
        var provider = new FakeHeadshotProvider("data:image/png;base64," + Convert.ToBase64String([1, 2, 3]));
        var service = CreateService(context, new FakeStorageService(), provider);
        var request = new HeadshotGenerationRequestDto
        {
            ImageStoragePath = $"dev/enhanced/{userId}/source.png",
            Style = "linkedin",
            Background = "auto",
            PackageCode = "starter_package",
            NumOutputs = 3,
            UseCaseCode = "realtor",
            ClientRequestId = "realtor-recipes"
        };

        var response = await service.GenerateHeadshotAsync(request, userId);

        Assert.Equal(3, provider.Requests.Count);
        Assert.Equal(new[] { "realtor_trust", "luxury_listing", "social_flyer" }, provider.Requests.Select(r => r.RecipeCode));
        Assert.All(provider.Requests, r => Assert.Equal("realtor", r.UseCaseCode));
        Assert.Contains("real-estate", provider.Requests[0].PromptTemplate);
        Assert.Equal(new[] { "Best Zillow/Realtor profile", "Best luxury listing vibe", "Best social flyer image" }, response.Candidates.Select(c => c.Label));
    }

    [Fact]
    public async Task GenerateHeadshotAsync_DoesNotApplyUseCaseRecipeToFreePreview()
    {
        using var context = CreateContext();
        var userId = await SeedUserProfileAsync(context, credits: 6);
        var provider = new FakeHeadshotProvider("data:image/png;base64," + Convert.ToBase64String([1, 2, 3]));
        var service = CreateService(context, new FakeStorageService(), provider);

        var response = await service.GenerateHeadshotAsync(new HeadshotGenerationRequestDto
        {
            ImageStoragePath = $"dev/enhanced/{userId}/source.png",
            Style = "linkedin",
            Background = "auto",
            PackageCode = "free_preview",
            NumOutputs = 1,
            UseCaseCode = "realtor",
            ClientRequestId = "free-preview-no-recipe"
        }, userId);

        Assert.Single(provider.Requests);
        Assert.Equal("realtor", provider.Requests[0].UseCaseCode);
        Assert.Equal(string.Empty, provider.Requests[0].RecipeCode);
        Assert.DoesNotContain("Use-case recipe:", provider.Requests[0].PromptTemplate);
        Assert.Null(response.Candidates[0].RecipeCode);
        Assert.Null(response.Candidates[0].Label);
    }

    [Fact]
    public async Task GenerateHeadshotAsync_ReturnsExistingMultiCandidateResultForDuplicateClientRequestWithoutConsumingCreditsAgain()
    {
        using var context = CreateContext();
        var userId = await SeedUserProfileAsync(context, credits: 6);
        var sourcePath = $"dev/enhanced/{userId}/source.png";
        var provider = new FakeHeadshotProvider("data:image/png;base64," + Convert.ToBase64String([1, 2, 3]));
        var service = CreateService(context, new FakeStorageService(), provider);
        var request = new HeadshotGenerationRequestDto
        {
            ImageStoragePath = sourcePath,
            Style = "professional",
            Background = "auto",
            PackageCode = "starter_package",
            NumOutputs = 2,
            ClientRequestId = "multi-candidate-retry"
        };

        var first = await service.GenerateHeadshotAsync(request, userId);
        var second = await service.GenerateHeadshotAsync(request, userId);

        Assert.Equal(2, first.Candidates.Count);
        Assert.Equal(2, second.Candidates.Count);
        Assert.Equal(first.Candidates.Select(c => c.ProcessedImageId), second.Candidates.Select(c => c.ProcessedImageId));
        Assert.Equal(0, second.CreditsCost);
        Assert.Equal(4, second.RemainingCredits);
        Assert.Equal(2, provider.CallCount);
        var profile = await context.UserProfiles.SingleAsync(p => p.UserId == userId);
        Assert.Equal(4, profile.Credits);
        Assert.Equal(2, context.ProcessedImages.Count());
        Assert.Single(context.UsageLogs.Where(l => l.Action == "instant_headshot_generation"));
    }

    [Theory]
    [InlineData(2, true)]
    [InlineData(3, false)]
    public async Task GenerateHeadshotAsync_ReturnsPromotedPreviewAndGeneratedCandidatesForDuplicatePaidContinuation(int newCandidateCount, bool fitsPackage)
    {
        using var context = CreateContext();
        var userId = await SeedUserProfileAsync(context, credits: 6);
        var profile = await context.UserProfiles.SingleAsync(p => p.UserId == userId);
        var sourcePath = $"dev/uploads/{userId}/source.png";
        var preview = new ProcessedImage
        {
            OriginalImageUrl = sourcePath,
            ProcessedImageUrl = $"dev/generated/{userId}/preview-watermarked.png",
            Style = "linkedin",
            UserProfileId = profile.Id,
            CreatedAt = DateTime.UtcNow,
            IsGenerated = true,
            IsOriginalUpload = false,
            Provider = "openai",
            ProviderModel = "gpt-image-2",
            GenerationMode = "instant_headshot",
            PromptVersion = "prompt-v1",
            CreditCost = 0,
            GenerationStatus = "succeeded",
            CorrelationId = "free-preview-correlation",
            FailureReason = $"raw-preview:dev/generated-private/{userId}/preview-raw.png"
        };
        context.ProcessedImages.Add(preview);
        await context.SaveChangesAsync();

        var provider = new FakeHeadshotProvider("data:image/png;base64," + Convert.ToBase64String([1, 2, 3]));
        var entitlement = new UserPackageEntitlement
        {
            UserId = userId,
            RemainingPackageUses = 1,
            RemainingCandidates = 3,
            RemainingRefinements = 1,
            RemainingPremiumAugmentations = 1,
            Status = PackageEntitlementStatus.Active
        };
        var service = CreateService(context, new FakeStorageService(), provider, new FakeOutcomePackageService(entitlement));
        var request = new HeadshotGenerationRequestDto
        {
            ImageStoragePath = sourcePath,
            Style = "linkedin",
            Background = "auto",
            PackageCode = "starter_package",
            NumOutputs = newCandidateCount,
            ClientRequestId = "paid-continuation-retry",
            ReusedPreviewProcessedImageId = preview.Id,
            ReusedPreviewSourcePath = sourcePath,
            ReusedPreviewStyle = "linkedin"
        };

        if (!fitsPackage)
        {
            await Assert.ThrowsAsync<HeadshotGenerationException>(() => service.GenerateHeadshotAsync(request, userId));
            Assert.Equal(0, provider.CallCount);
            Assert.Equal(3, entitlement.RemainingCandidates);
            return;
        }

        var first = await service.GenerateHeadshotAsync(request, userId);
        var second = await service.GenerateHeadshotAsync(request, userId);

        Assert.Equal(3, first.Candidates.Count);
        Assert.Equal(3, second.Candidates.Count);
        Assert.Equal(first.Candidates.Select(c => c.ProcessedImageId), second.Candidates.Select(c => c.ProcessedImageId));
        Assert.Equal(0, second.CreditsCost);
        Assert.Equal("dev/generated-private/" + userId + "/preview-raw.png", first.Candidates[0].StoragePath);
        Assert.Equal(2, provider.CallCount);
        Assert.Equal(0, entitlement.RemainingPackageUses);
        Assert.Equal(0, entitlement.RemainingCandidates);
        Assert.Single(context.ProcessedImages.Where(i => i.GenerationMode == "instant_headshot_promoted_preview"));
        Assert.Equal(3, context.ProcessedImages.Count(i => i.GenerationStatus == "succeeded" && (i.GenerationMode == "instant_headshot" || i.GenerationMode == "instant_headshot_promoted_preview")) - 1);
    }

    [Fact]
    public async Task GenerateHeadshotAsync_DoesNotReturnPersistedCandidatesWhenPackageConsumptionFails()
    {
        using var context = CreateContext();
        var userId = await SeedUserProfileAsync(context, credits: 6);
        var provider = new FakeHeadshotProvider("data:image/png;base64," + Convert.ToBase64String([1, 2, 3]));
        var entitlement = new UserPackageEntitlement
        {
            UserId = userId,
            RemainingPackageUses = 1,
            RemainingCandidates = 2,
            Status = PackageEntitlementStatus.Active
        };
        var packageService = new FakeOutcomePackageService(entitlement) { FailConsumeCandidates = true };
        var service = CreateService(context, new FakeStorageService(), provider, packageService);
        var request = new HeadshotGenerationRequestDto
        {
            ImageStoragePath = $"dev/uploads/{userId}/source.png",
            Style = "linkedin",
            Background = "auto",
            PackageCode = "starter_package",
            NumOutputs = 2,
            ClientRequestId = "package-consumption-race"
        };

        var first = await Assert.ThrowsAsync<HeadshotGenerationException>(() => service.GenerateHeadshotAsync(request, userId));
        var second = await Assert.ThrowsAsync<HeadshotGenerationException>(() => service.GenerateHeadshotAsync(request, userId));

        Assert.Equal("PackageEntitlementRequired", first.Code);
        Assert.Equal("PackageEntitlementRequired", second.Code);
        Assert.Equal(4, provider.CallCount);
        Assert.Empty(context.ProcessedImages.Where(i => i.GenerationStatus == "succeeded"));
        Assert.All(context.ProcessedImages, i => Assert.Equal("package-entitlement-consumption-failed", i.FailureReason));
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
    public async Task GenerateHeadshotAsync_MarksPartialBatchFailedAndRefundsCredits()
    {
        using var context = CreateContext();
        var userId = await SeedUserProfileAsync(context, credits: 3);
        var provider = new FakeHeadshotProvider("data:image/png;base64,AQID", failOnCall: 2);
        var service = CreateService(context, new FakeStorageService(), provider);

        var ex = await Assert.ThrowsAsync<HeadshotGenerationException>(() =>
            service.GenerateHeadshotAsync(new HeadshotGenerationRequestDto
            {
                ImageStoragePath = $"dev/enhanced/{userId}/source.png",
                Style = "professional",
                PackageCode = "starter_package",
                NumOutputs = 2,
                ClientRequestId = "partial-batch"
            }, userId));

        Assert.Equal("ProviderGenerationFailed", ex.Code);
        Assert.Equal(3, (await context.UserProfiles.SingleAsync(p => p.UserId == userId)).Credits);
        var persisted = await context.ProcessedImages.SingleAsync();
        Assert.Equal("failed", persisted.GenerationStatus);
        Assert.Equal("generation-incomplete", persisted.FailureReason);
    }

    [Fact]
    public async Task GenerateHeadshotAsync_ConcurrentDuplicateAcrossContextsRunsProviderAndAccountingOnce()
    {
        var databaseName = $"generation-{Guid.NewGuid():N}";
        var connectionString = $"Data Source={databaseName};Mode=Memory;Cache=Shared;Default Timeout=30";
        await using var keeper = new SqliteConnection(connectionString);
        await keeper.OpenAsync();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connectionString)
            .Options;
        string userId;
        await using (var setup = new ApplicationDbContext(options))
        {
            await setup.Database.EnsureCreatedAsync();
            userId = await SeedUserProfileAsync(setup, credits: 3);
        }

        var provider = new BlockingHeadshotProvider();
        var request = new HeadshotGenerationRequestDto
        {
            ImageStoragePath = $"dev/enhanced/{userId}/source.png",
            Style = "professional",
            ClientRequestId = "cross-replica-request"
        };

        await using var firstContext = new ApplicationDbContext(options);
        var first = CreateService(firstContext, new FakeStorageService(), provider)
            .GenerateHeadshotAsync(request, userId);
        await provider.Started.WaitAsync(TimeSpan.FromSeconds(10));

        await using (var secondContext = new ApplicationDbContext(options))
        {
            var duplicateTask = CreateService(secondContext, new FakeStorageService(), provider)
                .GenerateHeadshotAsync(new HeadshotGenerationRequestDto
                {
                    ImageStoragePath = request.ImageStoragePath,
                    Style = "professional",
                    ClientRequestId = request.ClientRequestId
                }, userId);
            await Task.WhenAny(duplicateTask, Task.Delay(TimeSpan.FromSeconds(2)));
            provider.Release();
            var duplicate = await Assert.ThrowsAsync<HeadshotGenerationException>(() => duplicateTask);
            Assert.Equal("GenerationInProgress", duplicate.Code);
        }

        var firstResult = await first;

        await using var retryContext = new ApplicationDbContext(options);
        var retryResult = await CreateService(retryContext, new FakeStorageService(), provider)
            .GenerateHeadshotAsync(new HeadshotGenerationRequestDto
            {
                ImageStoragePath = request.ImageStoragePath,
                Style = "professional",
                ClientRequestId = request.ClientRequestId
            }, userId);

        Assert.Equal(firstResult.ProcessedImageId, retryResult.ProcessedImageId);
        Assert.Equal(0, retryResult.CreditsCost);
        Assert.Equal(1, provider.CallCount);
        Assert.Equal(2, (await retryContext.UserProfiles.SingleAsync(p => p.UserId == userId)).Credits);
        Assert.Single(await retryContext.HeadshotGenerationOperations
            .Where(o => o.Status == HeadshotGenerationOperationStatus.Succeeded)
            .ToListAsync());
        Assert.Single(await retryContext.UsageLogs
            .Where(l => l.Action == "instant_headshot_generation")
            .ToListAsync());
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
        IHeadshotGenerationProvider provider,
        IOutcomePackageService? outcomePackageService = null)
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
            NullLogger<HeadshotGenerationService>.Instance,
            outcomePackageService);
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
        if (!await context.Styles.AnyAsync(s => s.Name == "linkedin"))
        {
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
        }
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
        private readonly int? _failOnCall;

        public FakeHeadshotProvider(string? output, bool success = true, int? failOnCall = null)
        {
            _output = output;
            _success = success;
            _failOnCall = failOnCall;
        }

        public string ProviderName => "openai";
        public string ModelName => "gpt-image-2";

        public int CallCount { get; private set; }
        public List<HeadshotGenerationRequest> Requests { get; } = new();

        public Task<HeadshotGenerationResult> GenerateAsync(HeadshotGenerationRequest request, CancellationToken cancellationToken = default)
        {
            CallCount++;
            Requests.Add(request);
            var success = _success && CallCount != _failOnCall;
            return Task.FromResult(new HeadshotGenerationResult
            {
                Success = success,
                DataUrlOrUrl = _output ?? string.Empty,
                Provider = ProviderName,
                Model = ModelName,
                PromptVersion = "prompt-v1",
                FailureCode = success ? null : "ProviderGenerationFailed",
                FailureMessage = success ? null : "provider failed"
            });
        }
    }

    private sealed class BlockingHeadshotProvider : IHeadshotGenerationProvider
    {
        private readonly TaskCompletionSource _started = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _callCount;

        public string ProviderName => "fixture";
        public string ModelName => "deterministic";
        public int CallCount => _callCount;
        public Task Started => _started.Task;

        public void Release() => _release.TrySetResult();

        public async Task<HeadshotGenerationResult> GenerateAsync(
            HeadshotGenerationRequest request,
            CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _callCount);
            _started.TrySetResult();
            await _release.Task.WaitAsync(cancellationToken);
            return new HeadshotGenerationResult
            {
                Success = true,
                DataUrlOrUrl = "data:image/png;base64,AQID",
                Provider = ProviderName,
                Model = ModelName,
                PromptVersion = "fixture-v1"
            };
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

    private sealed class FakeOutcomePackageService : IOutcomePackageService
    {
        private readonly UserPackageEntitlement _entitlement;

        public FakeOutcomePackageService(UserPackageEntitlement entitlement)
        {
            _entitlement = entitlement;
        }

        public Task<IReadOnlyList<OutcomePackageDefinitionDto>> GetActivePackageDefinitionsAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<OutcomePackageDefinitionDto>>(Array.Empty<OutcomePackageDefinitionDto>());
        public Task<IReadOnlyList<UserPackageEntitlementDto>> GetUserEntitlementsAsync(string userId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<UserPackageEntitlementDto>>(Array.Empty<UserPackageEntitlementDto>());
        public Task<UserPackageEntitlement?> GrantEntitlementForCreditPackageAsync(string userId, int creditPackageId, string? paymentTransactionId, CancellationToken cancellationToken = default) => Task.FromResult<UserPackageEntitlement?>(_entitlement);
        public Task<UserPackageEntitlement?> GetActiveEntitlementAsync(string userId, string packageCode, CancellationToken cancellationToken = default) => Task.FromResult(_entitlement.RemainingPackageUses > 0 || _entitlement.RemainingCandidates > 0 ? _entitlement : null);
        public bool FailConsumeCandidates { get; init; }

        public Task<bool> ConsumeCandidatesAsync(string userId, string packageCode, int candidateCount, CancellationToken cancellationToken = default)
        {
            if (FailConsumeCandidates) return Task.FromResult(false);
            if (_entitlement.RemainingPackageUses <= 0 || _entitlement.RemainingCandidates < candidateCount) return Task.FromResult(false);
            _entitlement.RemainingPackageUses = Math.Max(0, _entitlement.RemainingPackageUses - 1);
            _entitlement.RemainingCandidates -= candidateCount;
            return Task.FromResult(true);
        }
        public Task<bool> ConsumeRefinementAsync(string userId, string? packageCode = null, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task<bool> ConsumePremiumAugmentationAsync(string userId, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task<bool> ConsumeExportKitAsync(string userId, CancellationToken cancellationToken = default) => Task.FromResult(false);
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
