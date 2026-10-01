using AI.ProfilePhotoMaker.API.Services.Career;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;

namespace AI.ProfilePhotoMaker.API.Controllers;

/// <summary>
/// Manual career profile and goal (ticket #378). Contract:
/// docs/career/api-profile-goal.md. Owner identity always comes from the
/// authenticated user; another owner's resources answer 404.
/// </summary>
[Authorize]
[ApiController]
[Route("api/career")]
[ServiceFilter(typeof(RequireCareerWorkspaceFilter))]
public sealed class CareerProfileController : BaseController
{
    private readonly ICareerProfileService _career;

    public CareerProfileController(ICareerProfileService career, ILogger<CareerProfileController> logger)
        : base(logger)
    {
        _career = career;
    }

    [HttpGet("profile")]
    public async Task<IActionResult> GetProfile(CancellationToken ct) =>
        await Respond(owner => _career.GetProfileAsync(owner, ct), p => p.Etag);

    [HttpPut("profile")]
    public async Task<IActionResult> SaveProfile([FromBody] CareerProfileRequest? request, CancellationToken ct) =>
        await Respond(owner => _career.SaveProfileAsync(owner, request ?? new CareerProfileRequest(), ReadIfMatch("profile"), ct), p => p.Etag);

    [HttpGet("profile/versions")]
    public async Task<IActionResult> ListProfileVersions(CancellationToken ct) =>
        await Respond(owner => _career.ListProfileVersionsAsync(owner, ct));

    [HttpGet("profile/versions/{version:int}")]
    public async Task<IActionResult> GetProfileVersion(int version, CancellationToken ct) =>
        await Respond(owner => _career.GetProfileVersionAsync(owner, version, ct));

    [HttpPost("profile/versions/{version:int}/restore")]
    public async Task<IActionResult> RestoreProfileVersion(int version, CancellationToken ct) =>
        await Respond(owner => _career.RestoreProfileVersionAsync(owner, version, ReadIfMatch("profile"), ct), p => p.Etag);

    [HttpGet("goals")]
    public async Task<IActionResult> GetGoal(CancellationToken ct) =>
        await Respond(owner => _career.GetGoalAsync(owner, ct), g => g.Etag);

    [HttpPost("goals")]
    public async Task<IActionResult> CreateGoal([FromBody] CareerGoalRequest? request, CancellationToken ct) =>
        await Respond(owner => _career.CreateGoalAsync(owner, request ?? new CareerGoalRequest(), ct), g => g.Etag);

    [HttpPatch("goals/{id:guid}")]
    public async Task<IActionResult> UpdateGoal(Guid id, [FromBody] CareerGoalRequest? request, CancellationToken ct) =>
        await Respond(owner => _career.UpdateGoalAsync(owner, id, request ?? new CareerGoalRequest(), ReadIfMatch("goal"), ct), g => g.Etag);

    [HttpGet("goals/{id:guid}/versions")]
    public async Task<IActionResult> ListGoalVersions(Guid id, CancellationToken ct) =>
        await Respond(owner => _career.ListGoalVersionsAsync(owner, id, ct));

    [HttpGet("goals/{id:guid}/versions/{version:int}")]
    public async Task<IActionResult> GetGoalVersion(Guid id, int version, CancellationToken ct) =>
        await Respond(owner => _career.GetGoalVersionAsync(owner, id, version, ct));

    [HttpPost("goals/{id:guid}/versions/{version:int}/restore")]
    public async Task<IActionResult> RestoreGoalVersion(Guid id, int version, CancellationToken ct) =>
        await Respond(owner => _career.RestoreGoalVersionAsync(owner, id, version, ReadIfMatch("goal"), ct), g => g.Etag);

    /// <summary>
    /// Parses If-Match as <c>"{kind}-v{n}"</c>. Any other tag (including <c>*</c>)
    /// and weak tags (<c>W/"…"</c>) can never match, so they produce a 412 rather
    /// than an unconditional write.
    /// </summary>
    private VersionPrecondition ReadIfMatch(string kind)
    {
        var tags = Request.GetTypedHeaders().IfMatch;
        if (tags == null || tags.Count == 0)
        {
            return VersionPrecondition.Absent;
        }

        var prefix = $"{kind}-v";
        var tag = tags[0].Tag.ToString().Trim('"');
        if (tags.Count == 1 && !tags[0].IsWeak && tag.StartsWith(prefix, StringComparison.Ordinal)
            && int.TryParse(tag.AsSpan(prefix.Length), out var version))
        {
            return VersionPrecondition.Expect(version);
        }
        return VersionPrecondition.Mismatch;
    }

    private async Task<IActionResult> Respond<T>(Func<string, Task<CareerOutcome<T>>> operation, Func<T, string>? etag = null)
    {
        var owner = GetCurrentUserId();
        if (string.IsNullOrEmpty(owner))
        {
            return ValidateAuthentication()!;
        }

        var outcome = await operation(owner);
        if (outcome.Kind is CareerOutcomeKind.Ok or CareerOutcomeKind.Created)
        {
            if (etag != null)
            {
                Response.Headers[HeaderNames.ETag] = etag(outcome.Value!);
            }
            var envelope = new { success = true, data = outcome.Value, message = (string?)null, error = (object?)null };
            return StatusCode(outcome.Kind == CareerOutcomeKind.Created ? StatusCodes.Status201Created : StatusCodes.Status200OK, envelope);
        }

        var status = outcome.Kind switch
        {
            CareerOutcomeKind.NotFound => StatusCodes.Status404NotFound,
            CareerOutcomeKind.Invalid => StatusCodes.Status400BadRequest,
            CareerOutcomeKind.PreconditionRequired => StatusCodes.Status428PreconditionRequired,
            CareerOutcomeKind.VersionConflict => StatusCodes.Status412PreconditionFailed,
            CareerOutcomeKind.AlreadyExists => StatusCodes.Status409Conflict,
            _ => StatusCodes.Status500InternalServerError
        };

        return StatusCode(status, new
        {
            success = false,
            error = new
            {
                code = outcome.ErrorCode,
                message = outcome.Message,
                fieldErrors = outcome.FieldErrors,
                currentVersion = outcome.CurrentVersion,
                correlationId = HttpContext.TraceIdentifier
            }
        });
    }
}
