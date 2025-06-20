using System.ComponentModel.DataAnnotations;

namespace AI.ProfilePhotoMaker.API.Models.DTOs;

public class PremiumPackageDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Credits { get; set; }
    public decimal Price { get; set; }
    public int MaxStyles { get; set; }
    public int MaxImagesPerStyle { get; set; }
    public string Description { get; set; } = string.Empty;
}

public class PurchasePackageRequestDto
{
    [Required]
    public int PackageId { get; set; }
    
    public string? PaymentTransactionId { get; set; }
}

public class UserPackageStatusDto
{
    public int? PackageId { get; set; }
    public string? PackageName { get; set; }
    public int CreditsRemaining { get; set; }
    public DateTime? ExpirationDate { get; set; }
    public string? TrainedModelId { get; set; }
    public DateTime? ModelTrainedAt { get; set; }
    public bool HasActivePackage { get; set; }
    public bool ModelExpired { get; set; }
    public int DaysUntilExpiration { get; set; }
}