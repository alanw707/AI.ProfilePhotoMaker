using AI.ProfilePhotoMaker.API.Data;
using AI.ProfilePhotoMaker.API.Models.Career;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace AI.ProfilePhotoMaker.API.Services.Career;

/// <summary>
/// What the client sent in If-Match. <see cref="Absent"/> means no header; an
/// unparseable or foreign tag is <see cref="Mismatch"/> so it can never match.
/// </summary>
public readonly record struct VersionPrecondition(bool IsPresent, int ExpectedVersion)
{
    public static readonly VersionPrecondition Absent = new(false, 0);
    public static readonly VersionPrecondition Mismatch = new(true, -1);
    public static VersionPrecondition Expect(int version) => new(true, version);
}

public interface ICareerProfileService
{
    Task<CareerOutcome<CareerProfileDto>> GetProfileAsync(string ownerId, CancellationToken ct = default);
    Task<CareerOutcome<CareerProfileDto>> SaveProfileAsync(string ownerId, CareerProfileRequest request, VersionPrecondition precondition, CancellationToken ct = default);
    Task<CareerOutcome<IReadOnlyList<CareerProfileVersionSummaryDto>>> ListProfileVersionsAsync(string ownerId, CancellationToken ct = default);
    Task<CareerOutcome<CareerProfileVersionDto>> GetProfileVersionAsync(string ownerId, int version, CancellationToken ct = default);
    Task<CareerOutcome<CareerProfileDto>> RestoreProfileVersionAsync(string ownerId, int version, VersionPrecondition precondition, CancellationToken ct = default);

    /// <summary>
    /// Appends a version created by accepting a proposal (#379). <paramref name="baseVersion"/>
    /// is the profile version the proposal was pinned to (null when none existed);
    /// any other current version is a conflict. <paramref name="beforeCommit"/> receives
    /// the new version number so related rows are saved in the same SaveChanges.
    /// </summary>
    Task<CareerOutcome<CareerProfileDto>> AppendFromProposalAsync(
        string ownerId, ValidProfileFacts facts, string source, Guid proposalId, int? baseVersion,
        Action<int>? beforeCommit = null, CancellationToken ct = default);

    Task<CareerOutcome<CareerGoalDto>> GetGoalAsync(string ownerId, CancellationToken ct = default);
    Task<CareerOutcome<CareerGoalDto>> CreateGoalAsync(string ownerId, CareerGoalRequest request, CancellationToken ct = default);
    Task<CareerOutcome<CareerGoalDto>> UpdateGoalAsync(string ownerId, Guid goalId, CareerGoalRequest request, VersionPrecondition precondition, CancellationToken ct = default);
    Task<CareerOutcome<IReadOnlyList<CareerGoalVersionSummaryDto>>> ListGoalVersionsAsync(string ownerId, Guid goalId, CancellationToken ct = default);
    Task<CareerOutcome<CareerGoalVersionDto>> GetGoalVersionAsync(string ownerId, Guid goalId, int version, CancellationToken ct = default);
    Task<CareerOutcome<CareerGoalDto>> RestoreGoalVersionAsync(string ownerId, Guid goalId, int version, VersionPrecondition precondition, CancellationToken ct = default);
}

/// <summary>
/// Manual career profile and goal storage (ticket #378, ADR 0006). Every accepted
/// write appends an immutable version and moves the aggregate's active pointer in
/// the same SaveChanges; the pointer is a concurrency token so racing writers from
/// the same base version cannot both succeed.
/// </summary>
public sealed class CareerProfileService : ICareerProfileService
{
    public const int MaxVersionsListed = 50;

    private readonly ApplicationDbContext _db;
    private readonly TimeProvider _clock;

    public CareerProfileService(ApplicationDbContext db, TimeProvider clock)
    {
        _db = db;
        _clock = clock;
    }

    public static string ProfileEtag(int version) => $"\"profile-v{version}\"";
    public static string GoalEtag(int version) => $"\"goal-v{version}\"";

    // ---- Profile ---------------------------------------------------------

    public async Task<CareerOutcome<CareerProfileDto>> GetProfileAsync(string ownerId, CancellationToken ct = default)
    {
        var profile = await FindProfileAsync(ownerId, ct);
        if (profile == null)
        {
            return ProfileNotFound<CareerProfileDto>();
        }

        var active = await ActiveProfileVersionAsync(profile, ct);
        return CareerOutcome<CareerProfileDto>.Ok(ToDto(profile, active));
    }

