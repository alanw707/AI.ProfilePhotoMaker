using System.ComponentModel.DataAnnotations;

namespace AI.ProfilePhotoMaker.API.Models;

public enum DiscountType
{
    Percentage = 1,
    FixedAmount = 2
}

public class Coupon
{
    [Key]
    public int Id { get; set; }

    [Required]
    [MaxLength(50)]
    public string Code { get; set; } = string.Empty;

    [Required]
    public DiscountType DiscountType { get; set; }

    [Required]
    public decimal DiscountValue { get; set; }

    [Required]
    public int MaxUsages { get; set; }

    public int CurrentUsages { get; set; }

    public DateTime? ExpiresAt { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    [Required]
    public string CreatedByAdminId { get; set; } = string.Empty;

    public ICollection<CouponRedemption> Redemptions { get; set; } = new List<CouponRedemption>();
}
