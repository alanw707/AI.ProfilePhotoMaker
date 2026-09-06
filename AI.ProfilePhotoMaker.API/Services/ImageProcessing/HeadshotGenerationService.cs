using AI.ProfilePhotoMaker.API.Data;
using AI.ProfilePhotoMaker.API.Infrastructure.Logging;
using AI.ProfilePhotoMaker.API.Models;
using AI.ProfilePhotoMaker.API.Models.DTOs;
using AI.ProfilePhotoMaker.API.Services.Storage;
using Microsoft.EntityFrameworkCore;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;
using System.Security.Cryptography;
using System.Text;

namespace AI.ProfilePhotoMaker.API.Services.ImageProcessing;

public class HeadshotGenerationService : IHeadshotGenerationService
{
    public const string ActionName = "instant_headshot_generation";
    private static readonly TimeSpan GenerationLeaseDuration = TimeSpan.FromHours(1);

    private readonly ApplicationDbContext _dbContext;
    private readonly IBasicTierService _basicTierService;
    private readonly IHeadshotGenerationProvider _provider;
    private readonly IOutcomePackageService? _outcomePackageService;
    private readonly IStorageService _storageService;
    private readonly StoragePathResolver _pathResolver;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<HeadshotGenerationService> _logger;

    private static string S(string? value) => LoggingSanitizer.Sanitize(value);
    private static string Sid(string? value) => LoggingSanitizer.SanitizeId(value);

    public HeadshotGenerationService(
        ApplicationDbContext dbContext,
        IBasicTierService basicTierService,
        IHeadshotGenerationProvider provider,
        IStorageService storageService,
        StoragePathResolver pathResolver,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<HeadshotGenerationService> logger)
        : this(dbContext, basicTierService, provider, storageService, pathResolver, httpClientFactory, configuration, logger, null)
    {
    }