    public async Task<CareerOutcome<CareerProfileDto>> SaveProfileAsync(
        string ownerId, CareerProfileRequest request, VersionPrecondition precondition, CancellationToken ct = default)
    {
        var (facts, errors) = CareerInputValidator.Validate(request);
        if (facts == null)
        {
            return CareerOutcome<CareerProfileDto>.Invalid(errors);
        }

        var profile = await FindProfileAsync(ownerId, ct);
        var now = Now();

        if (profile == null)
        {
            // First creation needs no If-Match (spec #376).
            profile = new CareerProfile
            {
                Id = Guid.NewGuid(),
                OwnerId = ownerId,
                ActiveVersionNumber = 1,
                CreatedAt = now,
                UpdatedAt = now
            };
            var first = NewProfileVersion(profile, 1, facts, now, restoredFrom: null);
            _db.CareerProfiles.Add(profile);
            _db.CareerProfileVersions.Add(first);

            return await CommitProfileAsync(ownerId, profile, first, ct);
        }

        var blocked = CheckPrecondition<CareerProfileDto>(precondition, profile.ActiveVersionNumber);
        if (blocked != null)
        {
            return blocked;
        }

        var next = NewProfileVersion(profile, profile.ActiveVersionNumber + 1, facts, now, restoredFrom: null);
        return await AppendProfileVersionAsync(ownerId, profile, next, ct);
    }

    public async Task<CareerOutcome<CareerProfileDto>> AppendFromProposalAsync(
        string ownerId, ValidProfileFacts facts, string source, Guid proposalId, int? baseVersion,
        Action<int>? beforeCommit = null, CancellationToken ct = default)
    {
        var profile = await FindProfileAsync(ownerId, ct);
        var now = Now();

        if (profile == null)
        {
            if (baseVersion != null)
            {
                return CareerOutcome<CareerProfileDto>.Conflict(0);
            }

            profile = new CareerProfile
            {
                Id = Guid.NewGuid(),
                OwnerId = ownerId,
                ActiveVersionNumber = 1,
                CreatedAt = now,
                UpdatedAt = now
            };
            var first = NewProfileVersion(profile, 1, facts, now, restoredFrom: null);
            first.Source = source;
            first.SourceProposalId = proposalId;
            _db.CareerProfiles.Add(profile);
            _db.CareerProfileVersions.Add(first);
            beforeCommit?.Invoke(1);
            return await CommitProfileAsync(ownerId, profile, first, ct);
        }

        if (baseVersion != profile.ActiveVersionNumber)
        {
            return CareerOutcome<CareerProfileDto>.Conflict(profile.ActiveVersionNumber);
        }

        var next = NewProfileVersion(profile, profile.ActiveVersionNumber + 1, facts, now, restoredFrom: null);
        next.Source = source;
        next.SourceProposalId = proposalId;
        beforeCommit?.Invoke(next.VersionNumber);
        return await AppendProfileVersionAsync(ownerId, profile, next, ct);
    }

    public async Task<CareerOutcome<IReadOnlyList<CareerProfileVersionSummaryDto>>> ListProfileVersionsAsync(
        string ownerId, CancellationToken ct = default)
    {
        var profile = await FindProfileAsync(ownerId, ct);
        if (profile == null)
        {
            // Nothing saved yet is an empty history, not an error.
            return CareerOutcome<IReadOnlyList<CareerProfileVersionSummaryDto>>.Ok(Array.Empty<CareerProfileVersionSummaryDto>());
        }

        var versions = await _db.CareerProfileVersions.AsNoTracking()
            .Where(v => v.OwnerId == ownerId && v.CareerProfileId == profile.Id)
            .OrderByDescending(v => v.VersionNumber)
            .Take(MaxVersionsListed)
            .Select(v => new CareerProfileVersionSummaryDto(
                v.VersionNumber, v.CreatedAt, v.Source, v.CurrentTitle, v.VersionNumber == profile.ActiveVersionNumber))
            .ToListAsync(ct);

        return CareerOutcome<IReadOnlyList<CareerProfileVersionSummaryDto>>.Ok(versions);
    }

