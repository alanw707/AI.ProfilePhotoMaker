using System.Text.Json;
using AI.ProfilePhotoMaker.API.Data;
using AI.ProfilePhotoMaker.API.Models;
using AI.ProfilePhotoMaker.API.Models.DTOs;
using AI.ProfilePhotoMaker.API.Models.Replicate;

namespace AI.ProfilePhotoMaker.API.Services.ImageProcessing;

public class MockReplicateApiClient : IReplicateApiClient
{
    private static readonly Dictionary<string, ReplicateTrainingResult> Trainings = new();
    private static readonly Dictionary<string, ReplicatePredictionResult> Predictions = new();
    private readonly ApplicationDbContext _context;
    private readonly ILogger<MockReplicateApiClient> _logger;

    public MockReplicateApiClient(ApplicationDbContext context, ILogger<MockReplicateApiClient> logger)
    {
        _context = context;
        _logger = logger;
    }

    public Task<string> CreateModelAsync(string userId, string modelName, string? description = null)
    {
        var full = $"mock/{modelName}";
        _logger.LogInformation("[Mock] CreateModel => {Model}", full);
        return Task.FromResult(full);
    }

    public async Task<ReplicateTrainingResult> CreateModelTrainingAsync(string userId, string imageZipUrl)
    {
        var modelName = $"user-{userId}-{DateTime.UtcNow:yyyyMMddHHmmss}";
        var destination = await CreateModelAsync(userId, modelName);

        var request = new ModelCreationRequest
        {
            UserId = userId,
            ModelName = modelName,
            ReplicateModelId = destination,
            Status = ModelCreationStatus.Creating,
            TrainingImageZipUrl = imageZipUrl,
            PendingTrainingRequestId = Guid.NewGuid().ToString(),
            CreatedAt = DateTime.UtcNow
        };
        _context.ModelCreationRequests.Add(request);
        await _context.SaveChangesAsync();

        var training = new ReplicateTrainingResult
        {
            Id = request.PendingTrainingRequestId,
            Status = "starting",
            CreatedAt = DateTime.UtcNow,
            Version = null,
            Urls = new ReplicateUrls { Get = $"/mock/trainings/{request.PendingTrainingRequestId}" }
        };
        Trainings[training.Id!] = training;

        // Set up completion progression for polling
        _ = Task.Run(async () =>
        {
            await Task.Delay(100); // First transition to processing
            var tr = Trainings[training.Id!];
            tr.Status = "processing";
            Trainings[training.Id!] = tr;

            await Task.Delay(200); // Then complete
            tr = Trainings[training.Id!];
            tr.Status = "succeeded";
            tr.CompletedAt = DateTime.UtcNow;
            tr.Version = $"{request.ReplicateModelId}:mock-version";
            Trainings[training.Id!] = tr;

            _logger.LogInformation("[Mock] Training {TrainingId} completed with version {Version}",
                training.Id, tr.Version);
        });

        return training;
    }

    public Task<ReplicateTrainingResult> CreateModelTrainingWithDestinationAsync(string userId, string imageZipUrl, string destination)
    {
        // For simplicity, delegate to CreateModelTrainingAsync which creates destination internally
        return CreateModelTrainingAsync(userId, imageZipUrl);
    }

    public async Task<string> InitiateModelCreationAndTrainingAsync(string userId, string imageZipUrl)
    {
        var result = await CreateModelTrainingAsync(userId, imageZipUrl);
        return result.Id ?? string.Empty;
    }

    public Task<ReplicateTrainingResult> GetTrainingStatusAsync(string trainingId)
    {
        if (Trainings.TryGetValue(trainingId, out var tr))
        {
            _logger.LogInformation("[Mock] GetTrainingStatus {TrainingId} => {Status}", trainingId, tr.Status);
            return Task.FromResult(tr);
        }

        // Unknown id: return succeeded for backwards compatibility
        _logger.LogWarning("[Mock] GetTrainingStatus unknown id {TrainingId}, returning succeeded", trainingId);
        return Task.FromResult(new ReplicateTrainingResult
        {
            Id = trainingId,
            Status = "succeeded",
            CreatedAt = DateTime.UtcNow.AddSeconds(-1),
            CompletedAt = DateTime.UtcNow,
            Version = "mock/unknown:mock-version",
        });
    }