    public HeadshotGenerationService(
        ApplicationDbContext dbContext,
        IBasicTierService basicTierService,
        IHeadshotGenerationProvider provider,
        IStorageService storageService,
        StoragePathResolver pathResolver,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<HeadshotGenerationService> logger,
        IOutcomePackageService? outcomePackageService)
    {
        _dbContext = dbContext;
        _basicTierService = basicTierService;
        _provider = provider;
        _outcomePackageService = outcomePackageService;
        _storageService = storageService;
        _pathResolver = pathResolver;
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<HeadshotGenerationResponseDto> GenerateHeadshotAsync(
        HeadshotGenerationRequestDto request,
        string userId,
        CancellationToken cancellationToken = default)
    {
        var profile = await _dbContext.UserProfiles.FirstOrDefaultAsync(p => p.UserId == userId, cancellationToken);
        if (profile == null)
        {
            throw new InvalidOperationException("User profile not found.");
        }

        var sourcePath = ValidateAndNormalizeSourcePath(request.ImageStoragePath, userId);
        var portraitStyle = await ResolvePortraitStyleAsync(request.Style, cancellationToken);
        request.Style = portraitStyle.Name;
        var packageCode = NormalizePackageCode(request.PackageCode);
        var requestedOutputs = Math.Clamp(request.NumOutputs, 1, 9);
        requestedOutputs = packageCode switch
        {
            "free_preview" => 1,
            "starter_package" => Math.Min(requestedOutputs, 3),
            "pro_package" => Math.Min(requestedOutputs, 9),
            _ => 1
        };
        var requiredCredits = packageCode == "free_preview" && _outcomePackageService != null ? 0 : CreditCostConfig.GetCreditCost(ActionName) * requestedOutputs;
        var correlationId = BuildDeterministicCorrelationId(userId, sourcePath, request);
        var existingOperation = await _dbContext.HeadshotGenerationOperations
            .AsNoTracking()
            .Where(o => o.UserId == userId && o.CorrelationId == correlationId)
            .Select(o => new { o.Status, o.OperationToken })
            .FirstOrDefaultAsync(cancellationToken);
        var candidateCorrelationPrefix = $"{correlationId}:candidate:";
        var existingGeneratedImages = await _dbContext.ProcessedImages
            .Where(i =>
                i.UserProfileId == profile.Id &&
                i.GenerationStatus == "succeeded" &&
                i.GenerationMode == "instant_headshot" &&
                (existingOperation == null || (i.GenerationOperationToken ?? "") == existingOperation.OperationToken) &&
                (i.CorrelationId == correlationId || (i.CorrelationId != null && i.CorrelationId.StartsWith(candidateCorrelationPrefix))))
            .OrderBy(i => i.CorrelationId)
            .Take(requestedOutputs)
            .ToListAsync(cancellationToken);
        var existingPromotedPreview = packageCode != "free_preview" && request.ReusedPreviewProcessedImageId.HasValue
            ? await _dbContext.ProcessedImages
                .FirstOrDefaultAsync(i =>
                    i.UserProfileId == profile.Id &&
                    i.GenerationStatus == "succeeded" &&
                    i.GenerationMode == "instant_headshot_promoted_preview" &&
                    (existingOperation == null || (i.GenerationOperationToken ?? "") == existingOperation.OperationToken) &&
                    i.CorrelationId == $"{correlationId}:promoted-preview",
                    cancellationToken)
            : null;
        var hasCompleteIdempotentResult = existingGeneratedImages.Count >= requestedOutputs &&
            (request.ReusedPreviewProcessedImageId.HasValue ? existingPromotedPreview != null : existingPromotedPreview == null) &&
            (existingOperation == null || existingOperation.Status == HeadshotGenerationOperationStatus.Succeeded);
        if (hasCompleteIdempotentResult)
        {
            var existingRemainingCredits = await _basicTierService.GetAvailableCreditsAsync(userId);
            var existingCandidates = new List<HeadshotCandidateDto>();
            if (existingPromotedPreview != null)
            {
                existingCandidates.Add(ToCandidateDto(existingPromotedPreview));
            }
            existingCandidates.AddRange(existingGeneratedImages.Select(ToCandidateDto));
            var primary = existingCandidates[0];
            _logger.LogInformation(
                "Returning idempotent instant headshot candidates for user {UserId}, count={Count}, correlation={CorrelationId}",
                Sid(userId), existingCandidates.Count, S(correlationId));

            return new HeadshotGenerationResponseDto
            {
                Success = true,
                ImageUrl = primary.ImageUrl,
                StoragePath = primary.StoragePath,
                ProcessedImageId = primary.ProcessedImageId,
                Provider = primary.Provider,
                Model = primary.Model,
                Style = NormalizeStyle(request.Style),
                Background = NormalizeBackground(request.Background),
                CreditsCost = 0,
                RemainingCredits = existingRemainingCredits,
                CorrelationId = correlationId,
                Candidates = existingCandidates
            };
        }

        if (_outcomePackageService != null)
        {
            var entitlement = packageCode == "free_preview"
                ? null
                : await _outcomePackageService.GetActiveEntitlementAsync(userId, packageCode, cancellationToken);
            var candidateSlots = requestedOutputs +
                (packageCode != "free_preview" && !request.IsRegeneration && request.ReusedPreviewProcessedImageId.HasValue ? 1 : 0);
            var allowance = PackageEntitlementPolicy.CheckGenerationAllowance(
                packageCode,
                candidateSlots,
                request.IsRegeneration,
                entitlement);
            if (!allowance.Allowed)
            {
                throw new HeadshotGenerationException(
                    allowance.FailureCode ?? "PackageEntitlementRequired",
                    allowance.FailureMessage ?? "Choose or unlock a profile photo package before generating these candidates.");
            }
        }

        var availableCredits = await _basicTierService.GetAvailableCreditsAsync(userId);
        if (availableCredits < requiredCredits)
        {
            throw new HeadshotGenerationException("InsufficientCredits", $"Instant headshot generation requires {requiredCredits} credit{(requiredCredits == 1 ? string.Empty : "s")}.");
        }

        var operationToken = await AcquireGenerationOperationAsync(userId, correlationId, cancellationToken);

        CreditConsumptionResult? consumed = null;
        var generationCommitted = false;
        var providerOutcomeUnknown = false;
        var debitOutcomeUnknown = false;

        try
        {
            if (requiredCredits > 0)
            {
                debitOutcomeUnknown = true;
                consumed = await _basicTierService.ConsumeCreditsAsync(
                    userId,
                    requiredCredits,
                    ActionName,
                    $"{correlationId}:attempt:{operationToken}",
                    cancellationToken);
                debitOutcomeUnknown = consumed == null;

                if (consumed == null || !consumed.Success)
                {
                    var code = consumed?.Error == "insufficient_credits" ? "InsufficientCredits" : "CreditConsumptionFailed";
                    throw new HeadshotGenerationException(code, "Unable to charge credits for instant headshot generation.");
                }
            }

            _logger.LogInformation(
                "Instant headshot generation started for user {UserId}, provider={Provider}, model={Model}, correlation={CorrelationId}",
                Sid(userId), S(_provider.ProviderName), S(_provider.ModelName), S(correlationId));

            var candidates = new List<HeadshotCandidateDto>();
            var generationSourcePath = sourcePath;
            if (packageCode != "free_preview" && request.ReusedPreviewProcessedImageId is int previewImageId)
            {
                var promotedPreview = await BuildPromotedPreviewCandidateAsync(previewImageId, profile.Id, sourcePath, portraitStyle.Name, correlationId, operationToken, request, cancellationToken);
                if (promotedPreview != null)
                {
                    candidates.Add(promotedPreview);
                    if (!await StorageImageExistsAsync(sourcePath, cancellationToken) && !string.IsNullOrWhiteSpace(promotedPreview.StoragePath))
                    {
                        _logger.LogWarning(
                            "Preview source image missing for paid continuation. Falling back to promoted preview raw image. SourcePath={SourcePath}, FallbackPath={FallbackPath}",
                            S(sourcePath),
                            S(promotedPreview.StoragePath));
                        generationSourcePath = promotedPreview.StoragePath;
                    }
                }
            }

            for (var outputIndex = 0; outputIndex < requestedOutputs; outputIndex++)
            {
                var candidateCorrelationId = requestedOutputs == 1
                    ? correlationId
                    : $"{correlationId}:candidate:{outputIndex + 1}";
                var recipe = packageCode == "free_preview"
                    ? HeadshotRecipeRegistry.None(request.UseCaseCode)
                    : HeadshotRecipeRegistry.Resolve(request.UseCaseCode, request.RecipeCode, outputIndex);
                await EnsureGenerationOperationOwnershipAsync(userId, correlationId, operationToken, cancellationToken);
                providerOutcomeUnknown = true;
                var result = await _provider.GenerateAsync(new HeadshotGenerationRequest
                {
                    UserId = userId,
                    ImageStoragePath = generationSourcePath,
                    Style = portraitStyle.Name,
                    Background = request.Background,
                    PromptTemplate = ApplyRecipeToPrompt(BuildInstantHeadshotPrompt(portraitStyle.PromptTemplate, profile), recipe),
                    UseCaseCode = recipe.UseCaseCode,
                    RecipeCode = recipe.Code,
                    Label = recipe.Label,
                    CorrelationId = candidateCorrelationId
                }, cancellationToken);

                if (!result.Success)
                {
                    providerOutcomeUnknown = result.FailureCode == "ProviderOutcomeUnknown";
                    throw new HeadshotGenerationException(
                        result.FailureCode ?? "ProviderGenerationFailed",
                        result.FailureMessage ?? "Headshot provider failed to generate an image.");
                }

                await EnsureGenerationOperationOwnershipAsync(userId, correlationId, operationToken, cancellationToken);
                var storedOutput = await StoreProviderOutputAsync(result.DataUrlOrUrl, userId, packageCode == "free_preview" && _outcomePackageService != null, cancellationToken);
                await EnsureGenerationOperationOwnershipAsync(userId, correlationId, operationToken, cancellationToken);

                var processedImage = new ProcessedImage
                {
                    OriginalImageUrl = sourcePath,
                    ProcessedImageUrl = storedOutput.DisplayPath,
                    Style = NormalizeStyle(request.Style),
                    UserProfileId = profile.Id,
                    CreatedAt = DateTime.UtcNow,
                    IsGenerated = true,
                    IsOriginalUpload = false,
                    Provider = result.Provider,
                    ProviderModel = result.Model,
                    GenerationMode = "instant_headshot",
                    CreditCost = CreditCostConfig.GetCreditCost(ActionName),
                    GenerationStatus = "succeeded",
                    CorrelationId = candidateCorrelationId,
                    GenerationOperationToken = operationToken,
                    FailureReason = storedOutput.RawPath == null ? null : $"raw-preview:{storedOutput.RawPath}",
                    PromptVersion = string.IsNullOrWhiteSpace(recipe.Code) ? result.PromptVersion : $"{result.PromptVersion}:{recipe.Code}"
                };
                processedImage.SetScheduledDeletionDate();

                _dbContext.ProcessedImages.Add(processedImage);
                await _dbContext.SaveChangesAsync(cancellationToken);
                candidates.Add(ToCandidateDto(processedImage));
                providerOutcomeUnknown = false;
            }

            var commitStrategy = _dbContext.Database.CreateExecutionStrategy();
            await commitStrategy.ExecuteAsync(async () =>
            {
                // A retry must reload allowance state instead of decrementing a tracked entity twice.
                _dbContext.ChangeTracker.Clear();
                var alreadyCommitted = await _dbContext.HeadshotGenerationOperations
                    .AsNoTracking()
                    .AnyAsync(o => o.UserId == userId &&
                                   o.CorrelationId == correlationId &&
                                   o.OperationToken == operationToken &&
                                   o.Status == HeadshotGenerationOperationStatus.Succeeded,
                        cancellationToken);
                if (alreadyCommitted)
                {
                    return;
                }

                await using var commitTransaction = _dbContext.Database.IsRelational()
                    ? await _dbContext.Database.BeginTransactionAsync(cancellationToken)
                    : null;

                if (packageCode != "free_preview" && _outcomePackageService != null)
                {
                    var consumedPackageAllowance = request.IsRegeneration
                        ? await _outcomePackageService.ConsumeRefinementAsync(userId, packageCode, cancellationToken)
                        : await _outcomePackageService.ConsumeCandidatesAsync(userId, packageCode, candidates.Count, cancellationToken);
                    if (!consumedPackageAllowance)
                    {
                        throw new HeadshotGenerationException("PackageEntitlementRequired", request.IsRegeneration
                            ? "Unable to consume package refinement allowance."
                            : "Unable to consume profile photo package allowance.");
                    }
                }

                await CompleteGenerationOperationAsync(userId, correlationId, operationToken, cancellationToken);
                if (commitTransaction != null)
                {
                    await commitTransaction.CommitAsync(cancellationToken);
                }
            });
            generationCommitted = true;
            var remainingCredits = await _basicTierService.GetAvailableCreditsAsync(userId);
            await _basicTierService.LogUsageAsync(
                userId,
                "instant_headshot_generation_succeeded",
                $"provider={candidates[0].Provider}; model={candidates[0].Model}; candidateCount={candidates.Count}; correlationId={correlationId}",
                creditsCost: 0,
                creditsRemaining: remainingCredits);

            _logger.LogInformation(
                "Instant headshot generation succeeded for user {UserId}, candidates={Count}, correlation={CorrelationId}",
                Sid(userId), candidates.Count, S(correlationId));

            var primary = candidates[0];
            return new HeadshotGenerationResponseDto
            {
                Success = true,
                ImageUrl = primary.ImageUrl,
                StoragePath = primary.StoragePath,
                ProcessedImageId = primary.ProcessedImageId,
                Provider = primary.Provider,
                Model = primary.Model,
                Style = NormalizeStyle(request.Style),
                Background = NormalizeBackground(request.Background),
                CreditsCost = requiredCredits,
                RemainingCredits = remainingCredits,
                CorrelationId = correlationId,
                Candidates = candidates
            };
        }
        catch (Exception ex)
        {
            if (providerOutcomeUnknown || debitOutcomeUnknown)
            {
                // A missing consumption receipt does not prove the debit rolled back.
                // Keep the claim for reconciliation on uncertain debit/provider outcomes;
                // neither automatic refund nor resubmission is safe.
                _logger.LogWarning("Generation outcome requires reconciliation for correlation {CorrelationId}; debitUnknown={DebitUnknown}", S(correlationId), debitOutcomeUnknown);
                throw;
            }
            if (!generationCommitted)
            {
                // A lost commit acknowledgement is not proof of rollback. Discard tracked
                // transaction state and consult the durable receipt before compensating.
                // If this read fails, leave the operation for reconciliation, not retry.
                _dbContext.ChangeTracker.Clear();
                generationCommitted = await _dbContext.HeadshotGenerationOperations.AsNoTracking()
                    .AnyAsync(o => o.UserId == userId && o.CorrelationId == correlationId &&
                                   o.OperationToken == operationToken &&
                                   o.Status == HeadshotGenerationOperationStatus.Succeeded,
                        CancellationToken.None);
            }
            if (!generationCommitted)
            {
                var failureReason = ex is HeadshotGenerationException { Code: "PackageEntitlementRequired" }
                    ? "package-entitlement-consumption-failed"
                    : "generation-incomplete";
                await MarkPersistedCandidatesFailedAsync(
                    profile.Id,
                    correlationId,
                    operationToken,
                    CancellationToken.None,
                    failureReason);
                await _basicTierService.RefundCreditsAsync(userId, consumed);
                await FailGenerationOperationAsync(userId, correlationId, operationToken, CancellationToken.None);
            }
            throw;
        }
    }

    private async Task<string> AcquireGenerationOperationAsync(
        string userId,
        string correlationId,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var operationToken = Guid.NewGuid().ToString("N");
        if (!_dbContext.Database.IsRelational())
        {
            var existing = await _dbContext.HeadshotGenerationOperations
                .FirstOrDefaultAsync(o => o.UserId == userId && o.CorrelationId == correlationId, cancellationToken);
            if (existing != null)
            {
                if (existing.Status != HeadshotGenerationOperationStatus.Failed)
                {
                    throw new HeadshotGenerationException(
                        "GenerationInProgress",
                        "An identical headshot generation request is already in progress or requires reconciliation.");
                }

                existing.Status = HeadshotGenerationOperationStatus.Processing;
                existing.OperationToken = operationToken;
                existing.FailureCode = null;
                existing.LeaseExpiresAt = now.Add(GenerationLeaseDuration);
                existing.UpdatedAt = now;
                await _dbContext.SaveChangesAsync(cancellationToken);
                return operationToken;
            }

            _dbContext.HeadshotGenerationOperations.Add(new HeadshotGenerationOperation
            {
                UserId = userId,
                CorrelationId = correlationId,
                Status = HeadshotGenerationOperationStatus.Processing,
                OperationToken = operationToken,
                LeaseExpiresAt = now.Add(GenerationLeaseDuration),
                CreatedAt = now,
                UpdatedAt = now
            });
            await _dbContext.SaveChangesAsync(cancellationToken);
            return operationToken;
        }

        // Processing operations are never reclaimed automatically. A crashed operation must be
        // reconciled before retry so provider work and charging remain at-most-once.
        var reclaimed = await _dbContext.HeadshotGenerationOperations
            .Where(o => o.UserId == userId &&
                        o.CorrelationId == correlationId &&
                        o.Status == HeadshotGenerationOperationStatus.Failed)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(o => o.Status, HeadshotGenerationOperationStatus.Processing)
                .SetProperty(o => o.OperationToken, operationToken)
                .SetProperty(o => o.FailureCode, (string?)null)
                .SetProperty(o => o.LeaseExpiresAt, now.Add(GenerationLeaseDuration))
                .SetProperty(o => o.UpdatedAt, now), cancellationToken);
        if (reclaimed == 1)
        {
            return operationToken;
        }

        var operation = new HeadshotGenerationOperation
        {
            UserId = userId,
            CorrelationId = correlationId,
            Status = HeadshotGenerationOperationStatus.Processing,
            OperationToken = operationToken,
            LeaseExpiresAt = now.Add(GenerationLeaseDuration),
            CreatedAt = now,
            UpdatedAt = now
        };
        _dbContext.HeadshotGenerationOperations.Add(operation);

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
            return operationToken;
        }
        catch (DbUpdateException)
        {
            _dbContext.Entry(operation).State = EntityState.Detached;
            var existingOperation = await _dbContext.HeadshotGenerationOperations
                .AsNoTracking()
                .AnyAsync(o => o.UserId == userId && o.CorrelationId == correlationId, cancellationToken);
            if (!existingOperation)
            {
                throw;
            }

            throw new HeadshotGenerationException(
                "GenerationInProgress",
                "An identical headshot generation request is already in progress.");
        }
    }

    private async Task EnsureGenerationOperationOwnershipAsync(
        string userId,
        string correlationId,
        string operationToken,
        CancellationToken cancellationToken)
    {
        var ownsOperation = await _dbContext.HeadshotGenerationOperations
            .AsNoTracking()
            .AnyAsync(o => o.UserId == userId &&
                           o.CorrelationId == correlationId &&
                           o.OperationToken == operationToken &&
                           o.Status == HeadshotGenerationOperationStatus.Processing,
                cancellationToken);
        if (!ownsOperation)
        {
            throw new HeadshotGenerationException(
                "GenerationOwnershipLost",
                "The generation operation is no longer owned by this worker.");
        }
    }

    private async Task CompleteGenerationOperationAsync(
        string userId,
        string correlationId,
        string operationToken,
        CancellationToken cancellationToken)
    {
        if (!_dbContext.Database.IsRelational())
        {
            var operation = await _dbContext.HeadshotGenerationOperations
                .FirstOrDefaultAsync(o => o.UserId == userId &&
                                          o.CorrelationId == correlationId &&
                                          o.OperationToken == operationToken,
                    cancellationToken);
            if (operation?.Status != HeadshotGenerationOperationStatus.Processing)
            {
                throw new InvalidOperationException("Unable to complete the generation idempotency operation.");
            }

            operation.Status = HeadshotGenerationOperationStatus.Succeeded;
            operation.LeaseExpiresAt = DateTime.UtcNow;
            operation.UpdatedAt = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync(cancellationToken);
            return;
        }

        var updated = await _dbContext.HeadshotGenerationOperations
            .Where(o => o.UserId == userId &&
                        o.CorrelationId == correlationId &&
                        o.OperationToken == operationToken &&
                        o.Status == HeadshotGenerationOperationStatus.Processing)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(o => o.Status, HeadshotGenerationOperationStatus.Succeeded)
                .SetProperty(o => o.LeaseExpiresAt, DateTime.UtcNow)
                .SetProperty(o => o.UpdatedAt, DateTime.UtcNow), cancellationToken);
        if (updated != 1)
        {
            throw new InvalidOperationException("Unable to complete the generation idempotency operation.");
        }
    }

    private async Task FailGenerationOperationAsync(
        string userId,
        string correlationId,
        string operationToken,
        CancellationToken cancellationToken)
    {
        if (!_dbContext.Database.IsRelational())
        {
            var operation = await _dbContext.HeadshotGenerationOperations
                .FirstOrDefaultAsync(o => o.UserId == userId &&
                                          o.CorrelationId == correlationId &&
                                          o.OperationToken == operationToken &&
                                          o.Status == HeadshotGenerationOperationStatus.Processing,
                    cancellationToken);
            if (operation != null)
            {
                operation.Status = HeadshotGenerationOperationStatus.Failed;
                operation.FailureCode = "generation-failed";
                operation.LeaseExpiresAt = DateTime.UtcNow;
                operation.UpdatedAt = DateTime.UtcNow;
                await _dbContext.SaveChangesAsync(cancellationToken);
            }
            return;
        }

        await _dbContext.HeadshotGenerationOperations
            .Where(o => o.UserId == userId &&
                        o.CorrelationId == correlationId &&
                        o.OperationToken == operationToken &&
                        o.Status == HeadshotGenerationOperationStatus.Processing)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(o => o.Status, HeadshotGenerationOperationStatus.Failed)
                .SetProperty(o => o.FailureCode, "generation-failed")
                .SetProperty(o => o.LeaseExpiresAt, DateTime.UtcNow)
                .SetProperty(o => o.UpdatedAt, DateTime.UtcNow), cancellationToken);
    }

    private async Task MarkPersistedCandidatesFailedAsync(
        int userProfileId,
        string correlationId,
        string operationToken,
        CancellationToken cancellationToken,
        string failureReason = "package-entitlement-consumption-failed")
    {
        var candidateCorrelationPrefix = $"{correlationId}:candidate:";
        var promotedPreviewCorrelationId = $"{correlationId}:promoted-preview";
        var persistedCandidates = await _dbContext.ProcessedImages
            .Where(i =>
                i.UserProfileId == userProfileId &&
                i.GenerationStatus == "succeeded" &&
                i.GenerationOperationToken == operationToken &&
                (i.CorrelationId == correlationId ||
                 i.CorrelationId == promotedPreviewCorrelationId ||
                 (i.CorrelationId != null && i.CorrelationId.StartsWith(candidateCorrelationPrefix))))
            .ToListAsync(cancellationToken);

        foreach (var candidate in persistedCandidates)
        {
            candidate.GenerationStatus = "failed";
            candidate.FailureReason = failureReason;
        }

        if (persistedCandidates.Count > 0)
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    private async Task<HeadshotCandidateDto?> BuildPromotedPreviewCandidateAsync(
        int processedImageId,
        int userProfileId,
        string sourcePath,
        string portraitStyleName,
        string paidCorrelationId,
        string operationToken,
        HeadshotGenerationRequestDto request,
        CancellationToken cancellationToken)
    {
        var image = await _dbContext.ProcessedImages
            .FirstOrDefaultAsync(i => i.Id == processedImageId && i.UserProfileId == userProfileId && i.GenerationStatus == "succeeded", cancellationToken);
        if (image?.FailureReason?.StartsWith("raw-preview:", StringComparison.Ordinal) != true ||
            !string.Equals(image.OriginalImageUrl, sourcePath, StringComparison.Ordinal) ||
            !string.Equals(NormalizeStyle(image.Style), NormalizeStyle(portraitStyleName), StringComparison.Ordinal) ||
            !string.Equals(request.ReusedPreviewSourcePath, sourcePath, StringComparison.Ordinal) ||
            !string.Equals(NormalizeStyle(request.ReusedPreviewStyle), NormalizeStyle(portraitStyleName), StringComparison.Ordinal))
        {
            return null;
        }

        var rawStoragePath = image.FailureReason["raw-preview:".Length..];
        var existingPromotion = await _dbContext.ProcessedImages
            .FirstOrDefaultAsync(i =>
                i.UserProfileId == image.UserProfileId &&
                i.ProcessedImageUrl == rawStoragePath &&
                i.GenerationMode == "instant_headshot_promoted_preview" &&
                i.GenerationStatus == "succeeded" &&
                i.GenerationOperationToken == operationToken &&
                i.CorrelationId == $"{paidCorrelationId}:promoted-preview",
                cancellationToken);
        if (existingPromotion != null)
        {
            return ToCandidateDto(existingPromotion);
        }

        var promotedImage = new ProcessedImage
        {
            OriginalImageUrl = image.OriginalImageUrl,
            ProcessedImageUrl = rawStoragePath,
            Style = image.Style,
            UserProfileId = image.UserProfileId,
            CreatedAt = DateTime.UtcNow,
            IsGenerated = true,
            IsOriginalUpload = false,
            Provider = image.Provider,
            ProviderModel = image.ProviderModel,
            GenerationMode = "instant_headshot_promoted_preview",
            PromptVersion = image.PromptVersion,
            CreditCost = 0,
            GenerationStatus = "succeeded",
            CorrelationId = $"{paidCorrelationId}:promoted-preview",
            GenerationOperationToken = operationToken
        };
        promotedImage.SetScheduledDeletionDate();
        _dbContext.ProcessedImages.Add(promotedImage);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return ToCandidateDto(promotedImage);
    }

    private async Task<bool> StorageImageExistsAsync(string storagePath, CancellationToken cancellationToken)
    {
        try
        {
            await using var stream = await _storageService.GetImageAsync(storagePath);
            cancellationToken.ThrowIfCancellationRequested();
            return stream != null;
        }
        catch (FileNotFoundException)
        {
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Unable to verify storage image existence for {StoragePath}", S(storagePath));
            return false;
        }
    }

    private HeadshotCandidateDto ToCandidateDto(ProcessedImage image)
    {
        return new HeadshotCandidateDto
        {
            ImageUrl = _storageService.GetImageUrl(image.ProcessedImageUrl),
            StoragePath = image.ProcessedImageUrl,
            ProcessedImageId = image.Id,
            Provider = image.Provider ?? _provider.ProviderName,
            Model = image.ProviderModel ?? _provider.ModelName,
            CorrelationId = image.CorrelationId ?? string.Empty,
            UseCaseCode = ExtractUseCaseCode(image.PromptVersion),
            RecipeCode = ExtractRecipeCode(image.PromptVersion),
            Label = ExtractRecipeLabel(image.PromptVersion)
        };
    }

    private static string? ExtractUseCaseCode(string? promptVersion)
    {
        var recipeCode = ExtractRecipeCode(promptVersion);
        return string.IsNullOrWhiteSpace(recipeCode) ? null : HeadshotRecipeRegistry.FindByCode(recipeCode)?.UseCaseCode;
    }

    private static string? ExtractRecipeCode(string? promptVersion)
    {
        if (string.IsNullOrWhiteSpace(promptVersion) || !promptVersion.Contains(':', StringComparison.Ordinal))
        {
            return null;
        }

        return promptVersion.Split(':', StringSplitOptions.RemoveEmptyEntries).LastOrDefault();
    }

    private static string? ExtractRecipeLabel(string? promptVersion)
    {
        var recipeCode = ExtractRecipeCode(promptVersion);
        return string.IsNullOrWhiteSpace(recipeCode) ? null : HeadshotRecipeRegistry.FindByCode(recipeCode)?.Label;
    }

    private static string BuildDeterministicCorrelationId(string userId, string sourcePath, HeadshotGenerationRequestDto request)
    {
        var clientRequestId = string.IsNullOrWhiteSpace(request.ClientRequestId)
            ? "legacy"
            : request.ClientRequestId.Trim();
        var normalized = string.Join('|',
            userId.Trim(),
            sourcePath.Trim(),
            NormalizeStyle(request.Style),
            NormalizeBackground(request.Background),
            NormalizePackageCode(request.PackageCode),
            request.NumOutputs.ToString(System.Globalization.CultureInfo.InvariantCulture),
            NormalizeUseCaseCode(request.UseCaseCode),
            NormalizeRecipeCode(request.RecipeCode),
            request.IsRegeneration ? "regenerate" : "generate",
            clientRequestId);
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(normalized))).ToLowerInvariant();
        return $"{ActionName}:{hash[..32]}";
    }

    private static string ApplyRecipeToPrompt(string basePrompt, HeadshotRecipe recipe)
    {
        if (string.IsNullOrWhiteSpace(recipe.PromptModifier))
        {
            return basePrompt;
        }

        return $"{basePrompt}\n\nUse-case recipe: {recipe.PromptModifier}\nKeep the same person, facial structure, and natural skin texture. Avoid over-smoothing, synthetic-looking features, distorted hands, text artifacts, logos, badges, or misleading professional credentials.";
    }

    private string ValidateAndNormalizeSourcePath(string storagePath, string userId)
    {
        if (string.IsNullOrWhiteSpace(storagePath))
        {
            throw new HeadshotGenerationException("InvalidImageSource", "Image storage path is required.");
        }

        var trimmed = storagePath.Trim();
        string decoded;
        try
        {
            decoded = Uri.UnescapeDataString(trimmed);
        }
        catch
        {
            throw new HeadshotGenerationException("InvalidImageSource", "Invalid image source.");
        }

        if (!string.Equals(trimmed, decoded, StringComparison.Ordinal) ||
            Uri.TryCreate(trimmed, UriKind.Absolute, out _) ||
            trimmed.Contains('\\') ||
            trimmed.Split('/', StringSplitOptions.RemoveEmptyEntries).Any(p => p is "." or ".."))
        {
            throw new HeadshotGenerationException("InvalidImageSource", "Invalid image source.");
        }

        var allowedPrefixes = new[]
        {
            _pathResolver.GetDirectoryPrefix(StorageType.Upload, userId),
            _pathResolver.GetDirectoryPrefix(StorageType.Enhanced, userId)
        };

        if (!allowedPrefixes.Any(prefix => trimmed.StartsWith(prefix, StringComparison.Ordinal) && trimmed.Length > prefix.Length))
        {
            _logger.LogWarning("Rejected headshot source path for user {UserId}. Path={Path}", Sid(userId), S(trimmed));
            throw new HeadshotGenerationException("InvalidImageSource", "Invalid image source.");
        }

        return trimmed;
    }

    private sealed record StoredProviderOutput(string DisplayPath, string? RawPath);

    private async Task<StoredProviderOutput> StoreProviderOutputAsync(string output, string userId, bool freePreview, CancellationToken cancellationToken)
    {
        var bytes = await ReadOutputBytesAsync(output, cancellationToken);
        string? rawPath = null;
        if (freePreview)
        {
            await using var rawStream = new MemoryStream(bytes);
            rawPath = await _storageService.SaveImageAsync(rawStream, $"headshot-raw-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid():N}.png", userId, "generated-private");
            bytes = await CreateFreePreviewAsync(bytes, cancellationToken);
        }

        var fileName = $"headshot-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid():N}.png";
        await using var stream = new MemoryStream(bytes);
        var displayPath = await _storageService.SaveImageAsync(stream, fileName, userId, "generated");
        return new StoredProviderOutput(displayPath, rawPath);
    }

    private static async Task<byte[]> CreateFreePreviewAsync(byte[] bytes, CancellationToken cancellationToken)
    {
        using var image = Image.Load<Rgba32>(bytes);
        image.ProcessPixelRows(accessor =>
        {
            for (var y = 0; y < accessor.Height; y++)
            {
                var row = accessor.GetRowSpan(y);
                for (var x = 0; x < row.Length; x++)
                {
                    var bandWidth = Math.Max(12, accessor.Width / 44);
                    var diagonalBand = ((x + y) % Math.Max(140, accessor.Width / 3)) < bandWidth;
                    var reverseDiagonalBand = ((x - y + accessor.Height) % Math.Max(170, accessor.Width / 2)) < Math.Max(8, bandWidth / 2);
                    var bottomLogoBar = y > accessor.Height * 0.84 && y < accessor.Height * 0.91 && x > accessor.Width * 0.58 && x < accessor.Width * 0.94;
                    var cornerLogo = x > accessor.Width * 0.68 && y > accessor.Height * 0.72 && ((x / Math.Max(8, accessor.Width / 42) + y / Math.Max(8, accessor.Height / 42)) % 2 == 0);
                    if (!diagonalBand && !reverseDiagonalBand && !bottomLogoBar && !cornerLogo) continue;

                    ref var pixel = ref row[x];
                    var strength = bottomLogoBar || cornerLogo ? 0.36 : 0.24;
                    pixel.R = (byte)Math.Min(255, pixel.R * (1 - strength) + 255 * strength);
                    pixel.G = (byte)Math.Min(255, pixel.G * (1 - strength) + 255 * strength);
                    pixel.B = (byte)Math.Min(255, pixel.B * (1 - strength) + 255 * strength);
                }
            }
        });

        await using var output = new MemoryStream();
        await image.SaveAsync(output, new PngEncoder(), cancellationToken);
        return output.ToArray();
    }

    private async Task<byte[]> ReadOutputBytesAsync(string output, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(output))
        {
            throw new HeadshotGenerationException("ProviderEmptyOutput", "Headshot provider returned no image.");
        }

        const string dataPrefix = "data:image/png;base64,";
        if (output.StartsWith(dataPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return Convert.FromBase64String(output[dataPrefix.Length..]);
        }

        if (Uri.TryCreate(output, UriKind.Absolute, out var uri))
        {
            var client = _httpClientFactory.CreateClient();
            using var response = await client.GetAsync(uri, cancellationToken);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsByteArrayAsync(cancellationToken);
        }

        throw new HeadshotGenerationException("ProviderInvalidOutput", "Headshot provider returned an unsupported image format.");
    }

    private static string BuildInstantHeadshotPrompt(string promptTemplate, UserProfile profile)
    {
        var gender = string.IsNullOrWhiteSpace(profile.Gender) ? "person" : profile.Gender.Trim().ToLowerInvariant();
        var ethnicity = string.IsNullOrWhiteSpace(profile.Ethnicity) ? string.Empty : profile.Ethnicity.Trim().ToLowerInvariant();
        var genderEthnicityCombo = string.IsNullOrWhiteSpace(ethnicity) ? gender : $"{gender} {ethnicity}";

        return promptTemplate
            .Replace("{subject}", "professional person")
            .Replace("{gender} {ethnicity}", genderEthnicityCombo)
            .Replace("{gender}", gender)
            .Replace("{ethnicity}", ethnicity)
            .Replace("  ", " ")
            .Trim();
    }

    private async Task<Style> ResolvePortraitStyleAsync(string? requestedStyle, CancellationToken cancellationToken)
    {
        var normalizedStyle = NormalizeStyle(requestedStyle);
        var style = await _dbContext.Styles
            .Where(s => s.IsActive && s.Name.ToLower() == normalizedStyle)
            .FirstOrDefaultAsync(cancellationToken);

        if (style != null)
        {
            return style;
        }

        throw new HeadshotGenerationException("StyleUnavailable", "That portrait style is no longer available. Choose another style.");
    }

    private static string NormalizePackageCode(string? packageCode)
    {
        var normalized = (packageCode ?? "free_preview").Trim().ToLowerInvariant();
        return normalized is "free_preview" or "starter_package" or "pro_package" ? normalized : "free_preview";
    }

    private static string NormalizeStyle(string? style)
    {
        if (string.IsNullOrWhiteSpace(style))
        {
            return "linkedin";
        }

        var normalized = style.Trim().ToLowerInvariant();
        return normalized == "professional" ? "linkedin" : normalized;
    }

    private static string NormalizeBackground(string? background) =>
        string.IsNullOrWhiteSpace(background) ? "auto" : background.Trim().ToLowerInvariant();

    private static string NormalizeUseCaseCode(string? useCaseCode) => HeadshotRecipeRegistry.NormalizeUseCaseCode(useCaseCode);

    private static string NormalizeRecipeCode(string? recipeCode) => HeadshotRecipeRegistry.NormalizeRecipeCode(recipeCode);
}

