using AI.ProfilePhotoMaker.API.Models;
using AI.ProfilePhotoMaker.API.Models.DTOs;

namespace AI.ProfilePhotoMaker.API.Services;

public interface IAdminService
{
    Task<(List<AdminUserListDto> Users, int TotalCount)> GetUsersAsync(int page, int pageSize, string? searchTerm);
    Task<AdminUserDiagnosticsDto?> GetUserDiagnosticsAsync(string userId);
    Task<IReadOnlyList<AdminOutcomePackageDefinitionDto>> GetPackageDefinitionsAsync();
    Task<AdminPackageOperationResult> GrantPackageEntitlementAsync(string userId, AdminGrantPackageEntitlementDto dto, string adminUserId);
    Task<AdminPackageOperationResult> RevokePackageEntitlementAsync(string userId, int entitlementId, string reason, string adminUserId);
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
    Task<AdminProductHealthDto> GetProductHealthAsync(string window);
}

public sealed record AdminPackageOperationResult(
    bool Success,
    string Code,
    string Message,
    AdminPackageEntitlementDto? Entitlement = null,
    int? CreditBalance = null);
