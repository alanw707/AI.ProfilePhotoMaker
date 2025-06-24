using System.ComponentModel.DataAnnotations;

namespace AI.ProfilePhotoMaker.API.Models;

public class UserStyleSelection
{
    [Key]
    public int Id { get; set; }
    
    [Required]
    public int UserProfileId { get; set; }
    
    [Required]
    public int StyleId { get; set; }
    
    [Required]
    public DateTime SelectedAt { get; set; }
    
    // Navigation properties
    public virtual UserProfile UserProfile { get; set; } = null!;
    public virtual Style Style { get; set; } = null!;
}