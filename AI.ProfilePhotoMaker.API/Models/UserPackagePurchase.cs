using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AI.ProfilePhotoMaker.API.Models;

public class UserPackagePurchase
{
    [Key]
    public int Id { get; set; }
    
    [Required]
    public string UserId { get; set; } = string.Empty;
    
    [Required]
    public int PackageId { get; set; }
    
    [Required]
    public DateTime PurchaseDate { get; set; } = DateTime.UtcNow;
    
    [Required]
    public DateTime ExpirationDate { get; set; }
    
    [Required]
    public int CreditsRemaining { get; set; }
    
    [MaxLength(200)]
    public string? TrainedModelId { get; set; }
    
    public DateTime? ModelTrainedAt { get; set; }
    
    [Required]
    public bool IsActive { get; set; } = true;
    
    [Required]
    [Column(TypeName = "decimal(10,2)")]
    public decimal AmountPaid { get; set; }
    
    [MaxLength(100)]
    public string? PaymentTransactionId { get; set; }
    
    public DateTime? CompletedAt { get; set; }
    
    // Navigation properties
    [ForeignKey(nameof(PackageId))]
    public virtual PremiumPackage Package { get; set; } = null!;
    
    [ForeignKey(nameof(UserId))]
    public virtual ApplicationUser User { get; set; } = null!;
}