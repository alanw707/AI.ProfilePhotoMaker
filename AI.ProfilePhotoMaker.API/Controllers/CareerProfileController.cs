using AI.ProfilePhotoMaker.API.Services.Career;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

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
public sealed class CareerProfileController : CareerControllerBase
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
}
