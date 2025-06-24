using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AI.ProfilePhotoMaker.API.Models;

public class CreditPurchase
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
    public int CreditsAwarded { get; set; }
    
    [Required]
    [Column(TypeName = "decimal(10,2)")]
    public decimal AmountPaid { get; set; }
    
    [MaxLength(100)]
    public string? PaymentTransactionId { get; set; }
    
    [Required]
    [StringLength(50)]
    public string PaymentProvider { get; set; } = "stripe";
    
    public string? ExternalTransactionId { get; set; }
    
    // Payment status
    public PaymentStatus Status { get; set; } = PaymentStatus.Completed;
    
    public DateTime? CompletedAt { get; set; }
    
    // Navigation properties
    [ForeignKey(nameof(PackageId))]
    public virtual CreditPackage Package { get; set; } = null!;
    
    [ForeignKey(nameof(UserId))]
    public virtual ApplicationUser User { get; set; } = null!;
}