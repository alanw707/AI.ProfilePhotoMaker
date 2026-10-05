using AI.ProfilePhotoMaker.API.Services.Career;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AI.ProfilePhotoMaker.API.Controllers;

/// <summary>
/// Career privacy: export, scoped deletion and retention (ticket #392). Contract: docs/career/api-privacy.md;
/// design: ADR 0020. Another owner's deletion ids answer 404; creating a deletion needs a recent sign-in.
/// </summary>
[Authorize]
[ApiController]
[Route("api/career/privacy")]
[ServiceFilter(typeof(RequireCareerWorkspaceFilter))]
public sealed class CareerPrivacyController : CareerControllerBase
{
    private readonly ICareerPrivacyService _privacy;
    private readonly ICareerPrivacyExporter _exporter;
    private readonly TimeProvider _clock;

    public CareerPrivacyController(
        ICareerPrivacyService privacy, ICareerPrivacyExporter exporter, TimeProvider clock, ILogger<CareerPrivacyController> logger) : base(logger)
    {
        _privacy = privacy;
        _exporter = exporter;
        _clock = clock;
    }

    private void NoStore() => Response.Headers.CacheControl = "no-store, private";

    [HttpGet("retention")]
    public async Task<IActionResult> Retention() =>
        await Respond(_ => Task.FromResult(CareerOutcome<CareerRetentionDto>.Ok(_privacy.GetRetention())));

    [HttpGet("export")]
    public async Task<IActionResult> Export(CancellationToken ct)
    {
        NoStore();
        var owner = GetCurrentUserId();
        if (string.IsNullOrEmpty(owner))
        {
            return ValidateAuthentication()!;
        }
        var bytes = await _exporter.ExportAsync(owner, ct);
        return File(bytes, "application/json", $"career-export-{_clock.GetUtcNow():yyyy-MM-dd}.json");
    }

    [HttpPost("deletions")]
    public async Task<IActionResult> CreateDeletion([FromBody] CreateCareerDeletionRequest? request, CancellationToken ct)
    {
        NoStore();
        if (GetCurrentUserId() is null)
        {
            return ValidateAuthentication()!;
        }
        if (!CareerRecentAuth.IsRecent(User, _clock.GetUtcNow()))
        {
            return Unauthorized(new
            {
                success = false,
                error = new
                {
                    code = CareerPrivacyErrorCodes.ReauthRequired,
                    message = "Sign in again to delete career data.",
                    correlationId = HttpContext.TraceIdentifier
                }
            });
        }
        return await Respond(owner => _privacy.RequestDeletionAsync(owner, request?.Scope, ct), success: Accepted);
    }

    [HttpGet("deletions/{id:guid}")]
    public async Task<IActionResult> GetDeletion(Guid id, CancellationToken ct)
    {
        NoStore();
        return await Respond(owner => _privacy.GetDeletionAsync(owner, id, ct));
    }

    [HttpPost("deletions/{id:guid}/retry")]
    public async Task<IActionResult> RetryDeletion(Guid id, CancellationToken ct)
    {
        NoStore();
        return await Respond(owner => _privacy.RetryDeletionAsync(owner, id, ct), success: Accepted);
    }

    private IActionResult Accepted(CareerDeletionDto deletion)
    {
        Response.Headers.Location = $"/api/career/privacy/deletions/{deletion.Id}";
        return StatusCode(StatusCodes.Status202Accepted, new { success = true, data = deletion, message = (string?)null, error = (object?)null });
    }
}