    public async Task<CareerOutcome<CareerProfileVersionDto>> GetProfileVersionAsync(
        string ownerId, int version, CancellationToken ct = default)
    {
        var profile = await FindProfileAsync(ownerId, ct);
        var row = profile == null ? null : await FindProfileVersionAsync(ownerId, profile.Id, version, ct);
        if (profile == null || row == null)
        {
            return VersionNotFound<CareerProfileVersionDto>();
        }

        return CareerOutcome<CareerProfileVersionDto>.Ok(new CareerProfileVersionDto(
            row.VersionNumber, ToFacts(row), ToProvenance(row.Source, row.ConfirmedAt, row.RestoredFromVersion, row.SourceProposalId), row.CreatedAt,
            row.VersionNumber == profile.ActiveVersionNumber));
    }

    public async Task<CareerOutcome<CareerProfileDto>> RestoreProfileVersionAsync(
        string ownerId, int version, VersionPrecondition precondition, CancellationToken ct = default)
    {
        var profile = await FindProfileAsync(ownerId, ct);
        if (profile == null)
        {
            return ProfileNotFound<CareerProfileDto>();
        }

        var source = await FindProfileVersionAsync(ownerId, profile.Id, version, ct);
        if (source == null)
        {
            return VersionNotFound<CareerProfileDto>();
        }

        var blocked = CheckPrecondition<CareerProfileDto>(precondition, profile.ActiveVersionNumber);
        if (blocked != null)
        {
            return blocked;
        }

        // History is never rewritten: restoring appends a copy as the newest version.
        var facts = new ValidProfileFacts(
            source.CurrentTitle, source.Industry, source.YearsExperience, source.Location, source.Summary,
            source.Skills.ToList(), source.Highlights.ToList(), source.WorkArrangement);
        // Restoring is an explicit acceptance, so the new version is confirmed now.
        var next = NewProfileVersion(profile, profile.ActiveVersionNumber + 1, facts, Now(), restoredFrom: source.VersionNumber);
        return await AppendProfileVersionAsync(ownerId, profile, next, ct);
    }

    private async Task<CareerOutcome<CareerProfileDto>> AppendProfileVersionAsync(
        string ownerId, CareerProfile profile, CareerProfileVersion next, CancellationToken ct)
    {
        profile.ActiveVersionNumber = next.VersionNumber;
        profile.UpdatedAt = next.CreatedAt;
        _db.CareerProfileVersions.Add(next);
        return await CommitProfileAsync(ownerId, profile, next, ct);
    }

    private async Task<CareerOutcome<CareerProfileDto>> CommitProfileAsync(
        string ownerId, CareerProfile profile, CareerProfileVersion active, CancellationToken ct)
    {
        try
        {
            await _db.SaveChangesAsync(ct);
            return CareerOutcome<CareerProfileDto>.Ok(ToDto(profile, active));
        }
        catch (DbUpdateException ex) when (IsLostRace(ex))
        {
            // Lost a race: another write moved the active version (concurrency
            // token) or created the profile first (unique owner index).
            _db.ChangeTracker.Clear();
            var current = await FindProfileAsync(ownerId, ct);
            return CareerOutcome<CareerProfileDto>.Conflict(current?.ActiveVersionNumber ?? 0);
        }
    }

    // ---- Goal ------------------------------------------------------------

    public async Task<CareerOutcome<CareerGoalDto>> GetGoalAsync(string ownerId, CancellationToken ct = default)
    {
        var goal = await _db.CareerGoals.FirstOrDefaultAsync(g => g.OwnerId == ownerId, ct);
        if (goal == null)
        {
            return GoalNotFound();
        }

        var active = await ActiveGoalVersionAsync(goal, ct);
        return CareerOutcome<CareerGoalDto>.Ok(ToDto(goal, active, await ProfileVersionNumberAsync(ownerId, ct)));
    }

    public async Task<CareerOutcome<CareerGoalDto>> CreateGoalAsync(string ownerId, CareerGoalRequest request, CancellationToken ct = default)
    {
        var (facts, errors) = CareerInputValidator.Validate(request);
        if (facts == null)
        {
            return CareerOutcome<CareerGoalDto>.Invalid(errors);
        }

        if (await _db.CareerGoals.AnyAsync(g => g.OwnerId == ownerId, ct))
        {
            return GoalAlreadyExists();
        }

        var now = Now();
        var profileVersion = await ProfileVersionNumberAsync(ownerId, ct);
        var goal = new CareerGoal
        {
            Id = Guid.NewGuid(),
            OwnerId = ownerId,
            ActiveVersionNumber = 1,
            CreatedAt = now,
            UpdatedAt = now
        };
        var first = NewGoalVersion(goal, 1, facts, profileVersion, now);
        _db.CareerGoals.Add(goal);
        _db.CareerGoalVersions.Add(first);

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (IsLostRace(ex))
        {
            // Another request created this owner's goal first (unique owner index).
            _db.ChangeTracker.Clear();
            return GoalAlreadyExists();
        }

        return CareerOutcome<CareerGoalDto>.Created(ToDto(goal, first, profileVersion));
    }

