using AI.ProfilePhotoMaker.API.Infrastructure.Logging;
using AI.ProfilePhotoMaker.API.Models.DTOs;
using AI.ProfilePhotoMaker.API.Services;
using AI.ProfilePhotoMaker.API.Services.ImageProcessing;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AI.ProfilePhotoMaker.API.Controllers;

[ApiController]
[Route("api/admin")]
[Authorize(Roles = "Admin")]
public class AdminController : BaseController
{
    private readonly IReplicateApiClient _replicateApiClient;
    private readonly IAdminService _adminService;
    private readonly ILogger<AdminController> _logger;

    public AdminController(
        IReplicateApiClient replicateApiClient,
        IAdminService adminService,
        ILogger<AdminController> logger)
        : base(logger)
    {
        _replicateApiClient = replicateApiClient;
        _adminService = adminService;
        _logger = logger;
    }

    [HttpGet("users")]
    public async Task<IActionResult> GetUsers([FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] string? search = null)
    {
        var authCheck = ValidateAuthentication();
        if (authCheck != null) return authCheck;

        var (users, totalCount) = await _adminService.GetUsersAsync(page, pageSize, search);
        return SuccessResponse(new { users, totalCount, page, pageSize });
    }

    [HttpGet("users/{userId}")]
    public async Task<IActionResult> GetUserDetail(string userId)
    {
        var authCheck = ValidateAuthentication();
        if (authCheck != null) return authCheck;

        var diagnostics = await _adminService.GetUserDiagnosticsAsync(userId);
        if (diagnostics == null)
        {
            return ErrorResponse("NotFound", "User not found", 404);
        }

        return SuccessResponse(diagnostics);
    }

    [HttpPost("users/{userId}/deactivate")]
    public async Task<IActionResult> DeactivateUser(string userId, [FromBody] AdminActionReasonDto dto)
    {
        var authCheck = ValidateAuthentication();
        if (authCheck != null) return authCheck;

        var adminUserId = GetCurrentUserId()!;
        if (adminUserId == userId)
        {
            return ErrorResponse("InvalidOperation", "Cannot deactivate your own account");
        }

        var success = await _adminService.DeactivateUserAsync(userId, adminUserId, dto.Reason);
        if (!success)
        {
            return ErrorResponse("OperationFailed", "Failed to deactivate user");
        }

        return SuccessResponse(new { userId }, "User deactivated");
    }

    [HttpPost("users/{userId}/reactivate")]
    public async Task<IActionResult> ReactivateUser(string userId, [FromBody] AdminActionReasonDto dto)
    {
        var authCheck = ValidateAuthentication();
        if (authCheck != null) return authCheck;

        var adminUserId = GetCurrentUserId()!;
        var success = await _adminService.ReactivateUserAsync(userId, adminUserId, dto.Reason);
        if (!success)
        {
            return ErrorResponse("OperationFailed", "Failed to reactivate user");
        }

        return SuccessResponse(new { userId }, "User reactivated");
    }

    [HttpDelete("users/{userId}")]
    public async Task<IActionResult> DeleteUser(string userId, [FromBody] AdminActionReasonDto dto)
    {
        var authCheck = ValidateAuthentication();
        if (authCheck != null) return authCheck;

        var adminUserId = GetCurrentUserId()!;
        var (success, message) = await _adminService.DeleteUserAsync(userId, adminUserId, dto.Reason);
        if (!success)
        {
            return ErrorResponse("OperationFailed", message);
        }

        return SuccessResponse(new { userId }, message);
    }

    [HttpPost("credits/adjust")]
    public async Task<IActionResult> AdjustCredits([FromBody] AdminCreditAdjustmentDto dto)
    {
        var authCheck = ValidateAuthentication();
        if (authCheck != null) return authCheck;

        var adminUserId = GetCurrentUserId()!;
        var (success, message, newBalance) = await _adminService.AdjustCreditsAsync(dto, adminUserId);
        if (!success)
        {
            return ErrorResponse("OperationFailed", message);
        }

        return SuccessResponse(new { dto.UserId, newBalance }, message);
    }

    [HttpGet("coupons")]
    public async Task<IActionResult> GetCoupons()
    {
        var coupons = await _adminService.GetCouponsAsync();
        return SuccessResponse(coupons);
    }

