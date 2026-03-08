using System.Text.Json;
using AI.ProfilePhotoMaker.API.Data;
using AI.ProfilePhotoMaker.API.Infrastructure.Logging;
using AI.ProfilePhotoMaker.API.Models;
using AI.ProfilePhotoMaker.API.Models.DTOs;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace AI.ProfilePhotoMaker.API.Services;

public class AdminService : IAdminService
{
    private const int RecentPurchaseLimit = 8;
    private const int RecentImageLimit = 8;
    private const int ActivityHistoryLimit = 30;

    private readonly ApplicationDbContext _context;
    private readonly IUserProfileRepository _userProfileRepository;
    private readonly ICreditPackageService _creditPackageService;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<AdminService> _logger;

    public AdminService(
        ApplicationDbContext context,
        IUserProfileRepository userProfileRepository,
        ICreditPackageService creditPackageService,
        UserManager<ApplicationUser> userManager,
        ILogger<AdminService> logger)
    {
        _context = context;
        _userProfileRepository = userProfileRepository;
        _creditPackageService = creditPackageService;
        _userManager = userManager;
        _logger = logger;
    }

    public async Task<(List<AdminUserListDto> Users, int TotalCount)> GetUsersAsync(int page, int pageSize, string? searchTerm)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);

        var query = from u in _context.Users
                    join p in _context.UserProfiles on u.Id equals p.UserId into profiles
                    from profile in profiles.DefaultIfEmpty()
                    select new { u, profile };

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var term = searchTerm.Trim();
            query = query.Where(x =>
                (x.u.Email != null && x.u.Email.Contains(term)) ||
                (x.u.FirstName != null && x.u.FirstName.Contains(term)) ||
                (x.u.LastName != null && x.u.LastName.Contains(term)));
        }

        var totalCount = await query.CountAsync();
        var users = await query
            .OrderByDescending(x => x.u.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new AdminUserListDto
            {
                Id = x.u.Id,
                Email = x.u.Email ?? string.Empty,
                FirstName = x.profile != null ? x.profile.FirstName ?? x.u.FirstName : x.u.FirstName,
                LastName = x.profile != null ? x.profile.LastName ?? x.u.LastName : x.u.LastName,
                Credits = x.profile != null ? x.profile.Credits : 0,
                IsLockedOut = x.u.LockoutEnd.HasValue && x.u.LockoutEnd.Value > DateTimeOffset.UtcNow,
                CreatedAt = x.u.CreatedAt,
                LastLoginAt = null
            })
            .ToListAsync();

        return (users, totalCount);
    }

    public async Task<AdminUserDiagnosticsDto?> GetUserDiagnosticsAsync(string userId)
    {
        var user = await _context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId);
        if (user == null)
        {
            return null;
        }

        var profile = await _userProfileRepository.GetByUserIdLightAsync(userId);
        var profileStats = await _userProfileRepository.GetUserProfileStatsAsync(userId);
        var profileWithRecentImages = await _userProfileRepository.GetProfileWithRecentImagesAsync(userId, RecentImageLimit);
        var purchases = (await _creditPackageService.GetUserPurchaseHistoryAsync(userId))
            .Take(RecentPurchaseLimit)
            .ToList();
        var usageLogs = await _context.UsageLogs
            .AsNoTracking()
            .Where(log => log.UserId == userId)
            .OrderByDescending(log => log.CreatedAt)
            .Take(12)
            .ToListAsync();
        var modelRequests = await _context.ModelCreationRequests
            .AsNoTracking()
            .Where(request => request.UserId == userId)
            .OrderByDescending(request => request.CreatedAt)
            .Take(8)
            .ToListAsync();
        var pendingGenerationRequests = await _context.PendingGenerationRequests
            .AsNoTracking()
            .Where(request => request.UserId == userId)
            .OrderByDescending(request => request.CreatedAt)
            .Take(8)
            .ToListAsync();
        var adminAuditLogs = await _context.AdminAuditLogs
            .AsNoTracking()
            .Where(log => log.TargetUserId == userId)
            .OrderByDescending(log => log.CreatedAt)
            .Take(8)
            .ToListAsync();
        var roles = await _userManager.GetRolesAsync(user) ?? Array.Empty<string>();
        var adminEmails = await ResolveAdminEmailsAsync(adminAuditLogs);

        var userDetail = new AdminUserDetailDto
        {
            Id = user.Id,
            Email = user.Email ?? string.Empty,
            FirstName = profile?.FirstName ?? user.FirstName,
            LastName = profile?.LastName ?? user.LastName,
            Credits = profile?.Credits ?? 0,
            IsLockedOut = user.LockoutEnd.HasValue && user.LockoutEnd.Value > DateTimeOffset.UtcNow,
            CreatedAt = user.CreatedAt,
            LastLoginAt = null,
            Gender = profile?.Gender,
            Ethnicity = profile?.Ethnicity,
            SubscriptionTier = profile?.SubscriptionTier.ToString() ?? SubscriptionTier.Basic.ToString(),
            EmailConfirmed = user.EmailConfirmed,
            Roles = roles.ToList()
        };

        var recentImages = profileWithRecentImages?.RecentImages
            .Select(image => new AdminRecentImageDto
            {
                Id = image.Id,
                ImageUrl = ResolveImageUrl(image),
                Kind = image.IsGenerated ? "Generated" : image.IsOriginalUpload ? "Upload" : "Image",
                Style = string.IsNullOrWhiteSpace(image.Style) ? null : image.Style,
                CreatedAt = image.CreatedAt,
                IsGenerated = image.IsGenerated,
                IsOriginalUpload = image.IsOriginalUpload
            })
            .ToList() ?? new List<AdminRecentImageDto>();

        var recentPurchases = purchases
            .Select(purchase => new AdminCreditPurchaseHistoryDto
            {
                Id = purchase.Id,
                PackageName = purchase.Package?.Name ?? $"Package {purchase.PackageId}",
                CreditsAwarded = purchase.CreditsAwarded,
                AmountPaid = purchase.AmountPaid,
                PaymentProvider = purchase.PaymentProvider,
                Status = purchase.Status.ToString(),
                PurchaseDate = purchase.PurchaseDate,
                CompletedAt = purchase.CompletedAt
            })
            .ToList();

        var recentAdminActions = adminAuditLogs
            .Select(log => new AdminUserAdminActionDto
            {
                Id = log.Id,
                AdminEmail = adminEmails.TryGetValue(log.AdminUserId, out var adminEmail) ? adminEmail : log.AdminUserId,
                Action = log.Action,
                Details = log.Details,
                OldValue = log.OldValue,
                NewValue = log.NewValue,
                CreatedAt = log.CreatedAt
            })
            .ToList();

        var activityHistory = BuildActivityHistory(
            usageLogs,
            recentPurchases,
            recentImages,
            modelRequests,
            pendingGenerationRequests,
            recentAdminActions);

        var totalCreditsConsumed = usageLogs
            .Where(log => log.CreditsCost.HasValue && log.CreditsCost.Value > 0)
            .Sum(log => log.CreditsCost ?? 0);

        return new AdminUserDiagnosticsDto
        {
            User = userDetail,
            Metrics = new AdminUserMetricsDto
            {
                CurrentCredits = userDetail.Credits,
                TotalProcessedImages = profileStats?.TotalProcessedImages ?? 0,
                OriginalUploads = profileStats?.OriginalUploads ?? 0,
                GeneratedImages = profileStats?.GeneratedImages ?? 0,
                PurchaseCount = recentPurchases.Count,
                TotalCreditsPurchased = recentPurchases.Sum(purchase => purchase.CreditsAwarded),
                TotalCreditsConsumed = totalCreditsConsumed,
                LastPurchaseAt = recentPurchases.FirstOrDefault()?.PurchaseDate,
                LastImageUploadAt = profileStats?.LastImageUpload,
                LastImageGenerationAt = profileStats?.LastImageGeneration,
                LastActivityAt = activityHistory.FirstOrDefault()?.OccurredAt,
                HasUsageHistory = usageLogs.Count > 0 || recentPurchases.Count > 0 || recentImages.Count > 0
            },
            RecentPurchases = recentPurchases,
            RecentImages = recentImages,
            ActivityHistory = activityHistory,
            RecentAdminActions = recentAdminActions
        };
    }

    public async Task<bool> DeactivateUserAsync(string userId, string adminUserId, string reason)
    {
        if (userId == adminUserId)
        {
            return false;
        }

        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
        {
            return false;
        }

        await _userManager.SetLockoutEnabledAsync(user, true);
        var result = await _userManager.SetLockoutEndDateAsync(user, DateTimeOffset.MaxValue);
        if (!result.Succeeded)
        {
            return false;
        }

        await WriteAuditLogAsync(adminUserId, "UserDeactivated", userId, reason, null, DateTimeOffset.MaxValue.ToString("O"));
        return true;
    }

    public async Task<bool> ReactivateUserAsync(string userId, string adminUserId, string reason)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
        {
            return false;
        }

        var result = await _userManager.SetLockoutEndDateAsync(user, null);
        if (!result.Succeeded)
        {
            return false;
        }

        await WriteAuditLogAsync(adminUserId, "UserReactivated", userId, reason, DateTimeOffset.MaxValue.ToString("O"), null);
        return true;
    }

    public async Task<(bool Success, string Message)> DeleteUserAsync(string userId, string adminUserId, string reason)
    {
        if (userId == adminUserId)
        {
            return (false, "Cannot delete your own account");
        }

        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
        {
            return (false, "User not found");
        }

        var adminUsers = await _userManager.GetUsersInRoleAsync("Admin");
        if (adminUsers.Count == 1 && adminUsers.Any(a => a.Id == userId))
        {
            return (false, "Cannot delete the last admin user");
        }

        var strategy = _context.Database.CreateExecutionStrategy();
        try
        {
            return await strategy.ExecuteAsync(async () =>
            {
                await using var transaction = await _context.Database.BeginTransactionAsync();

                var profile = await _context.UserProfiles.FirstOrDefaultAsync(p => p.UserId == userId);
                if (profile != null)
                {
                    var processedImages = await _context.ProcessedImages
                        .Where(p => p.UserProfileId == profile.Id)
                        .ToListAsync();
                    _context.ProcessedImages.RemoveRange(processedImages);

                    var usageLogs = await _context.UsageLogs.Where(u => u.UserId == userId).ToListAsync();
                    _context.UsageLogs.RemoveRange(usageLogs);

                    var styleSelections = await _context.UserStyleSelections
                        .Where(s => s.UserProfileId == profile.Id)
                        .ToListAsync();
                    _context.UserStyleSelections.RemoveRange(styleSelections);

                    _context.UserProfiles.Remove(profile);
                }

                var redemptions = await _context.CouponRedemptions.Where(r => r.UserId == userId).ToListAsync();
                _context.CouponRedemptions.RemoveRange(redemptions);

                var purchases = await _context.CreditPurchases.Where(p => p.UserId == userId).ToListAsync();
                _context.CreditPurchases.RemoveRange(purchases);

                var transactions = await _context.PaymentTransactions.Where(t => t.UserId == userId).ToListAsync();
                _context.PaymentTransactions.RemoveRange(transactions);

                var predictions = await _context.Predictions.Where(p => p.UserId == userId).ToListAsync();
                _context.Predictions.RemoveRange(predictions);

                var modelRequests = await _context.ModelCreationRequests.Where(m => m.UserId == userId).ToListAsync();
                _context.ModelCreationRequests.RemoveRange(modelRequests);

                var pendingGenerationRequests = await _context.PendingGenerationRequests.Where(p => p.UserId == userId).ToListAsync();
                _context.PendingGenerationRequests.RemoveRange(pendingGenerationRequests);

                var feedbackSubmissions = await _context.FeedbackSubmissions.Where(f => f.UserId == userId).ToListAsync();
                _context.FeedbackSubmissions.RemoveRange(feedbackSubmissions);

                var subscriptions = await _context.Subscriptions.Where(s => s.UserId == userId).ToListAsync();
                _context.Subscriptions.RemoveRange(subscriptions);

                var retentionLogs = await _context.RetentionDeletionWarningLogs.Where(r => r.UserId == userId).ToListAsync();
                _context.RetentionDeletionWarningLogs.RemoveRange(retentionLogs);

                await _context.SaveChangesAsync();

                var deleteResult = await _userManager.DeleteAsync(user);
                if (!deleteResult.Succeeded)
                {
                    await transaction.RollbackAsync();
                    return (false, "Failed to delete user identity");
                }

                AddAuditLog(adminUserId, "UserDeleted", userId, reason, null, JsonSerializer.Serialize(new { DeletedUserId = userId }));
                await _context.SaveChangesAsync();

                await transaction.CommitAsync();
                return (true, "User deleted");
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete user {UserId}", LoggingSanitizer.SanitizeId(userId));
            return (false, "Failed to delete user");
        }
    }

    public async Task<(bool Success, string Message, int NewBalance)> AdjustCreditsAsync(AdminCreditAdjustmentDto dto, string adminUserId)
    {
        // Admin credit adjustment limits to prevent abuse
        const int MaxCreditsPerAdjustment = 10000;
        const int MaxCreditBalance = 100000;
        var isSqlServer = _context.Database.IsSqlServer();

        if (dto.Amount > MaxCreditsPerAdjustment)
        {
            return (false, $"Cannot add more than {MaxCreditsPerAdjustment} credits in a single adjustment", 0);
        }

        if (dto.Amount < -MaxCreditsPerAdjustment)
        {
            return (false, $"Cannot subtract more than {MaxCreditsPerAdjustment} credits in a single adjustment", 0);
        }

        var strategy = _context.Database.CreateExecutionStrategy();
        try
        {
            return await strategy.ExecuteAsync(async () =>
            {
                await using var transaction = isSqlServer
                    ? await _context.Database.BeginTransactionAsync()
                    : null;

                // Read the profile with a lock hint to prevent concurrent modifications.
                // Use FromSqlRaw with UPDLOCK to serialize concurrent credit adjustments for the same user.
                UserProfile? profile;
                if (isSqlServer)
                {
                    profile = await _context.UserProfiles
                        .FromSqlRaw("SELECT * FROM UserProfiles WITH (UPDLOCK) WHERE UserId = {0}", dto.UserId)
                        .FirstOrDefaultAsync();
                }
                else
                {
                    profile = await _context.UserProfiles.FirstOrDefaultAsync(p => p.UserId == dto.UserId);
                }

                if (profile == null)
                {
                    return (false, "User profile not found", 0);
                }

                var oldValue = profile.Credits;
                var newValue = oldValue + dto.Amount;
                if (newValue < 0)
                {
                    return (false, "User credit balance cannot go negative", oldValue);
                }

                if (newValue > MaxCreditBalance)
                {
                    return (false, $"User credit balance cannot exceed {MaxCreditBalance} credits", oldValue);
                }

                profile.Credits = newValue;
                profile.UpdatedAt = DateTime.UtcNow;

                var action = dto.Amount >= 0 ? "CreditsAdded" : "CreditsSubtracted";
                AddAuditLog(adminUserId, action, dto.UserId, dto.Reason, oldValue.ToString(), newValue.ToString());

                await _context.SaveChangesAsync();
                if (transaction != null)
                {
                    await transaction.CommitAsync();
                }
                return (true, "Credits adjusted", newValue);
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to adjust credits for user {UserId}", LoggingSanitizer.SanitizeId(dto.UserId));
            return (false, "Failed to adjust credits", 0);
        }
    }

    public async Task<List<CouponListDto>> GetCouponsAsync()
    {
        var now = DateTime.UtcNow;
        return await _context.Coupons
            .AsNoTracking()
            .OrderByDescending(c => c.CreatedAt)
            .Select(c => new CouponListDto
            {
                Id = c.Id,
                Code = c.Code,
                DiscountType = c.DiscountType.ToString(),
                DiscountValue = c.DiscountValue,
                MaxUsages = c.MaxUsages,
                CurrentUsages = c.CurrentUsages,
                ExpiresAt = c.ExpiresAt,
                IsActive = c.IsActive,
                IsExpired = c.ExpiresAt.HasValue && c.ExpiresAt.Value < now,
                CreatedAt = c.CreatedAt
            })
            .ToListAsync();
    }

    public async Task<(Coupon? Coupon, string? Warning)> CreateCouponAsync(CouponCreateDto dto, string adminUserId)
    {
        var code = dto.Code.Trim().ToUpperInvariant();
        var existing = await _context.Coupons.AnyAsync(c => c.Code == code);
        if (existing)
        {
            return (null, null);
        }

        var coupon = new Coupon
        {
            Code = code,
            DiscountType = dto.DiscountType,
            DiscountValue = dto.DiscountValue,
            MaxUsages = dto.MaxUsages,
            ExpiresAt = dto.ExpiresAt,
            IsActive = true,
            CreatedByAdminId = adminUserId,
            CreatedAt = DateTime.UtcNow
        };

        _context.Coupons.Add(coupon);
        await _context.SaveChangesAsync();

        string? warning = null;
        if (dto.DiscountType == DiscountType.Percentage && dto.DiscountValue == 100)
        {
            warning = "100% discount coupon created - this allows free purchases";
        }
        else if (dto.DiscountType == DiscountType.FixedAmount)
        {
            // Check if the fixed discount exceeds any active package price (effectively free)
            var maxPackagePrice = await _context.CreditPackages
                .Where(p => p.IsActive)
                .MaxAsync(p => (decimal?)p.Price) ?? 0m;

            if (dto.DiscountValue >= maxPackagePrice && maxPackagePrice > 0)
            {
                warning = $"Fixed discount (${dto.DiscountValue:F2}) meets or exceeds the highest package price (${maxPackagePrice:F2}) - this effectively allows free purchases";
            }
        }

        var details = warning == null
            ? JsonSerializer.Serialize(new { Coupon = code })
            : JsonSerializer.Serialize(new { Coupon = code, Warning = warning });
        await WriteAuditLogAsync(adminUserId, "CouponCreated", null, details, null, null);

        return (coupon, warning);
    }

    public async Task<bool> UpdateCouponAsync(int couponId, CouponUpdateDto dto, string adminUserId)
    {
        var coupon = await _context.Coupons.FirstOrDefaultAsync(c => c.Id == couponId);
        if (coupon == null)
        {
            return false;
        }

        var oldState = JsonSerializer.Serialize(new { coupon.MaxUsages, coupon.ExpiresAt, coupon.IsActive });

        if (dto.MaxUsages.HasValue)
        {
            coupon.MaxUsages = dto.MaxUsages.Value;
        }

        if (dto.ExpiresAt.HasValue)
        {
            coupon.ExpiresAt = dto.ExpiresAt;
        }

        if (dto.IsActive.HasValue)
        {
            coupon.IsActive = dto.IsActive.Value;
        }

        coupon.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        var newState = JsonSerializer.Serialize(new { coupon.MaxUsages, coupon.ExpiresAt, coupon.IsActive });
        await WriteAuditLogAsync(adminUserId, "CouponUpdated", null, coupon.Code, oldState, newState);
        return true;
    }

    public async Task<bool> DeleteCouponAsync(int couponId, string adminUserId)
    {
        var coupon = await _context.Coupons.FirstOrDefaultAsync(c => c.Id == couponId);
        if (coupon == null)
        {
            return false;
        }

        coupon.IsActive = false;
        coupon.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        await WriteAuditLogAsync(adminUserId, "CouponDeleted", null, coupon.Code, null, "Inactive");
        return true;
    }

    public async Task<(List<AdminAuditLogDto> Logs, int TotalCount)> GetAuditLogsAsync(int page, int pageSize, string? actionFilter)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);

        var query = _context.AdminAuditLogs.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(actionFilter))
        {
            query = query.Where(l => l.Action == actionFilter);
        }

        var totalCount = await query.CountAsync();

        var logs = await query
            .OrderByDescending(l => l.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var adminIds = logs.Select(l => l.AdminUserId).Distinct().ToList();
        var targetIds = logs.Where(l => !string.IsNullOrWhiteSpace(l.TargetUserId)).Select(l => l.TargetUserId!).Distinct().ToList();
        var users = await _context.Users.AsNoTracking()
            .Where(u => adminIds.Contains(u.Id) || targetIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.Email ?? string.Empty);

        var dtos = logs.Select(log => new AdminAuditLogDto
        {
            Id = log.Id,
            AdminEmail = users.TryGetValue(log.AdminUserId, out var adminEmail) ? adminEmail : log.AdminUserId,
            Action = log.Action,
            TargetUserEmail = !string.IsNullOrWhiteSpace(log.TargetUserId) && users.TryGetValue(log.TargetUserId, out var targetEmail)
                ? targetEmail
                : null,
            Details = log.Details,
            OldValue = log.OldValue,
            NewValue = log.NewValue,
            CreatedAt = log.CreatedAt
        }).ToList();

        return (dtos, totalCount);
    }

    public async Task<AdminDashboardDto> GetDashboardAsync()
    {
        var now = DateTime.UtcNow;
        var totalUsers = await _context.Users.CountAsync();
        var activeUsers = await _context.Users.CountAsync(u => !u.LockoutEnd.HasValue || u.LockoutEnd <= now);
        var totalCreditsOutstanding = await _context.UserProfiles.SumAsync(p => (int?)p.Credits) ?? 0;
        var totalCreditsPurchased = await _context.CreditPurchases.SumAsync(p => (long?)p.CreditsAwarded) ?? 0;
        var activeCoupons = await _context.Coupons.CountAsync(c => c.IsActive && (!c.ExpiresAt.HasValue || c.ExpiresAt > now));

        return new AdminDashboardDto
        {
            TotalUsers = totalUsers,
            ActiveUsers = activeUsers,
            TotalCreditsOutstanding = totalCreditsOutstanding,
            TotalCreditsPurchased = totalCreditsPurchased,
            ActiveCoupons = activeCoupons
        };
    }

    private async Task<Dictionary<string, string>> ResolveAdminEmailsAsync(List<AdminAuditLog> adminAuditLogs)
    {
        var adminIds = adminAuditLogs
            .Select(log => log.AdminUserId)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct()
            .ToList();

        if (adminIds.Count == 0)
        {
            return new Dictionary<string, string>();
        }

        return await _context.Users
            .AsNoTracking()
            .Where(user => adminIds.Contains(user.Id))
            .ToDictionaryAsync(user => user.Id, user => user.Email ?? user.Id);
    }

    private static string ResolveImageUrl(AI.ProfilePhotoMaker.API.Data.ProcessedImageDto image)
    {
        if (image.IsGenerated && !string.IsNullOrWhiteSpace(image.ProcessedImageUrl))
        {
            return image.ProcessedImageUrl;
        }

        if (image.IsOriginalUpload && !string.IsNullOrWhiteSpace(image.OriginalImageUrl))
        {
            return image.OriginalImageUrl;
        }

        return !string.IsNullOrWhiteSpace(image.ProcessedImageUrl)
            ? image.ProcessedImageUrl
            : image.OriginalImageUrl;
    }

    private static List<AdminUserActivityDto> BuildActivityHistory(
        List<UsageLog> usageLogs,
        List<AdminCreditPurchaseHistoryDto> recentPurchases,
        List<AdminRecentImageDto> recentImages,
        List<ModelCreationRequest> modelRequests,
        List<PendingGenerationRequest> pendingGenerationRequests,
        List<AdminUserAdminActionDto> recentAdminActions)
    {
        var activity = new List<AdminUserActivityDto>();

        activity.AddRange(usageLogs.Select(log => new AdminUserActivityDto
        {
            EventType = "usage",
            Title = HumanizeAction(log.Action),
            Description = log.Details,
            Status = log.CreditsCost.HasValue ? $"Consumed {log.CreditsCost.Value} credits" : null,
            OccurredAt = log.CreatedAt,
            CreditsDelta = log.CreditsCost.HasValue ? -log.CreditsCost.Value : null,
            CreditsRemaining = log.CreditsRemaining
        }));

        activity.AddRange(recentPurchases.Select(purchase => new AdminUserActivityDto
        {
            EventType = "purchase",
            Title = "Credits purchased",
            Description = $"{purchase.PackageName} via {purchase.PaymentProvider}",
            Status = purchase.Status,
            OccurredAt = purchase.PurchaseDate,
            CreditsDelta = purchase.CreditsAwarded
        }));

        activity.AddRange(recentImages.Select(image => new AdminUserActivityDto
        {
            EventType = image.IsGenerated ? "generation" : "upload",
            Title = image.IsGenerated ? "Generated new headshot" : "Uploaded source image",
            Description = image.Style,
            Status = image.Kind,
            OccurredAt = image.CreatedAt,
            ImageUrl = image.ImageUrl
        }));

        activity.AddRange(modelRequests.Select(request => new AdminUserActivityDto
        {
            EventType = "model",
            Title = "Model training request",
            Description = request.ModelName,
            Status = request.Status.ToString(),
            OccurredAt = request.CompletedAt ?? request.CreatedAt
        }));

        activity.AddRange(pendingGenerationRequests.Select(request => new AdminUserActivityDto
        {
            EventType = "generation-request",
            Title = "Queued generation request",
            Description = $"Training request {request.TrainingRequestId}",
            Status = request.Status.ToString(),
            OccurredAt = request.CompletedAt ?? request.StartedAt ?? request.CreatedAt
        }));

        activity.AddRange(recentAdminActions.Select(action => new AdminUserActivityDto
        {
            EventType = "admin",
            Title = HumanizeAction(action.Action),
            Description = $"{action.AdminEmail}: {action.Details ?? "No details recorded"}",
            Status = "Admin action",
            OccurredAt = action.CreatedAt
        }));

        return activity
            .OrderByDescending(entry => entry.OccurredAt)
            .Take(ActivityHistoryLimit)
            .ToList();
    }

    private static string HumanizeAction(string action)
    {
        if (string.IsNullOrWhiteSpace(action))
        {
            return "Activity";
        }

        var builder = new System.Text.StringBuilder(action.Length + 8);
        for (var index = 0; index < action.Length; index++)
        {
            var character = action[index];
            if (character == '_' || character == '-')
            {
                builder.Append(' ');
                continue;
            }

            if (index > 0 && char.IsUpper(character) && !char.IsUpper(action[index - 1]))
            {
                builder.Append(' ');
            }

            builder.Append(builder.Length == 0 ? char.ToUpperInvariant(character) : character);
        }

        return builder.ToString();
    }

    /// <summary>
    /// Adds an audit log entry to the context WITHOUT calling SaveChangesAsync.
    /// Use inside transactions where the caller controls the save.
    /// </summary>
    private void AddAuditLog(string adminUserId, string action, string? targetUserId, string? details, string? oldValue, string? newValue)
    {
        _context.AdminAuditLogs.Add(new AdminAuditLog
        {
            AdminUserId = adminUserId,
            Action = action,
            TargetUserId = targetUserId,
            Details = details,
            OldValue = oldValue,
            NewValue = newValue,
            CreatedAt = DateTime.UtcNow
        });
    }

    /// <summary>
    /// Adds an audit log entry and immediately saves. Use outside transactions.
    /// </summary>
    private async Task WriteAuditLogAsync(string adminUserId, string action, string? targetUserId, string? details, string? oldValue, string? newValue)
    {
        AddAuditLog(adminUserId, action, targetUserId, details, oldValue, newValue);
        await _context.SaveChangesAsync();
    }
}