public sealed record HeadshotRecipe(string UseCaseCode, string Code, string Label, string PromptModifier);

public static class HeadshotRecipeRegistry
{
    public static HeadshotRecipe None(string? useCaseCode) => new(NormalizeUseCaseCode(useCaseCode), string.Empty, string.Empty, string.Empty);

    private static readonly IReadOnlyDictionary<string, HeadshotRecipe[]> Recipes = new Dictionary<string, HeadshotRecipe[]>(StringComparer.Ordinal)
    {
        ["linkedin_executive"] = new[]
        {
            new HeadshotRecipe("linkedin_executive", "linkedin_studio", "Best LinkedIn profile", "clean studio or soft office background, confident approachable expression, modern professional wardrobe, shoulders-up crop for LinkedIn profile use"),
            new HeadshotRecipe("linkedin_executive", "executive_presence", "Best executive look", "premium office or boardroom-adjacent background, composed executive presence, polished business formal wardrobe, trustworthy senior-leader tone"),
            new HeadshotRecipe("linkedin_executive", "approachable_resume", "Best resume/avatar", "neutral uncluttered background, warm approachable expression, business-casual wardrobe, crisp crop that works for resume and avatar uploads")
        },
        ["realtor"] = new[]
        {
            new HeadshotRecipe("realtor", "realtor_trust", "Best Zillow/Realtor profile", "bright modern real-estate office feel, warm trustworthy expression, polished but approachable wardrobe, square profile crop suitable for Zillow and Realtor.com"),
            new HeadshotRecipe("realtor", "luxury_listing", "Best luxury listing vibe", "upscale home interior or premium neutral background, confident expert tone, refined wardrobe, high-end but realistic real estate marketing feel"),
            new HeadshotRecipe("realtor", "social_flyer", "Best social flyer image", "clean background with room for flyer/social cropping, friendly client-facing expression, professional wardrobe, vertical-crop friendly framing")
        },
        ["founder_press_kit"] = new[]
        {
            new HeadshotRecipe("founder_press_kit", "press_bio", "Best press bio", "editorial business portrait, confident founder expression, polished but authentic wardrobe, suitable for press bio and podcast guest pages"),
            new HeadshotRecipe("founder_press_kit", "website_hero", "Best website hero", "wider composition feel with clean negative space, founder/entrepreneur presence, modern startup or premium office mood, website hero friendly framing"),
            new HeadshotRecipe("founder_press_kit", "linkedin_founder", "Best founder LinkedIn", "professional LinkedIn-ready founder portrait, approachable thought-leader tone, clean premium background, crisp square-avatar crop")
        }
    };