    public async Task<ReplicatePredictionResult> GenerateImagesAsync(string trainedModelVersion, string userId, string style, UserInfo? userInfo = null, int numOutputs = 2)
    {
        var id = Guid.NewGuid().ToString();
        var result = new ReplicatePredictionResult
        {
            Id = id,
            Version = trainedModelVersion,
            Status = "starting",
            CreatedAt = DateTime.UtcNow,
            Input = new Dictionary<string, object> { { "user_id", userId }, { "style", style } },
            Urls = new ReplicateUrls { Get = $"/mock/predictions/{id}" }
        };
        Predictions[id] = result;

        // Persist ownership
        _context.Predictions.Add(new Prediction { Id = id, UserId = userId, Style = style, CreatedAt = DateTime.UtcNow });
        await _context.SaveChangesAsync();

        // Complete with mock URLs
        _ = Task.Run(async () =>
        {
            await Task.Delay(300);
            var outputs = Enumerable.Range(1, Math.Max(1, Math.Min(4, numOutputs)))
                .Select(i => $"https://example.com/mock/{style}/{id}/{i}.png").ToArray();
            var pr = Predictions[id];
            pr.Status = "succeeded";
            pr.CompletedAt = DateTime.UtcNow;
            pr.Output = JsonSerializer.SerializeToElement(outputs);
            Predictions[id] = pr;
        });

        return result;
    }

    public Task<ReplicatePredictionResult> GetPredictionStatusAsync(string predictionId)
    {
        if (Predictions.TryGetValue(predictionId, out var pr))
        {
            return Task.FromResult(pr);
        }
        return Task.FromResult(new ReplicatePredictionResult
        {
            Id = predictionId,
            Status = "succeeded",
            CreatedAt = DateTime.UtcNow.AddSeconds(-1),
            CompletedAt = DateTime.UtcNow,
            Output = JsonSerializer.SerializeToElement(new[] { $"https://example.com/mock/{predictionId}.png" })
        });
    }

    public Task<string> GenerateImagesAsync(GenerateImagesRequestDto request)
    {
        // Return prediction id for convenience
        return Task.FromResult(Guid.NewGuid().ToString());
    }


    public async Task<ReplicatePredictionResult> EnhancePhotoAsync(string userId, string imageUrl, string enhancementType = "professional")
    {
        var id = Guid.NewGuid().ToString();
        var result = new ReplicatePredictionResult
        {
            Id = id,
            Version = "mock/flux-kontext-pro",
            Status = "starting",
            CreatedAt = DateTime.UtcNow,
            Input = new Dictionary<string, object> { { "user_id", userId }, { "image", imageUrl }, { "type", enhancementType } }
        };
        Predictions[id] = result;
        _context.Predictions.Add(new Prediction { Id = id, UserId = userId, Style = "enhance", CreatedAt = DateTime.UtcNow });
        await _context.SaveChangesAsync();
        _ = Task.Run(async () =>
        {
            await Task.Delay(200);
            var pr = Predictions[id];
            pr.Status = "succeeded";
            pr.CompletedAt = DateTime.UtcNow;
            pr.Output = JsonSerializer.SerializeToElement($"https://example.com/mock/enhanced/{id}.png");
            Predictions[id] = pr;
        });
        return result;
    }

    public Task<bool> CheckModelExistsAsync(string modelId) => Task.FromResult(true);

    public Task<bool> DeleteModelAsync(string modelId) => Task.FromResult(true);

    public Task<ReplicatePredictionResult> CreatePredictionAsync(string modelId, Dictionary<string, object> input)
    {
        var id = Guid.NewGuid().ToString();
        var result = new ReplicatePredictionResult
        {
            Id = id,
            Version = modelId,
            Status = "succeeded",
            CreatedAt = DateTime.UtcNow,
            CompletedAt = DateTime.UtcNow,
            Output = JsonSerializer.SerializeToElement(new[] { $"https://example.com/mock/{id}.png" })
        };
        Predictions[id] = result;
        return Task.FromResult(result);
    }

    public Task<List<ReplicateModelInfo>> FindUserModelsByPatternAsync(string userId)
    {
        var list = new List<ReplicateModelInfo>
        {
            new ReplicateModelInfo { Name = $"user-{userId}-20250101010101", Owner = "mock", CreatedAt = DateTime.UtcNow.AddDays(-1), UpdatedAt = DateTime.UtcNow, LatestVersion = "mock-version", Visibility = "private" }
        };
        return Task.FromResult(list);
    }

    public Task<string?> GetModelVersionAsync(string modelId)
    {
        // Return just the version hash - the calling code will format it properly
        return Task.FromResult<string?>("1234567890abcdef1234567890abcdef1234567890abcdef1234567890abcdef");
    }

    public Task<bool> CheckModelAvailabilityAsync(string modelId) => Task.FromResult(true);
}