using AI.ProfilePhotoMaker.API.Data;
using AI.ProfilePhotoMaker.API.Models;
using AI.ProfilePhotoMaker.API.Models.DTOs;
using Microsoft.EntityFrameworkCore;

namespace AI.ProfilePhotoMaker.API.Services;

public class PremiumPackageService : IPremiumPackageService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<PremiumPackageService> _logger;

    public PremiumPackageService(ApplicationDbContext context, ILogger<PremiumPackageService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<IEnumerable<PremiumPackageDto>> GetActivePackagesAsync()
    {
        return await _context.PremiumPackages
            .Where(p => p.IsActive)
            .OrderBy(p => p.Price)
            .Select(p => new PremiumPackageDto
            {
                Id = p.Id,
                Name = p.Name,
                Credits = p.Credits,
                Price = p.Price,
                MaxStyles = p.MaxStyles,
                MaxImagesPerStyle = p.MaxImagesPerStyle,
                Description = p.Description
            })
            .ToListAsync();
    }

    public async Task<UserPackageStatusDto> GetUserPackageStatusAsync(string userId)
    {
        var activePackage = await GetActiveUserPackageAsync(userId);

        if (activePackage == null)
        {
            return new UserPackageStatusDto
            {
                HasActivePackage = false,
                CreditsRemaining = 0,
                ModelExpired = true,
                DaysUntilExpiration = 0
            };
        }

        var daysUntilExpiration = (activePackage.ExpirationDate - DateTime.UtcNow).Days;
        var modelExpired = !activePackage.TrainedModelId.HasValue() || daysUntilExpiration <= 0;

        return new UserPackageStatusDto
        {
            PackageId = activePackage.PackageId,
            PackageName = activePackage.Package.Name,
            CreditsRemaining = activePackage.CreditsRemaining,
            ExpirationDate = activePackage.ExpirationDate,
            TrainedModelId = activePackage.TrainedModelId,
            ModelTrainedAt = activePackage.ModelTrainedAt,
            HasActivePackage = true,
            ModelExpired = modelExpired,
            DaysUntilExpiration = Math.Max(0, daysUntilExpiration)
        };
    }

    public async Task<UserPackagePurchase?> PurchasePackageAsync(string userId, int packageId, string? paymentTransactionId = null)
    {
        var package = await _context.PremiumPackages
            .FirstOrDefaultAsync(p => p.Id == packageId && p.IsActive);

        if (package == null)
        {
            _logger.LogWarning("Package {PackageId} not found or inactive", packageId);
            return null;
        }

        // Check if user already has an active package
        var existingPackage = await GetActiveUserPackageAsync(userId);
        if (existingPackage != null)
        {
            _logger.LogWarning("User {UserId} already has an active package", userId);
            return null;
        }

        var purchase = new UserPackagePurchase
        {
            UserId = userId,
            PackageId = packageId,
            PurchaseDate = DateTime.UtcNow,
            ExpirationDate = DateTime.UtcNow.AddDays(7), // 7-day model expiration
            CreditsRemaining = package.Credits,
            AmountPaid = package.Price,
            PaymentTransactionId = paymentTransactionId,
            IsActive = true
        };

        _context.UserPackagePurchases.Add(purchase);
        await _context.SaveChangesAsync();

        // Load the package for return
        await _context.Entry(purchase)
            .Reference(p => p.Package)
            .LoadAsync();

        _logger.LogInformation("User {UserId} purchased package {PackageId} with {Credits} credits", 
            userId, packageId, package.Credits);

        return purchase;
    }

    public async Task<bool> ConsumeCreditsAsync(string userId, int credits, string action)
    {
        var activePackage = await GetActiveUserPackageAsync(userId);
        if (activePackage == null || activePackage.CreditsRemaining < credits)
        {
            _logger.LogWarning("User {UserId} attempted to consume {Credits} credits but has insufficient balance", 
                userId, credits);
            return false;
        }

        // Check if package has expired
        if (DateTime.UtcNow > activePackage.ExpirationDate)
        {
            _logger.LogWarning("User {UserId} attempted to use expired package", userId);
            return false;
        }

        activePackage.CreditsRemaining -= credits;
        
        // Log the credit consumption
        var usageLog = new UsageLog
        {
            UserId = userId,
            Action = action,
            CreditsCost = credits,
            CreditsRemaining = activePackage.CreditsRemaining,
            CreatedAt = DateTime.UtcNow,
            Details = $"Premium package credit consumption - Package: {activePackage.Package.Name}"
        };

        _context.UsageLogs.Add(usageLog);
        await _context.SaveChangesAsync();

        _logger.LogInformation("User {UserId} consumed {Credits} credits for {Action}", 
            userId, credits, action);

        return true;
    }

    public async Task<bool> HasAvailableCreditsAsync(string userId, int requiredCredits = 1)
    {
        var activePackage = await GetActiveUserPackageAsync(userId);
        
        if (activePackage == null)
            return false;

        // Check if package has expired
        if (DateTime.UtcNow > activePackage.ExpirationDate)
            return false;

        return activePackage.CreditsRemaining >= requiredCredits;
    }

    public async Task<UserPackagePurchase?> GetActiveUserPackageAsync(string userId)
    {
        return await _context.UserPackagePurchases
            .Include(p => p.Package)
            .Where(p => p.UserId == userId && p.IsActive)
            .OrderByDescending(p => p.PurchaseDate)
            .FirstOrDefaultAsync();
    }

    public async Task<bool> UpdateTrainedModelAsync(string userId, string modelId)
    {
        var activePackage = await GetActiveUserPackageAsync(userId);
        if (activePackage == null)
        {
            _logger.LogWarning("No active package found for user {UserId} when updating trained model", userId);
            return false;
        }

        activePackage.TrainedModelId = modelId;
        activePackage.ModelTrainedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        _logger.LogInformation("Updated trained model {ModelId} for user {UserId}", modelId, userId);
        return true;
    }

    public async Task CleanupExpiredModelsAsync()
    {
        var expiredPackages = await _context.UserPackagePurchases
            .Where(p => p.IsActive && 
                       p.TrainedModelId != null && 
                       DateTime.UtcNow > p.ExpirationDate)
            .ToListAsync();

        foreach (var package in expiredPackages)
        {
            // TODO: Call Replicate API to delete the model
            // await _replicateApiClient.DeleteModelAsync(package.TrainedModelId);
            
            package.TrainedModelId = null;
            package.ModelTrainedAt = null;
            package.IsActive = false;
            
            _logger.LogInformation("Cleaned up expired model for user {UserId}, package {PackageId}", 
                package.UserId, package.PackageId);
        }

        if (expiredPackages.Any())
        {
            await _context.SaveChangesAsync();
            _logger.LogInformation("Cleaned up {Count} expired models", expiredPackages.Count);
        }
    }

    public async Task<bool> CanSelectStylesAsync(string userId, int styleCount)
    {
        var activePackage = await GetActiveUserPackageAsync(userId);
        if (activePackage == null || DateTime.UtcNow > activePackage.ExpirationDate)
            return false;

        return styleCount <= activePackage.Package.MaxStyles;
    }

    public async Task<bool> CanGenerateImagesAsync(string userId, int imageCount)
    {
        var activePackage = await GetActiveUserPackageAsync(userId);
        if (activePackage == null || DateTime.UtcNow > activePackage.ExpirationDate)
            return false;

        // Check if they have enough credits (including 1 for training if no model exists)
        var requiredCredits = imageCount;
        if (string.IsNullOrEmpty(activePackage.TrainedModelId))
        {
            requiredCredits += 1; // Add 1 credit for training
        }

        return activePackage.CreditsRemaining >= requiredCredits;
    }
}

public static class StringExtensions
{
    public static bool HasValue(this string? value)
    {
        return !string.IsNullOrWhiteSpace(value);
    }
}