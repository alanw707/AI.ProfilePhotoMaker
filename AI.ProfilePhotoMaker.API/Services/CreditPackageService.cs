using AI.ProfilePhotoMaker.API.Data;
using AI.ProfilePhotoMaker.API.Models;
using AI.ProfilePhotoMaker.API.Models.DTOs;
using Microsoft.EntityFrameworkCore;

namespace AI.ProfilePhotoMaker.API.Services;

public class CreditPackageService : ICreditPackageService
{
    private readonly ApplicationDbContext _context;
    private readonly IBasicTierService _basicTierService;
    private readonly ILogger<CreditPackageService> _logger;

    public CreditPackageService(
        ApplicationDbContext context, 
        IBasicTierService basicTierService,
        ILogger<CreditPackageService> logger)
    {
        _context = context;
        _basicTierService = basicTierService;
        _logger = logger;
    }

    public async Task<IEnumerable<CreditPackageDto>> GetActiveCreditPackagesAsync()
    {
        var packages = await _context.CreditPackages
            .Where(p => p.IsActive)
            .OrderBy(p => p.DisplayOrder)
            .Select(p => new CreditPackageDto
            {
                Id = p.Id,
                Name = p.Name,
                Credits = p.Credits,
                BonusCredits = p.BonusCredits,
                TotalCredits = p.Credits + p.BonusCredits,
                Price = p.Price,
                Description = p.Description,
                DisplayOrder = p.DisplayOrder
            })
            .ToListAsync();

        return packages;
    }

    public async Task<CreditPurchase?> PurchaseCreditPackageAsync(string userId, int packageId, string? paymentTransactionId = null)
    {
        var package = await _context.CreditPackages
            .FirstOrDefaultAsync(p => p.Id == packageId && p.IsActive);

        if (package == null)
        {
            _logger.LogWarning("Credit package {PackageId} not found or inactive", packageId);
            return null;
        }

        var purchase = new CreditPurchase
        {
            UserId = userId,
            PackageId = packageId,
            PurchaseDate = DateTime.UtcNow,
            CreditsAwarded = package.TotalCredits,
            AmountPaid = package.Price,
            PaymentTransactionId = paymentTransactionId,
            Status = PaymentStatus.Completed,
            CompletedAt = DateTime.UtcNow
        };

        _context.CreditPurchases.Add(purchase);
        await _context.SaveChangesAsync();

        // Add the purchased credits to the user's account
        var creditsAdded = await _basicTierService.AddPurchasedCreditsAsync(userId, package.TotalCredits, "credit_package_purchase");
        
        if (!creditsAdded)
        {
            _logger.LogError("Failed to add purchased credits to user {UserId} after successful purchase {PurchaseId}", userId, purchase.Id);
            // Consider rolling back the purchase or marking it as failed
        }

        _logger.LogInformation("User {UserId} purchased credit package {PackageName} for {Amount}. Credits awarded: {Credits}", 
            userId, package.Name, package.Price, package.TotalCredits);

        return purchase;
    }

    public async Task<IEnumerable<CreditPurchase>> GetUserPurchaseHistoryAsync(string userId)
    {
        return await _context.CreditPurchases
            .Where(p => p.UserId == userId)
            .Include(p => p.Package)
            .OrderByDescending(p => p.PurchaseDate)
            .ToListAsync();
    }
}