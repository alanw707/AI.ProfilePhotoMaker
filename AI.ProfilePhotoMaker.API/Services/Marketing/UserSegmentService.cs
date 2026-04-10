using AI.ProfilePhotoMaker.API.Data;
using AI.ProfilePhotoMaker.API.Models;
using Microsoft.EntityFrameworkCore;

namespace AI.ProfilePhotoMaker.API.Services.Marketing;

public class UserSegmentService : IUserSegmentService
{
    private readonly ApplicationDbContext _db;

    public UserSegmentService(ApplicationDbContext db)
    {
        _db = db;
    }

    public Task<bool> IsValidSegmentFilter(string segmentFilter)
        => Task.FromResult(SegmentFilters.All.Contains(segmentFilter));

    public async Task<int> GetSegmentCountAsync(string segmentFilter)
        => await BuildSegmentQuery(segmentFilter).CountAsync();

    public async Task<IEnumerable<(string UserId, string Email)>> GetSegmentUsersAsync(
        string segmentFilter, int page, int pageSize)
    {
        var results = await BuildSegmentQuery(segmentFilter)
            .OrderBy(u => u.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(u => new { u.Id, u.Email })
            .ToListAsync();

        return results.Select(r => (r.Id, r.Email ?? string.Empty));
    }

    private IQueryable<ApplicationUser> BuildSegmentQuery(string segmentFilter)
    {
        // Base: email-verified, marketing opt-in
        var baseQuery = _db.Users
            .Where(u => u.EmailConfirmed && !u.MarketingOptOut && u.Email != null);

        // Pre-compute upload counts per user as an IQueryable (not materialized).
        // EF Core composes this into each downstream query as a single subquery/join
        // instead of a correlated COUNT per user row.
        var uploadsByUser = _db.UserProfiles
            .Select(up => new
            {
                up.UserId,
                UploadCount = up.ProcessedImages.Count(i => i.IsOriginalUpload)
            });

        switch (segmentFilter)
        {
            // Signed up + verified, never uploaded a single selfie
            case SegmentFilters.NoUploads:
            {
                var usersWithUploads = uploadsByUser
                    .Where(x => x.UploadCount > 0)
                    .Select(x => x.UserId);
                return baseQuery.Where(u => !usersWithUploads.Contains(u.Id));
            }

            // Uploaded 1–4 selfies (previously blocked at 10-photo minimum, now eligible)
            case SegmentFilters.StuckUnderMinimum:
            {
                var eligibleIds = uploadsByUser
                    .Where(x => x.UploadCount >= 1 && x.UploadCount <= 4)
                    .Select(x => x.UserId);
                return baseQuery.Where(u => eligibleIds.Contains(u.Id));
            }

            // 5+ uploads but never started training
            case SegmentFilters.HasUploadsNoModel:
            {
                var withUploads = uploadsByUser
                    .Where(x => x.UploadCount >= 5)
                    .Select(x => x.UserId);
                var withModel = _db.ModelCreationRequests
                    .Where(m => m.Status == ModelCreationStatus.Ready
                             || m.Status == ModelCreationStatus.Creating
                             || m.Status == ModelCreationStatus.Pending)
                    .Select(m => m.UserId);
                return baseQuery.Where(u => withUploads.Contains(u.Id) && !withModel.Contains(u.Id));
            }

            // Has uploads but no activity in the last 30 days
            case SegmentFilters.Inactive30d:
            {
                var cutoff = DateTime.UtcNow.AddDays(-30);
                var usersWithUploads = uploadsByUser
                    .Where(x => x.UploadCount > 0)
                    .Select(x => x.UserId);
                var recentlyActive = _db.UsageLogs
                    .Where(l => l.CreatedAt >= cutoff)
                    .Select(l => l.UserId);
                return baseQuery.Where(u => usersWithUploads.Contains(u.Id)
                                         && !recentlyActive.Contains(u.Id));
            }

            // All verified, non-opted-out users
            case SegmentFilters.AllVerified:
                return baseQuery;

            default:
                throw new ArgumentException($"Unknown segment filter: {segmentFilter}");
        }
    }
}