    public async Task<CareerOutcome<CareerGoalDto>> UpdateGoalAsync(
        string ownerId, Guid goalId, CareerGoalRequest request, VersionPrecondition precondition, CancellationToken ct = default)
    {
        // Ownership is checked before validation so another owner's goal ID
        // reveals nothing, not even whether the input would have been valid.
        var goal = await _db.CareerGoals.FirstOrDefaultAsync(g => g.Id == goalId && g.OwnerId == ownerId, ct);
        if (goal == null)
        {
            return GoalNotFound();
        }

        var (facts, errors) = CareerInputValidator.Validate(request);
        if (facts == null)
        {
            return CareerOutcome<CareerGoalDto>.Invalid(errors);
        }

        var blocked = CheckPrecondition<CareerGoalDto>(precondition, goal.ActiveVersionNumber);
        if (blocked != null)
        {
            return blocked;
        }

        var profileVersion = await ProfileVersionNumberAsync(ownerId, ct);
        var next = NewGoalVersion(goal, goal.ActiveVersionNumber + 1, facts, profileVersion, Now());
        return await AppendGoalVersionAsync(goal, next, profileVersion, ct);
    }

    public async Task<CareerOutcome<CareerGoalVersionDto>> GetGoalVersionAsync(
        string ownerId, Guid goalId, int version, CancellationToken ct = default)
    {
        var goal = await _db.CareerGoals.AsNoTracking().FirstOrDefaultAsync(g => g.Id == goalId && g.OwnerId == ownerId, ct);
        var row = goal == null ? null : await FindGoalVersionAsync(ownerId, goal.Id, version, ct);
        if (goal == null || row == null)
        {
            return CareerOutcome<CareerGoalVersionDto>.NotFound(CareerErrorCodes.VersionNotFound, "That goal version was not found.");
        }

        return CareerOutcome<CareerGoalVersionDto>.Ok(new CareerGoalVersionDto(
            row.VersionNumber, ToGoalFacts(row), row.BasedOnProfileVersion,
            ToProvenance(row.Source, row.ConfirmedAt, row.RestoredFromVersion), row.CreatedAt,
            row.VersionNumber == goal.ActiveVersionNumber));
    }

    public async Task<CareerOutcome<CareerGoalDto>> RestoreGoalVersionAsync(
        string ownerId, Guid goalId, int version, VersionPrecondition precondition, CancellationToken ct = default)
    {
        var goal = await _db.CareerGoals.FirstOrDefaultAsync(g => g.Id == goalId && g.OwnerId == ownerId, ct);
        if (goal == null)
        {
            return GoalNotFound();
        }

        var source = await FindGoalVersionAsync(ownerId, goal.Id, version, ct);
        if (source == null)
        {
            return CareerOutcome<CareerGoalDto>.NotFound(CareerErrorCodes.VersionNotFound, "That goal version was not found.");
        }

        var blocked = CheckPrecondition<CareerGoalDto>(precondition, goal.ActiveVersionNumber);
        if (blocked != null)
        {
            return blocked;
        }

        // Restoring re-confirms the old goal against today's profile, so it is not stale.
        var facts = new ValidGoalFacts(source.TargetRole, source.TargetLocation, source.WorkArrangement,
            source.DesiredPayMin, source.DesiredPayMax, source.WeeklyEffortHours);
        var profileVersion = await ProfileVersionNumberAsync(ownerId, ct);
        var next = NewGoalVersion(goal, goal.ActiveVersionNumber + 1, facts, profileVersion, Now());
        next.RestoredFromVersion = source.VersionNumber;
        return await AppendGoalVersionAsync(goal, next, profileVersion, ct);
    }

    private async Task<CareerOutcome<CareerGoalDto>> AppendGoalVersionAsync(
        CareerGoal goal, CareerGoalVersion next, int? profileVersion, CancellationToken ct)
    {
        goal.ActiveVersionNumber = next.VersionNumber;
        goal.UpdatedAt = next.CreatedAt;
        _db.CareerGoalVersions.Add(next);

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (IsLostRace(ex))
        {
            _db.ChangeTracker.Clear();
            var current = await _db.CareerGoals.AsNoTracking()
                .FirstOrDefaultAsync(g => g.Id == goal.Id && g.OwnerId == goal.OwnerId, ct);
            return CareerOutcome<CareerGoalDto>.Conflict(current?.ActiveVersionNumber ?? 0);
        }

        return CareerOutcome<CareerGoalDto>.Ok(ToDto(goal, next, profileVersion));
    }

