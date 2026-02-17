using AI.ProfilePhotoMaker.API.Models;
using AI.ProfilePhotoMaker.API.Models.DTOs;

namespace AI.ProfilePhotoMaker.API.Services;

public interface IAdminService
{
    Task<(List<AdminUserListDto> Users, int TotalCount)> GetUsersAsync(int page, int pageSize, string? searchTerm);
    Task<AdminUserDetailDto?> GetUserDetailAsync(string userId);
    Task<bool> DeactivateUserAsync(string userId, string adminUserId, string reason);
    Task<bool> ReactivateUserAsync(string userId, string adminUserId, string reason);
    Task<(bool Success, string Message)> DeleteUserAsync(string userId, string adminUserId, string reason);
    Task<(bool Success, string Message, int NewBalance)> AdjustCreditsAsync(AdminCreditAdjustmentDto dto, string adminUserId);
    Task<List<CouponListDto>> GetCouponsAsync();
    Task<(Coupon? Coupon, string? Warning)> CreateCouponAsync(CouponCreateDto dto, string adminUserId);
    Task<bool> UpdateCouponAsync(int couponId, CouponUpdateDto dto, string adminUserId);
    Task<bool> DeleteCouponAsync(int couponId, string adminUserId);
    Task<(List<AdminAuditLogDto> Logs, int TotalCount)> GetAuditLogsAsync(int page, int pageSize, string? actionFilter);
    Task<AdminDashboardDto> GetDashboardAsync();
}
