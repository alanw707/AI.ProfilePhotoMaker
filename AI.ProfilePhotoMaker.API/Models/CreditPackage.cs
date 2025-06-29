using System.ComponentModel.DataAnnotations;

namespace AI.ProfilePhotoMaker.API.Models;

public class CreditPackage
{
    [Key]
    public int Id { get; set; }
    
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;
    
    [Required]
    public int Credits { get; set; }
    
    [Required]
    public decimal Price { get; set; }
    
    [MaxLength(500)]
    public string Description { get; set; } = string.Empty;
    
    [Required]
    public bool IsActive { get; set; } = true;
    
    // Display order for UI
    public int DisplayOrder { get; set; } = 0;
    
    // Bonus credits (for promotions)
    public int BonusCredits { get; set; } = 0;
    
    // Stripe integration
    public string? StripeProductId { get; set; }
    public string? StripePriceId { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    
    // Navigation properties
    public virtual ICollection<CreditPurchase> Purchases { get; set; } = new List<CreditPurchase>();
    
    // Computed property for total credits including bonus
    public int TotalCredits => Credits + BonusCredits;
}