    [HttpPost("coupons")]
    public async Task<IActionResult> CreateCoupon([FromBody] CouponCreateDto dto)
    {
        var authCheck = ValidateAuthentication();
        if (authCheck != null) return authCheck;

        var adminUserId = GetCurrentUserId()!;
        var (coupon, warning) = await _adminService.CreateCouponAsync(dto, adminUserId);
        if (coupon == null)
        {
            return ErrorResponse("Conflict", "Coupon code already exists");
        }

        return Ok(new { success = true, data = coupon, warning, error = (object?)null });
    }

    [HttpPut("coupons/{id:int}")]
    public async Task<IActionResult> UpdateCoupon(int id, [FromBody] CouponUpdateDto dto)
    {
        var authCheck = ValidateAuthentication();
        if (authCheck != null) return authCheck;

        var adminUserId = GetCurrentUserId()!;
        var success = await _adminService.UpdateCouponAsync(id, dto, adminUserId);
        if (!success)
        {
            return ErrorResponse("NotFound", "Coupon not found", 404);
        }

        return SuccessResponse(new { id }, "Coupon updated");
    }

    [HttpDelete("coupons/{id:int}")]
    public async Task<IActionResult> DeleteCoupon(int id)
    {
        var authCheck = ValidateAuthentication();
        if (authCheck != null) return authCheck;

        var adminUserId = GetCurrentUserId()!;
        var success = await _adminService.DeleteCouponAsync(id, adminUserId);
        if (!success)
        {
            return ErrorResponse("NotFound", "Coupon not found", 404);
        }

        return SuccessResponse(new { id }, "Coupon deactivated");
    }

    [HttpGet("audit-logs")]
    public async Task<IActionResult> GetAuditLogs([FromQuery] int page = 1, [FromQuery] int pageSize = 50, [FromQuery] string? action = null)
    {
        var (logs, totalCount) = await _adminService.GetAuditLogsAsync(page, pageSize, action);
        return SuccessResponse(new { logs, totalCount, page, pageSize });
    }

    [HttpGet("dashboard")]
    public async Task<IActionResult> GetDashboard()
    {
        try
        {
            var dashboard = await _adminService.GetDashboardAsync();
            return SuccessResponse(dashboard);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load admin dashboard statistics");
            return ErrorResponse("InternalError", "Failed to load dashboard statistics", 500);
        }
    }

    [HttpGet("product-health")]
    public async Task<IActionResult> GetProductHealth([FromQuery] string window = "7d")
    {
        try
        {
            var productHealth = await _adminService.GetProductHealthAsync(window);
            return SuccessResponse(productHealth);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load admin product health for window {Window}", S(window));
            return ErrorResponse("InternalError", "Failed to load product health", 500);
        }
    }

    [HttpPost("cleanup-orphaned-model")]
    public async Task<IActionResult> CleanupOrphanedModel([FromBody] CleanupRequest request)
    {
        try
        {
            _logger.LogWarning("Starting orphaned model cleanup for: {ModelId}", S(request.ModelId));

            if (string.IsNullOrEmpty(request.ModelId))
            {
                return BadRequest(new { success = false, error = "ModelId is required" });
            }

            var (success, errorMessage) = await _replicateApiClient.DeleteModelAsync(request.ModelId);

            if (success)
            {
                _logger.LogInformation("Successfully cleaned up orphaned model: {ModelId}", S(request.ModelId));
                return Ok(new
                {
                    success = true,
                    message = $"Orphaned model {request.ModelId} successfully deleted from Replicate",
                    modelId = request.ModelId
                });
            }

            _logger.LogError("Failed to cleanup orphaned model {ModelId}: {Error}", S(request.ModelId), S(errorMessage));
            return BadRequest(new
            {
                success = false,
                error = errorMessage ?? "Failed to delete orphaned model",
                modelId = request.ModelId
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception during orphaned model cleanup for {ModelId}", S(request.ModelId));
            return StatusCode(500, new
            {
                success = false,
                error = "Internal server error during cleanup",
                details = ex.Message,
                modelId = request.ModelId
            });
        }
    }
}

public class CleanupRequest
{
    public string ModelId { get; set; } = string.Empty;
}
