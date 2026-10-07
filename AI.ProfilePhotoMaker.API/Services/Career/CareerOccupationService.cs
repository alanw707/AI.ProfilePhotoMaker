using System.Text.Json;
using System.Text.RegularExpressions;
using AI.ProfilePhotoMaker.API.Data;
using AI.ProfilePhotoMaker.API.Models.Career;
using Microsoft.EntityFrameworkCore;

namespace AI.ProfilePhotoMaker.API.Services.Career;

public interface ICareerOccupationService
{
    Task<CareerOutcome<CareerOccupationMatchDto>> GetAsync(string ownerId, Guid id, CancellationToken ct = default);
    Task<CareerOutcome<CareerGoalDto>> ConfirmAsync(string ownerId, Guid id, ConfirmOccupationRequest request, VersionPrecondition precondition, CancellationToken ct = default);
    Task<CareerOutcome<CareerOccupationMatchDto>> DismissAsync(string ownerId, Guid id, CancellationToken ct = default);
    CareerOutcome<CareerOccupationReferenceDto> GetReference();
}

/// <summary>
/// Reads, confirms and dismisses occupation matches (ADR 0010). Matching itself happens in
/// the agent runner; confirming is the only way an occupation reaches the goal.
/// </summary>
public sealed class CareerOccupationService : ICareerOccupationService
{
    private static readonly Regex CodePattern = new(@"^\d{2}-\d{4}\.\d{2}$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private readonly ApplicationDbContext _db;
    private readonly IOccupationReference _reference;
    private readonly TimeProvider _clock;

    public CareerOccupationService(ApplicationDbContext db, IOccupationReference reference, TimeProvider clock)
    {
        _db = db;
        _reference = reference;
        _clock = clock;
    }

    public async Task<CareerOutcome<CareerOccupationMatchDto>> GetAsync(string ownerId, Guid id, CancellationToken ct = default)
    {
        // Ownership first, so another owner's id reveals nothing.
        var match = await _db.CareerOccupationMatches.AsNoTracking().FirstOrDefaultAsync(m => m.Id == id && m.OwnerId == ownerId, ct);
        if (match == null)
        {
            return MatchNotFound<CareerOccupationMatchDto>();
        }
        return await ToOutcomeAsync(match, ct);
    }

    public async Task<CareerOutcome<CareerOccupationMatchDto>> DismissAsync(string ownerId, Guid id, CancellationToken ct = default)
    {
        // Two passes at most: a lost race re-reads the decision that won.
        for (var attempt = 0; attempt < 2; attempt++)
        {
            var match = await _db.CareerOccupationMatches.FirstOrDefaultAsync(m => m.Id == id && m.OwnerId == ownerId, ct);
            if (match == null)
            {
                return MatchNotFound<CareerOccupationMatchDto>();
            }
            if (match.Status == CareerMatchStatuses.Confirmed)
            {
                return NotConfirmable<CareerOccupationMatchDto>("This match was already confirmed into your goal.");
            }

            // Dismissing twice, or an unsupported result, changes nothing.
            if (match.Status != CareerMatchStatuses.Proposed)
            {
                return await ToOutcomeAsync(match, ct);
            }

            match.Status = CareerMatchStatuses.Dismissed;
            match.DecidedAt = Now();
            try
            {
                await _db.SaveChangesAsync(ct);
                return await ToOutcomeAsync(match, ct);
            }
            catch (DbUpdateConcurrencyException)
            {
                // A confirm (or another dismiss) decided the match first; answer from what won.
                _db.ChangeTracker.Clear();
            }
        }
        return CareerOutcome<CareerOccupationMatchDto>.Busy("CareerRunBusy", "Too many requests at once. Try again shortly.", 1);
    }

    public async Task<CareerOutcome<CareerGoalDto>> ConfirmAsync(
        string ownerId, Guid id, ConfirmOccupationRequest request, VersionPrecondition precondition, CancellationToken ct = default)
    {
        var match = await _db.CareerOccupationMatches.FirstOrDefaultAsync(m => m.Id == id && m.OwnerId == ownerId, ct);
        if (match == null)
        {
            return MatchNotFound<CareerGoalDto>();
        }

        var code = request.OccupationCode?.Trim();
        if (string.IsNullOrEmpty(code) || !CodePattern.IsMatch(code))
        {
            return CareerOutcome<CareerGoalDto>.Invalid(new Dictionary<string, string>
            {
                ["occupationCode"] = "Choose one of the suggested occupations (for example 15-1252.00)."
            });
        }

        var goal = await _db.CareerGoals.FirstOrDefaultAsync(g => g.OwnerId == ownerId, ct);
        if (goal == null)
        {
            return CareerOutcome<CareerGoalDto>.AlreadyExists(
                CareerOccupationErrorCodes.GoalRequired, "Set a career goal before confirming an occupation.");
        }

        // State before the precondition, so a repeated confirm says "already decided" rather than "stale".
        var stored = JsonSerializer.Deserialize<StoredOccupationResult>(match.ResultJson, OccupationMatchJson.Options);
        var candidate = stored?.Candidates.FirstOrDefault(c => c.Code == code);
        if (match.Status != CareerMatchStatuses.Proposed || candidate == null)
        {
            return NotConfirmable<CareerGoalDto>(match.Status == CareerMatchStatuses.Proposed
                ? "That occupation is not one of the suggested matches."
                : "This match can no longer be confirmed.");
        }

        var profileVersion = await _db.CareerProfiles.AsNoTracking()
            .Where(p => p.OwnerId == ownerId).Select(p => (int?)p.ActiveVersionNumber).FirstOrDefaultAsync(ct);
        if (profileVersion != match.PinnedProfileVersion)
        {
            return CareerOutcome<CareerGoalDto>.AlreadyExists(
                CareerOccupationErrorCodes.MatchStale, "Your profile changed since this match. Start a new match.");
        }

        if (!precondition.IsPresent)
        {
            return CareerOutcome<CareerGoalDto>.PreconditionRequired();
        }
        if (precondition.ExpectedVersion != goal.ActiveVersionNumber)
        {
            return CareerOutcome<CareerGoalDto>.Conflict(goal.ActiveVersionNumber);
        }

        var active = await _db.CareerGoalVersions.AsNoTracking().FirstAsync(
            v => v.OwnerId == ownerId && v.CareerGoalId == goal.Id && v.VersionNumber == goal.ActiveVersionNumber, ct);
        var now = Now();
        var next = new CareerGoalVersion
        {
            Id = Guid.NewGuid(),
            CareerGoalId = goal.Id,
            OwnerId = ownerId,
            VersionNumber = goal.ActiveVersionNumber + 1,
            TargetRole = active.TargetRole,
            TargetLocation = active.TargetLocation,
            WorkArrangement = active.WorkArrangement,
            DesiredPayMin = active.DesiredPayMin,
            DesiredPayMax = active.DesiredPayMax,
            WeeklyEffortHours = active.WeeklyEffortHours,
            BasedOnProfileVersion = profileVersion,
            Source = CareerOccupationSources.OccupationMatch,
            ConfirmedAt = now,
            CreatedAt = now,
            OccupationCode = candidate.Code,
            OccupationTitle = candidate.Title,
            OccupationReferenceRelease = match.ReferenceRelease,
            OccupationMatchId = match.Id
        };
        // Confirming an occupation keeps a location the user already saved.
        CareerProfileService.CarryPreferredArea(active, next);

        goal.ActiveVersionNumber = next.VersionNumber;
        goal.UpdatedAt = now;
        _db.CareerGoalVersions.Add(next);
        match.Status = CareerMatchStatuses.Confirmed;
        match.ConfirmedCode = candidate.Code;
        match.ConfirmedIntoGoalVersion = next.VersionNumber;
        match.DecidedAt = now;

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (CareerProfileService.IsLostRace(ex))
        {
            // The goal moved (or this match was decided) while we worked: the same answer as a stale If-Match.
            _db.ChangeTracker.Clear();
            var current = await _db.CareerGoals.AsNoTracking().FirstOrDefaultAsync(g => g.OwnerId == ownerId, ct);
            return CareerOutcome<CareerGoalDto>.Conflict(current?.ActiveVersionNumber ?? 0);
        }

        return CareerOutcome<CareerGoalDto>.Ok(CareerProfileService.ToDto(goal, next, profileVersion));
    }

    public CareerOutcome<CareerOccupationReferenceDto> GetReference()
    {
        var data = _reference.Data;
        if (data == null)
        {
            return CareerOutcome<CareerOccupationReferenceDto>.ReferenceUnavailable();
        }
        var s = data.Source;
        return CareerOutcome<CareerOccupationReferenceDto>.Ok(new CareerOccupationReferenceDto(
            s.Name, s.Release, s.ReleaseDate, s.Taxonomy, s.License, s.LicenseUrl, s.Url, s.Attribution, data.Occupations.Count));
    }

    // ---- Mapping ---------------------------------------------------------------

    private DateTime Now() => _clock.GetUtcNow().UtcDateTime;

    private async Task<CareerOutcome<CareerOccupationMatchDto>> ToOutcomeAsync(CareerOccupationMatch match, CancellationToken ct)
    {
        var data = _reference.Data;
        if (data == null)
        {
            return CareerOutcome<CareerOccupationMatchDto>.ReferenceUnavailable();
        }

        var stored = JsonSerializer.Deserialize<StoredOccupationResult>(match.ResultJson, OccupationMatchJson.Options)
            ?? new StoredOccupationResult(Array.Empty<OccupationCandidate>(), null);
        var clarification = match.ClarificationJson == null
            ? null
            : JsonSerializer.Deserialize<CareerMatchClarificationDto>(match.ClarificationJson, OccupationMatchJson.Options);
        var currentProfile = await _db.CareerProfiles.AsNoTracking()
            .Where(p => p.OwnerId == match.OwnerId).Select(p => (int?)p.ActiveVersionNumber).FirstOrDefaultAsync(ct);

        return CareerOutcome<CareerOccupationMatchDto>.Ok(new CareerOccupationMatchDto(
            match.Id, match.RunId, match.Status, match.PinnedProfileVersion, match.PinnedGoalVersion,
            ProfileChanged: currentProfile != match.PinnedProfileVersion,
            data.Source, match.MatcherVersion, stored.Candidates, clarification, stored.Guidance,
            match.ConfirmedCode, match.ConfirmedIntoGoalVersion,
            DateTime.SpecifyKind(match.CreatedAt, DateTimeKind.Utc),
            match.DecidedAt is { } decided ? DateTime.SpecifyKind(decided, DateTimeKind.Utc) : null));
    }

    private static CareerOutcome<T> MatchNotFound<T>() =>
        CareerOutcome<T>.NotFound(CareerOccupationErrorCodes.MatchNotFound, "That occupation match was not found.");

    private static CareerOutcome<T> NotConfirmable<T>(string message) =>
        CareerOutcome<T>.AlreadyExists(CareerOccupationErrorCodes.MatchNotConfirmable, message);
}

public static class CareerOccupationSources
{
    public const string OccupationMatch = "occupation_match";
}