    public async Task<CareerOutcome<IReadOnlyList<CareerGoalVersionSummaryDto>>> ListGoalVersionsAsync(
        string ownerId, Guid goalId, CancellationToken ct = default)
    {
        var goal = await _db.CareerGoals.AsNoTracking().FirstOrDefaultAsync(g => g.Id == goalId && g.OwnerId == ownerId, ct);
        if (goal == null)
        {
            return CareerOutcome<IReadOnlyList<CareerGoalVersionSummaryDto>>.NotFound(
                CareerErrorCodes.GoalNotFound, "No career goal was found.");
        }

        var versions = await _db.CareerGoalVersions.AsNoTracking()
            .Where(v => v.OwnerId == ownerId && v.CareerGoalId == goal.Id)
            .OrderByDescending(v => v.VersionNumber)
            .Take(MaxVersionsListed)
            .Select(v => new CareerGoalVersionSummaryDto(v.VersionNumber, v.CreatedAt, v.TargetRole, v.VersionNumber == goal.ActiveVersionNumber))
            .ToListAsync(ct);

        return CareerOutcome<IReadOnlyList<CareerGoalVersionSummaryDto>>.Ok(versions);
    }

    // ---- Helpers ---------------------------------------------------------

    private DateTime Now() => _clock.GetUtcNow().UtcDateTime;

    /// <summary>
    /// Only a concurrency-token mismatch or a unique-index violation means another
    /// write won; any other database failure propagates as a real error.
    /// </summary>
    internal static bool IsLostRace(DbUpdateException exception) =>
        exception is DbUpdateConcurrencyException
        || exception.InnerException is SqlException { Number: 2601 or 2627 };

    private static CareerOutcome<T>? CheckPrecondition<T>(VersionPrecondition precondition, int activeVersion)
    {
        if (!precondition.IsPresent)
        {
            return CareerOutcome<T>.PreconditionRequired();
        }
        return precondition.ExpectedVersion == activeVersion ? null : CareerOutcome<T>.Conflict(activeVersion);
    }

    private Task<CareerProfile?> FindProfileAsync(string ownerId, CancellationToken ct) =>
        _db.CareerProfiles.FirstOrDefaultAsync(p => p.OwnerId == ownerId, ct);

    private Task<CareerProfileVersion?> FindProfileVersionAsync(string ownerId, Guid profileId, int version, CancellationToken ct) =>
        _db.CareerProfileVersions.AsNoTracking()
            .FirstOrDefaultAsync(v => v.OwnerId == ownerId && v.CareerProfileId == profileId && v.VersionNumber == version, ct);

    private Task<CareerGoalVersion?> FindGoalVersionAsync(string ownerId, Guid goalId, int version, CancellationToken ct) =>
        _db.CareerGoalVersions.AsNoTracking()
            .FirstOrDefaultAsync(v => v.OwnerId == ownerId && v.CareerGoalId == goalId && v.VersionNumber == version, ct);

    private async Task<CareerProfileVersion> ActiveProfileVersionAsync(CareerProfile profile, CancellationToken ct) =>
        await FindProfileVersionAsync(profile.OwnerId, profile.Id, profile.ActiveVersionNumber, ct)
        ?? throw new InvalidOperationException($"Career profile {profile.Id} has no active version {profile.ActiveVersionNumber}.");

    private async Task<CareerGoalVersion> ActiveGoalVersionAsync(CareerGoal goal, CancellationToken ct) =>
        await _db.CareerGoalVersions.AsNoTracking()
            .FirstOrDefaultAsync(v => v.OwnerId == goal.OwnerId && v.CareerGoalId == goal.Id && v.VersionNumber == goal.ActiveVersionNumber, ct)
        ?? throw new InvalidOperationException($"Career goal {goal.Id} has no active version {goal.ActiveVersionNumber}.");

    /// <summary>The owner's active profile version, or null when no profile exists.</summary>
    private Task<int?> ProfileVersionNumberAsync(string ownerId, CancellationToken ct) =>
        _db.CareerProfiles.AsNoTracking()
            .Where(p => p.OwnerId == ownerId)
            .Select(p => (int?)p.ActiveVersionNumber)
            .FirstOrDefaultAsync(ct);

