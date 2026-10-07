using AI.ProfilePhotoMaker.API.Services.Career;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AI.ProfilePhotoMaker.API.Controllers;

/// <summary>
/// Occupation matches (ticket #381). Contract: docs/career/api-occupation-matches.md; design:
/// ADR 0010. Another owner's match ids answer 404.
/// </summary>
[Authorize]
[ApiController]
[Route("api/career")]
[ServiceFilter(typeof(RequireCareerWorkspaceFilter))]
public sealed class CareerOccupationController : CareerControllerBase
{
    private readonly ICareerOccupationService _occupations;

    public CareerOccupationController(ICareerOccupationService occupations, ILogger<CareerOccupationController> logger) : base(logger)
    {
        _occupations = occupations;
    }

    [HttpGet("occupation-matches/{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct) =>
        await Respond(owner => _occupations.GetAsync(owner, id, ct));

    [HttpPost("occupation-matches/{id:guid}/confirm")]
    public async Task<IActionResult> Confirm(Guid id, [FromBody] ConfirmOccupationRequest? request, CancellationToken ct) =>
        await Respond(
            owner => _occupations.ConfirmAsync(owner, id, request ?? new ConfirmOccupationRequest(), ReadIfMatch("goal"), ct),
            g => g.Etag);

    [HttpPost("occupation-matches/{id:guid}/dismiss")]
    public async Task<IActionResult> Dismiss(Guid id, CancellationToken ct) =>
        await Respond(owner => _occupations.DismissAsync(owner, id, ct));

    [HttpGet("occupations/reference")]
    public async Task<IActionResult> Reference() =>
        await Respond(owner => Task.FromResult(_occupations.GetReference()));
}
