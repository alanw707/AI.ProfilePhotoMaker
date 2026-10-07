using System.Security.Claims;
using AI.ProfilePhotoMaker.API.Data;
using AI.ProfilePhotoMaker.API.Models.Career;
using Microsoft.EntityFrameworkCore;

namespace AI.ProfilePhotoMaker.API.Services.Career;

public sealed record CareerDeletionDto(
    Guid Id, string Scope, string Status, int Attempts, string? LastError, DateTime CreatedAt, DateTime? CompletedAt);

public sealed class CreateCareerDeletionRequest
{
    public string? Scope { get; set; }
}

public sealed record CareerRetentionItemDto(string Key, string Label, string Retention, string Notes);
public sealed record CareerProcessorDto(string Name, string Purpose);
public sealed record CareerRetentionDto(IReadOnlyList<CareerRetentionItemDto> Items, IReadOnlyList<CareerProcessorDto> Processors);

public static class CareerPrivacyErrorCodes
{
    public const string ReauthRequired = "CareerReauthRequired";
    public const string DeletionNotFound = "CareerDeletionNotFound";
    public const string DeletionCompleted = "CareerDeletionCompleted";
}

/// <summary>"Signed in recently" (ADR 0020): <c>auth_time</c>, else <c>iat</c>, at most ten minutes old.</summary>
public static class CareerRecentAuth
{
    public static readonly TimeSpan MaxAge = TimeSpan.FromMinutes(10);

    public static bool IsRecent(ClaimsPrincipal user, DateTimeOffset now)
    {
        // A token with neither claim cannot prove when the user signed in, so it is not recent.
        var raw = user.FindFirst("auth_time")?.Value ?? user.FindFirst("iat")?.Value;
        if (!long.TryParse(raw, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var seconds)
            || seconds > DateTimeOffset.MaxValue.ToUnixTimeSeconds())
        {
            return false;
        }
        var signedIn = DateTimeOffset.FromUnixTimeSeconds(seconds);
        // A little skew is tolerated; a claim from the future is not trusted beyond that.
        return now - signedIn <= MaxAge && signedIn - now <= TimeSpan.FromMinutes(1);
    }
}

public interface ICareerPrivacyService
{
    Task<CareerOutcome<CareerDeletionDto>> RequestDeletionAsync(string ownerId, string? scope, CancellationToken ct = default);
    Task<CareerOutcome<CareerDeletionDto>> GetDeletionAsync(string ownerId, Guid id, CancellationToken ct = default);
    Task<CareerOutcome<CareerDeletionDto>> RetryDeletionAsync(string ownerId, Guid id, CancellationToken ct = default);
    CareerRetentionDto GetRetention();
}

public sealed class CareerPrivacyService : ICareerPrivacyService
{
    private readonly ApplicationDbContext _db;
    private readonly ICareerPrivateDataService _data;
    private readonly IConfiguration _configuration;

    public CareerPrivacyService(ApplicationDbContext db, ICareerPrivateDataService data, IConfiguration configuration)
    {
        _db = db;
        _data = data;
        _configuration = configuration;
    }

    public async Task<CareerOutcome<CareerDeletionDto>> RequestDeletionAsync(string ownerId, string? scope, CancellationToken ct = default)
    {
        var normalized = scope?.Trim().ToLowerInvariant();
        if (!CareerDeletionScopes.IsValid(normalized))
        {
            return CareerOutcome<CareerDeletionDto>.Invalid(new Dictionary<string, string>
            {
                ["scope"] = $"Choose {CareerDeletionScopes.RawDocuments} or {CareerDeletionScopes.CareerProfile}."
            });
        }
        return CareerOutcome<CareerDeletionDto>.Ok(ToDto(await _data.DeleteAsync(ownerId, normalized!, ct)));
    }

    public async Task<CareerOutcome<CareerDeletionDto>> GetDeletionAsync(string ownerId, Guid id, CancellationToken ct = default)
    {
        var request = await _db.CareerDeletionRequests.AsNoTracking().FirstOrDefaultAsync(r => r.Id == id && r.OwnerId == ownerId, ct);
        return request == null ? NotFound() : CareerOutcome<CareerDeletionDto>.Ok(ToDto(request));
    }

    public async Task<CareerOutcome<CareerDeletionDto>> RetryDeletionAsync(string ownerId, Guid id, CancellationToken ct = default)
    {
        var request = await _db.CareerDeletionRequests.FirstOrDefaultAsync(r => r.Id == id && r.OwnerId == ownerId, ct);
        if (request == null)
        {
            return NotFound();
        }
        if (request.Status == CareerDeletionStatuses.Completed)
        {
            return CareerOutcome<CareerDeletionDto>.AlreadyExists(CareerPrivacyErrorCodes.DeletionCompleted, "This deletion is already complete.");
        }
        return CareerOutcome<CareerDeletionDto>.Ok(ToDto(await _data.RetryAsync(request, ct)));
    }

    public CareerRetentionDto GetRetention()
    {
        var resumeDays = Math.Max(1, _configuration.GetValue<int?>(ResumeImportService.RetentionConfigKey) ?? 30);
        var backups = "Up to 35 days";
        return new CareerRetentionDto(
            new[]
            {
                new CareerRetentionItemDto("career_data", "Profile, goals, runs, analyses, roadmaps and materials",
                    "Until you delete it or delete your account", "Delete it any time from privacy controls; deletion needs a recent sign-in."),
                new CareerRetentionItemDto("exports", "Generated PDF and DOCX files",
                    $"{(int)CareerExportService.Lifetime.TotalHours} hours", "Files expire and are removed; you can generate a new one."),
                new CareerRetentionItemDto("raw_documents", "Uploaded resume files",
                    $"Until you delete them, and automatically after {resumeDays} days",
                    "Only the extracted proposal you accepted stays in your profile."),
                new CareerRetentionItemDto("backups", "Database backups",
                    backups, "Backups age out on the provider's schedule. A restore replays your deletions so deleted career data does not return.")
            },
            new[]
            {
                new CareerProcessorDto("OpenAI", "Text processing, only for assistant runs that use a model; your data is not used to train models.")
            });
    }

    private static CareerOutcome<CareerDeletionDto> NotFound() =>
        CareerOutcome<CareerDeletionDto>.NotFound(CareerPrivacyErrorCodes.DeletionNotFound, "That deletion was not found.");

    private static CareerDeletionDto ToDto(CareerDeletionRequest r) =>
        new(r.Id, r.Scope, r.Status, r.Attempts, r.LastError,
            DateTime.SpecifyKind(r.CreatedAt, DateTimeKind.Utc),
            r.CompletedAt is { } done ? DateTime.SpecifyKind(done, DateTimeKind.Utc) : null);
}
