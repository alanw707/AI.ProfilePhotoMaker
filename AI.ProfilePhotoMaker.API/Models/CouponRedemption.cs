using System.ComponentModel.DataAnnotations;

namespace AI.ProfilePhotoMaker.API.Models;

public class CouponRedemption
{
    [Key]
    public int Id { get; set; }

    [Required]
    public int CouponId { get; set; }

    [Required]
    public string UserId { get; set; } = string.Empty;

    public DateTime RedeemedAt { get; set; } = DateTime.UtcNow;

    public decimal DiscountApplied { get; set; }

    public decimal OriginalPrice { get; set; }

    public decimal FinalPrice { get; set; }

    public Coupon Coupon { get; set; } = null!;
}
