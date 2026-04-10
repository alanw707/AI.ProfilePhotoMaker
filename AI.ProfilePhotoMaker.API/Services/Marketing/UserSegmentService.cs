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

        return segmentFilter switch
        {
            // Signed up + verified, never uploaded a single selfie
            SegmentFilters.NoUploads =>
                from u in baseQuery
                where !_db.Set<ProcessedImage>()
                    .Any(i => i.UserProfile.UserId == u.Id && i.IsOriginalUpload)
                select u,

            // Uploaded 1–4 selfies (previously blocked at 10-photo minimum, now eligible)
            SegmentFilters.StuckUnderMinimum =>
                from u in baseQuery
                let uploadCount = _db.Set<ProcessedImage>()
                    .Count(i => i.UserProfile.UserId == u.Id && i.IsOriginalUpload)
                where uploadCount >= 1 && uploadCount <= 4
                select u,

            // 5+ uploads but never started training
            SegmentFilters.HasUploadsNoModel =>
                from u in baseQuery
                let uploadCount = _db.Set<ProcessedImage>()
                    .Count(i => i.UserProfile.UserId == u.Id && i.IsOriginalUpload)
                where uploadCount >= 5
                    && !_db.Set<ModelCreationRequest>().Any(m =>
                        m.UserId == u.Id
                        && (m.Status == ModelCreationStatus.Ready
                            || m.Status == ModelCreationStatus.Creating
                            || m.Status == ModelCreationStatus.Pending))
                select u,

            // Has uploads but no activity in the last 30 days
            SegmentFilters.Inactive30d =>
                from u in baseQuery
                let cutoff = DateTime.UtcNow.AddDays(-30)
                where _db.Set<ProcessedImage>()
                          .Any(i => i.UserProfile.UserId == u.Id && i.IsOriginalUpload)
                      && !_db.Set<UsageLog>()
                          .Any(l => l.UserId == u.Id && l.CreatedAt >= cutoff)
                select u,

            // All verified, non-opted-out users
            SegmentFilters.AllVerified => baseQuery,

            _ => throw new ArgumentException($"Unknown segment filter: {segmentFilter}")
        };
    }
}
