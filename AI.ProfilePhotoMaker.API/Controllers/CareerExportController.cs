using AI.ProfilePhotoMaker.API.Services.Career;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AI.ProfilePhotoMaker.API.Controllers;

/// <summary>
/// PDF and DOCX exports (ticket #390). Contract: docs/career/api-summaries-exports.md; design: ADR 0019.
/// Exports are private and short-lived: never cached, always an attachment, 404 for another owner, 410 once expired.
/// </summary>
[Authorize]
[ApiController]
[Route("api/career")]
[ServiceFilter(typeof(RequireCareerWorkspaceFilter))]
public sealed class CareerExportController : CareerControllerBase
{
    private readonly ICareerExportService _exports;

    public CareerExportController(ICareerExportService exports, ILogger<CareerExportController> logger) : base(logger)
    {
        _exports = exports;
    }

    private void NoStore() => Response.Headers.CacheControl = "no-store, private";

    [HttpPost("materials/{id:guid}/exports")]
    public async Task<IActionResult> Create(Guid id, [FromBody] CreateExportRequest? request, CancellationToken ct)
    {
        NoStore();
        return await Respond(owner => _exports.CreateAsync(owner, id, request ?? new CreateExportRequest(), ct));
    }

    [HttpGet("materials/{id:guid}/exports")]
    public async Task<IActionResult> List(Guid id, CancellationToken ct)
    {
        NoStore();
        return await Respond(owner => _exports.ListAsync(owner, id, ct));
    }

    [HttpGet("exports/{id:guid}")]
    public async Task<IActionResult> Download(Guid id, CancellationToken ct)
    {
        NoStore();
        return await Respond(owner => _exports.DownloadAsync(owner, id, ct),
            success: file => File(file.Content, file.ContentType, file.FileName));
    }
}
