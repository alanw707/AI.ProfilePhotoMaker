namespace AI.ProfilePhotoMaker.API.Services.Marketing;

public interface IUserSegmentService
{
    Task<int> GetSegmentCountAsync(string segmentFilter);
    Task<IEnumerable<(string UserId, string Email)>> GetSegmentUsersAsync(string segmentFilter, int page, int pageSize);
    Task<bool> IsValidSegmentFilter(string segmentFilter);
}

public static class SegmentFilters
{
    public const string NoUploads = "no-uploads";
    public const string StuckUnderMinimum = "stuck-under-minimum";
    public const string HasUploadsNoModel = "has-uploads-no-model";
    public const string Inactive30d = "inactive-30d";
    public const string AllVerified = "all-verified";

    public static readonly IReadOnlyList<string> All = new[]
    {
        NoUploads, StuckUnderMinimum, HasUploadsNoModel, Inactive30d, AllVerified
    };
}
