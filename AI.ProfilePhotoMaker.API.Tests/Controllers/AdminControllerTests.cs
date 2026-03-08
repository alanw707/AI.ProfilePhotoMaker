using System.Security.Claims;
using AI.ProfilePhotoMaker.API.Controllers;
using AI.ProfilePhotoMaker.API.Models;
using AI.ProfilePhotoMaker.API.Models.DTOs;
using AI.ProfilePhotoMaker.API.Services;
using AI.ProfilePhotoMaker.API.Services.ImageProcessing;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace AI.ProfilePhotoMaker.API.Tests.Controllers;

public class AdminControllerTests
{
    private readonly Mock<IReplicateApiClient> _replicateApiClient = new();
    private readonly Mock<IAdminService> _adminService = new();
    private readonly Mock<ILogger<AdminController>> _logger = new();

    [Fact]
    public async Task GetUsers_ReturnsOk_WithPaginatedUsers()
    {
        _adminService
            .Setup(s => s.GetUsersAsync(1, 20, null))
            .ReturnsAsync((new List<AdminUserListDto> { new() { Id = "u1", Email = "u1@example.com" } }, 1));

        var controller = CreateController();
        var result = await controller.GetUsers();

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(ok.Value);
    }

    [Fact]
    public async Task GetUserDetail_ReturnsNotFound_WhenUserDoesNotExist()
    {
        _adminService.Setup(s => s.GetUserDiagnosticsAsync("missing")).ReturnsAsync((AdminUserDiagnosticsDto?)null);

        var controller = CreateController();
        var result = await controller.GetUserDetail("missing");

        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task GetUserDetail_ReturnsOk_WithDiagnosticsPayload()
    {
        _adminService
            .Setup(s => s.GetUserDiagnosticsAsync("u1"))
            .ReturnsAsync(new AdminUserDiagnosticsDto
            {
                User = new AdminUserDetailDto { Id = "u1", Email = "u1@example.com" },
                Metrics = new AdminUserMetricsDto { CurrentCredits = 3 }
            });

        var controller = CreateController();
        var result = await controller.GetUserDetail("u1");

        Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public async Task DeactivateUser_ReturnsOk_WhenSuccessful()
    {
        _adminService.Setup(s => s.DeactivateUserAsync("u2", "admin-1", "reason")).ReturnsAsync(true);

        var controller = CreateController("admin-1");
        var result = await controller.DeactivateUser("u2", new AdminActionReasonDto { Reason = "reason" });

        Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public async Task DeactivateUser_ReturnsBadRequest_WhenSelfDeactivation()
    {
        var controller = CreateController("admin-1");
        var result = await controller.DeactivateUser("admin-1", new AdminActionReasonDto { Reason = "reason" });

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task DeleteUser_ReturnsBadRequest_WhenLastAdmin()
    {
        _adminService
            .Setup(s => s.DeleteUserAsync("admin-last", "admin-1", "reason"))
            .ReturnsAsync((false, "Cannot delete the last admin user"));

        var controller = CreateController("admin-1");
        var result = await controller.DeleteUser("admin-last", new AdminActionReasonDto { Reason = "reason" });

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task AdjustCredits_ReturnsOk_WhenSuccessful()
    {
        _adminService
            .Setup(s => s.AdjustCreditsAsync(It.IsAny<AdminCreditAdjustmentDto>(), "admin-1"))
            .ReturnsAsync((true, "ok", 12));

        var controller = CreateController("admin-1");
        var result = await controller.AdjustCredits(new AdminCreditAdjustmentDto
        {
            UserId = "u1",
            Amount = 5,
            Reason = "manual"
        });

        Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public async Task AdjustCredits_ReturnsBadRequest_WhenWouldGoNegative()
    {
        _adminService
            .Setup(s => s.AdjustCreditsAsync(It.IsAny<AdminCreditAdjustmentDto>(), "admin-1"))
            .ReturnsAsync((false, "User credit balance cannot go negative", 0));

        var controller = CreateController("admin-1");
        var result = await controller.AdjustCredits(new AdminCreditAdjustmentDto
        {
            UserId = "u1",
            Amount = -999,
            Reason = "manual"
        });

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task CreateCoupon_ReturnsCreated_WhenValid()
    {
        _adminService
            .Setup(s => s.CreateCouponAsync(It.IsAny<CouponCreateDto>(), "admin-1"))
            .ReturnsAsync((new Coupon { Id = 1, Code = "SAVE20", DiscountType = DiscountType.Percentage, DiscountValue = 20, MaxUsages = 100, CreatedByAdminId = "admin-1" }, null));

        var controller = CreateController("admin-1");
        var result = await controller.CreateCoupon(new CouponCreateDto
        {
            Code = "SAVE20",
            DiscountType = DiscountType.Percentage,
            DiscountValue = 20,
            MaxUsages = 100
        });

        Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public async Task CreateCoupon_ReturnsCreated_WithWarning_When100Percent()
    {
        _adminService
            .Setup(s => s.CreateCouponAsync(It.IsAny<CouponCreateDto>(), "admin-1"))
            .ReturnsAsync((new Coupon { Id = 2, Code = "FREE", DiscountType = DiscountType.Percentage, DiscountValue = 100, MaxUsages = 10, CreatedByAdminId = "admin-1" }, "100% discount coupon created - this allows free purchases"));

        var controller = CreateController("admin-1");
        var result = await controller.CreateCoupon(new CouponCreateDto
        {
            Code = "FREE",
            DiscountType = DiscountType.Percentage,
            DiscountValue = 100,
            MaxUsages = 10
        });

        Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public async Task CreateCoupon_ReturnsConflict_WhenCodeExists()
    {
        _adminService
            .Setup(s => s.CreateCouponAsync(It.IsAny<CouponCreateDto>(), "admin-1"))
            .ReturnsAsync((null, null));

        var controller = CreateController("admin-1");
        var result = await controller.CreateCoupon(new CouponCreateDto
        {
            Code = "SAVE20",
            DiscountType = DiscountType.Percentage,
            DiscountValue = 20,
            MaxUsages = 100
        });

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task GetAuditLogs_ReturnsOk_WithPaginatedLogs()
    {
        _adminService
            .Setup(s => s.GetAuditLogsAsync(1, 50, null))
            .ReturnsAsync((new List<AdminAuditLogDto> { new() { Id = 1, Action = "CreditsAdded", AdminEmail = "a@example.com" } }, 1));

        var controller = CreateController();
        var result = await controller.GetAuditLogs();

        Assert.IsType<OkObjectResult>(result);
    }

    private AdminController CreateController(string userId = "admin-1")
    {
        var controller = new AdminController(_replicateApiClient.Object, _adminService.Object, _logger.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(new[]
                    {
                        new Claim(ClaimTypes.NameIdentifier, userId),
                        new Claim(ClaimTypes.Role, "Admin")
                    }, "Test"))
                }
            }
        };

        return controller;
    }
}