    private static CareerProfileVersion NewProfileVersion(CareerProfile profile, int number, ValidProfileFacts facts, DateTime now, int? restoredFrom) => new()
    {
        Id = Guid.NewGuid(),
        CareerProfileId = profile.Id,
        OwnerId = profile.OwnerId,
        VersionNumber = number,
        CurrentTitle = facts.CurrentTitle,
        Industry = facts.Industry,
        YearsExperience = facts.YearsExperience,
        Location = facts.Location,
        Summary = facts.Summary,
        Skills = facts.Skills,
        Highlights = facts.Highlights,
        WorkArrangement = facts.WorkArrangement,
        Source = CareerFactSource.Manual,
        ConfirmedAt = now,
        RestoredFromVersion = restoredFrom,
        CreatedAt = now
    };

    private static CareerGoalVersion NewGoalVersion(CareerGoal goal, int number, ValidGoalFacts facts, int? profileVersion, DateTime now) => new()
    {
        Id = Guid.NewGuid(),
        CareerGoalId = goal.Id,
        OwnerId = goal.OwnerId,
        VersionNumber = number,
        TargetRole = facts.TargetRole,
        TargetLocation = facts.TargetLocation,
        WorkArrangement = facts.WorkArrangement,
        DesiredPayMin = facts.DesiredPayMin,
        DesiredPayMax = facts.DesiredPayMax,
        WeeklyEffortHours = facts.WeeklyEffortHours,
        BasedOnProfileVersion = profileVersion,
        Source = CareerFactSource.Manual,
        ConfirmedAt = now,
        CreatedAt = now
    };

    /// <summary>
    /// A goal is stale when a profile exists that the goal was not confirmed
    /// against: either it moved past the goal's base version or appeared later.
    /// </summary>
    public static bool IsGoalStale(int? basedOnProfileVersion, int? currentProfileVersion) =>
        currentProfileVersion is { } current && (basedOnProfileVersion is not { } basedOn || current > basedOn);

    private static CareerProfileFactsDto ToFacts(CareerProfileVersion v) => new(
        v.CurrentTitle, v.Industry, v.YearsExperience, v.Location, v.Summary, v.Skills, v.Highlights, v.WorkArrangement);

    private static CareerProvenanceDto ToProvenance(string source, DateTime confirmedAt, int? restoredFrom, Guid? proposalId = null) =>
        new(source, DateTime.SpecifyKind(confirmedAt, DateTimeKind.Utc), restoredFrom, proposalId);

    private static CareerGoalFactsDto ToGoalFacts(CareerGoalVersion v) => new(
        v.TargetRole, v.TargetLocation, v.WorkArrangement, v.DesiredPayMin, v.DesiredPayMax, v.WeeklyEffortHours);

    private static CareerProfileDto ToDto(CareerProfile profile, CareerProfileVersion active) => new(
        profile.Id, active.VersionNumber, ProfileEtag(active.VersionNumber), ToFacts(active),
        ToProvenance(active.Source, active.ConfirmedAt, active.RestoredFromVersion, active.SourceProposalId), profile.CreatedAt, profile.UpdatedAt);

    private static CareerGoalDto ToDto(CareerGoal goal, CareerGoalVersion active, int? currentProfileVersion) => new(
        goal.Id, active.VersionNumber, GoalEtag(active.VersionNumber),
        ToGoalFacts(active),
        active.BasedOnProfileVersion,
        IsGoalStale(active.BasedOnProfileVersion, currentProfileVersion),
        ToProvenance(active.Source, active.ConfirmedAt, active.RestoredFromVersion), goal.CreatedAt, goal.UpdatedAt);

    private static CareerOutcome<T> ProfileNotFound<T>() =>
        CareerOutcome<T>.NotFound(CareerErrorCodes.ProfileNotFound, "No career profile has been saved yet.");

    private static CareerOutcome<T> VersionNotFound<T>() =>
        CareerOutcome<T>.NotFound(CareerErrorCodes.VersionNotFound, "That profile version was not found.");

    private static CareerOutcome<CareerGoalDto> GoalNotFound() =>
        CareerOutcome<CareerGoalDto>.NotFound(CareerErrorCodes.GoalNotFound, "No career goal was found.");

    private static CareerOutcome<CareerGoalDto> GoalAlreadyExists() =>
        CareerOutcome<CareerGoalDto>.AlreadyExists(CareerErrorCodes.GoalAlreadyExists, "A career goal already exists. Edit it instead.");
}