    public static HeadshotRecipe? FindByCode(string recipeCode)
    {
        var normalizedRecipe = NormalizeRecipeCode(recipeCode);
        return Recipes.Values.SelectMany(recipe => recipe).FirstOrDefault(recipe => recipe.Code == normalizedRecipe);
    }

    public static HeadshotRecipe Resolve(string? useCaseCode, string? recipeCode, int candidateIndex)
    {
        var normalizedUseCase = NormalizeUseCaseCode(useCaseCode);
        var recipes = Recipes[normalizedUseCase];
        var normalizedRecipe = NormalizeRecipeCode(recipeCode);
        if (!string.IsNullOrWhiteSpace(normalizedRecipe))
        {
            var match = recipes.FirstOrDefault(recipe => recipe.Code == normalizedRecipe);
            if (match != null)
            {
                return match;
            }
        }

        return recipes[Math.Abs(candidateIndex) % recipes.Length];
    }

    public static string NormalizeUseCaseCode(string? useCaseCode)
    {
        var normalized = (useCaseCode ?? "linkedin_executive").Trim().ToLowerInvariant().Replace('-', '_');
        return Recipes.ContainsKey(normalized) ? normalized : "linkedin_executive";
    }

    public static string NormalizeRecipeCode(string? recipeCode) => string.IsNullOrWhiteSpace(recipeCode) ? string.Empty : recipeCode.Trim().ToLowerInvariant().Replace('-', '_');
}

public class HeadshotGenerationException : Exception
{
    public HeadshotGenerationException(string code, string message) : base(message)
    {
        Code = code;
    }

    public string Code { get; }
}
