using System.Text.Json;
using AI.ProfilePhotoMaker.API.Data;
using AI.ProfilePhotoMaker.API.Infrastructure.Logging;
using AI.ProfilePhotoMaker.API.Models;
using AI.ProfilePhotoMaker.API.Services.ImageProcessing;
using Microsoft.EntityFrameworkCore;

namespace AI.ProfilePhotoMaker.API.Services;

public interface IPendingGenerationService
{
    Task EnqueueAsync(string userId, string trainingRequestId, IEnumerable<string> styles, int numOutputsPerStyle);
    Task ProcessAsync(string trainingRequestId);
}

public class PendingGenerationService : IPendingGenerationService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<PendingGenerationService> _logger;
    private readonly IReplicateApiClient _replicateApiClient;
    private readonly IBasicTierService _basicTierService;

    private static string Sid(string? value) => LoggingSanitizer.SanitizeId(value);
    private static string S(string? value) => LoggingSanitizer.Sanitize(value);

    public PendingGenerationService(
        ApplicationDbContext context,
        ILogger<PendingGenerationService> logger,
        IReplicateApiClient replicateApiClient,
        IBasicTierService basicTierService)
    {
        _context = context;
        _logger = logger;
        _replicateApiClient = replicateApiClient;
        _basicTierService = basicTierService;
    }

    public async Task EnqueueAsync(string userId, string trainingRequestId, IEnumerable<string> styles, int numOutputsPerStyle)
    {
        if (numOutputsPerStyle < 1 || numOutputsPerStyle > 4)
        {
            _logger.LogWarning("Rejecting enqueue with invalid outputs per style {NumOutputsPerStyle} for training {TrainingId}", numOutputsPerStyle, Sid(trainingRequestId));
            throw new ArgumentOutOfRangeException(nameof(numOutputsPerStyle), "Outputs per style must be between 1 and 4.");
        }

        var stylesList = styles?.Where(s => !string.IsNullOrWhiteSpace(s)).Distinct().ToList() ?? new List<string>();
        if (!stylesList.Any())
        {
            _logger.LogWarning("Attempted to enqueue generation with no styles for training {TrainingId}", Sid(trainingRequestId));
            return;
        }

        var existing = await _context.PendingGenerationRequests
            .FirstOrDefaultAsync(p => p.UserId == userId && p.TrainingRequestId == trainingRequestId);

        if (existing != null)
        {
            existing.StylesJson = JsonSerializer.Serialize(stylesList);
            existing.NumOutputsPerStyle = numOutputsPerStyle;
            existing.Status = PendingGenerationStatus.Pending;
            existing.ErrorMessage = null;
            existing.CompletedAt = null;
            existing.StartedAt = null;
            existing.LastPredictionId = null;
        }
        else
        {
            _context.PendingGenerationRequests.Add(new PendingGenerationRequest
            {
                UserId = userId,
                TrainingRequestId = trainingRequestId,
                StylesJson = JsonSerializer.Serialize(stylesList),
                NumOutputsPerStyle = numOutputsPerStyle,
                Status = PendingGenerationStatus.Pending,
                CreatedAt = DateTime.UtcNow
            });
        }

        await _context.SaveChangesAsync();
        _logger.LogInformation("Enqueued background generation for training {TrainingId} with {Count} style(s)", Sid(trainingRequestId), stylesList.Count);
    }

    public async Task ProcessAsync(string trainingRequestId)
    {
        var pending = await _context.PendingGenerationRequests
            .Where(p => p.TrainingRequestId == trainingRequestId && p.Status == PendingGenerationStatus.Pending)
            .ToListAsync();

        if (!pending.Any())
        {
            return;
        }

        foreach (var request in pending)
        {
            await ProcessRequest(request);
        }
    }

    private async Task ProcessRequest(PendingGenerationRequest request)
    {
        try
        {
            if (request.NumOutputsPerStyle < 1 || request.NumOutputsPerStyle > 4)
            {
                request.Status = PendingGenerationStatus.Failed;
                request.ErrorMessage = "Invalid outputs per style";
                request.CompletedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
                return;
            }

            request.Status = PendingGenerationStatus.Started;
            request.StartedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            var modelRequest = await _context.ModelCreationRequests
                .FirstOrDefaultAsync(m => m.UserId == request.UserId && m.PendingTrainingRequestId == request.TrainingRequestId);

            if (modelRequest == null || string.IsNullOrWhiteSpace(modelRequest.TrainedModelVersion) || string.IsNullOrWhiteSpace(modelRequest.ReplicateModelId))
            {
                request.Status = PendingGenerationStatus.Failed;
                request.ErrorMessage = "Model not ready for generation";
                request.CompletedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
                return;
            }

            var styles = JsonSerializer.Deserialize<List<string>>(request.StylesJson) ?? new();
            if (!styles.Any())
            {
                request.Status = PendingGenerationStatus.Failed;
                request.ErrorMessage = "No styles to generate";
                request.CompletedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
                return;
            }

            var userProfile = await _context.UserProfiles.FirstOrDefaultAsync(u => u.UserId == request.UserId);
            var userInfo = userProfile != null ? new UserInfo
            {
                Gender = userProfile.Gender,
                Ethnicity = userProfile.Ethnicity
            } : null;

            var modelVersion = string.IsNullOrWhiteSpace(modelRequest.ReplicateModelId)
                ? modelRequest.TrainedModelVersion!
                : $"{modelRequest.ReplicateModelId}:{modelRequest.TrainedModelVersion}";

            var correlationId = $"pending-generation:{request.Id}";

            foreach (var style in styles)
            {
                var requiredCredits = request.NumOutputsPerStyle * CreditCostConfig.GetCreditCost("styled_generation");
                CreditConsumptionResult? consumed = null;
                try
                {
                    consumed = await _basicTierService.ConsumeCreditsAsync(request.UserId, requiredCredits, "styled_generation", correlationId);
                    if (!consumed.Success)
                    {
                        throw new InvalidOperationException("Insufficient credits for background generation");
                    }

                    var result = await _replicateApiClient.GenerateImagesAsync(
                        modelVersion,
                        request.UserId,
                        style,
                        userInfo,
                        request.NumOutputsPerStyle);

                    request.LastPredictionId = result.Id ?? request.LastPredictionId;
                }
                catch (Exception ex)
                {
                    if (consumed?.Success == true)
                    {
                        await _basicTierService.RefundCreditsAsync(request.UserId, consumed);
                    }

                    _logger.LogWarning(ex, "Failed background generation for training {TrainingId}, style {Style}", Sid(request.TrainingRequestId), S(style));
                    request.Status = PendingGenerationStatus.Failed;
                    request.ErrorMessage = ex.Message;
                    request.CompletedAt = DateTime.UtcNow;
                    await _context.SaveChangesAsync();
                    return;
                }
            }

            request.Status = PendingGenerationStatus.Succeeded;
            request.CompletedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            request.Status = PendingGenerationStatus.Failed;
            request.ErrorMessage = ex.Message;
            request.CompletedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            _logger.LogError(ex, "Error processing pending generation {RequestId}", request.Id);
        }
    }
}
