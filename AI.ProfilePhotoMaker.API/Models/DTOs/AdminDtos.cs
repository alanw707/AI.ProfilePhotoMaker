using System.ComponentModel.DataAnnotations;
using AI.ProfilePhotoMaker.API.Models;

namespace AI.ProfilePhotoMaker.API.Models.DTOs;

public class AdminUserListDto
{
    public string Id { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public int Credits { get; set; }
    public bool IsLockedOut { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? LastLoginAt { get; set; }
}

public class AdminUserDetailDto : AdminUserListDto
{
    public string? Gender { get; set; }
    public string? Ethnicity { get; set; }
    public string SubscriptionTier { get; set; } = string.Empty;
    public bool EmailConfirmed { get; set; }
    public List<string> Roles { get; set; } = new();
}

public class AdminCreditAdjustmentDto
{
    [Required]
    public string UserId { get; set; } = string.Empty;

    [Required]
    public int Amount { get; set; }

    [Required(ErrorMessage = "Reason is required")]
    [MinLength(1, ErrorMessage = "Reason cannot be empty")]
    [MaxLength(500)]
    public string Reason { get; set; } = string.Empty;
}

public class CouponCreateDto
{
    [Required]
    [MaxLength(50)]
    public string Code { get; set; } = string.Empty;

    [Required]
    public DiscountType DiscountType { get; set; }

    [Required]
    [Range(0.01, 100)]
    public decimal DiscountValue { get; set; }

    [Required]
    [Range(1, int.MaxValue)]
    public int MaxUsages { get; set; }

    public DateTime? ExpiresAt { get; set; }
}

public class CouponUpdateDto
{
    public int? MaxUsages { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public bool? IsActive { get; set; }
}

public class CouponListDto
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string DiscountType { get; set; } = string.Empty;
    public decimal DiscountValue { get; set; }
    public int MaxUsages { get; set; }
    public int CurrentUsages { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public bool IsActive { get; set; }
    public bool IsExpired { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class AdminAuditLogDto
{
    public int Id { get; set; }
    public string AdminEmail { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string? TargetUserEmail { get; set; }
    public string? Details { get; set; }
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class AdminDashboardDto
{
    public int TotalUsers { get; set; }
    public int ActiveUsers { get; set; }
    public int TotalCreditsOutstanding { get; set; }
    public long TotalCreditsPurchased { get; set; }
    public int ActiveCoupons { get; set; }
}

public class AdminActionReasonDto
{
    [Required(ErrorMessage = "Reason is required")]
    [MinLength(1, ErrorMessage = "Reason cannot be empty")]
    [MaxLength(500)]
    public string Reason { get; set; } = string.Empty;
}

public class ValidateCouponDto
{
    [Required]
    [MaxLength(50)]
    public string Code { get; set; } = string.Empty;

    [Range(0.01, double.MaxValue)]
    public decimal OriginalPrice { get; set; }
}
