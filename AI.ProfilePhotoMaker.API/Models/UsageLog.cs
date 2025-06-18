using System.ComponentModel.DataAnnotations;

namespace AI.ProfilePhotoMaker.API.Models;

public class UsageLog
{
    public int Id { get; set; }
    
    [Required]
    public string UserId { get; set; } = string.Empty;
    
    public ApplicationUser User { get; set; } = null!;
    
    [Required]
    [MaxLength(50)]
    public string Action { get; set; } = string.Empty; // "free_generation", "credit_reset", etc.
    
    [MaxLength(500)]
    public string? Details { get; set; }
    
    public int? CreditsCost { get; set; }
    
    public int? CreditsRemaining { get; set; }
    
    [Required]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}