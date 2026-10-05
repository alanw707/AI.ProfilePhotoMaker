using AI.ProfilePhotoMaker.API.Services.Career;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AI.ProfilePhotoMaker.API.Controllers;

/// <summary>
/// Optional photo handoff (ticket #391). Contract: docs/career/api-photo-handoff.md.
/// Reads the owner's photos and remembers a choice; never spends credits or allowances.
/// </summary>
[Authorize]
[ApiController]
[Route("api/career/photos")]
[ServiceFilter(typeof(RequireCareerWorkspaceFilter))]
public sealed class CareerPhotoController : CareerControllerBase
{
    private readonly ICareerPhotoService _photos;

    public CareerPhotoController(ICareerPhotoService photos, ILogger<CareerPhotoController> logger)
        : base(logger)
    {
        _photos = photos;
    }

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct) =>
        await Respond(owner => _photos.ListAsync(owner, ct));

    [HttpPut("selection")]
    public async Task<IActionResult> Select([FromBody] CareerPhotoSelectionRequest? request, CancellationToken ct) =>
        await Respond(owner => _photos.SelectAsync(owner, request?.ProcessedImageId, ct));

    [HttpDelete("selection")]
    public async Task<IActionResult> Clear(CancellationToken ct) =>
        await Respond(owner => _photos.ClearAsync(owner, ct), success: _ => NoContent());
}
