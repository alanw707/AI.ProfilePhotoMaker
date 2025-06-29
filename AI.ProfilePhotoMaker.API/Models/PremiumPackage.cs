using System.ComponentModel.DataAnnotations;

namespace AI.ProfilePhotoMaker.API.Models;

public class PremiumPackage
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
    
    [Required]
    public int MaxStyles { get; set; }
    
    [Required]
    public int MaxImagesPerStyle { get; set; }
    
    [MaxLength(500)]
    public string Description { get; set; } = string.Empty;
    
    [Required]
    public bool IsActive { get; set; } = true;
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    public DateTime? UpdatedAt { get; set; }
    
    // Navigation properties
    public virtual ICollection<UserPackagePurchase> Purchases { get; set; } = new List<UserPackagePurchase>();
}