using AI.ProfilePhotoMaker.API.Services.Career;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AI.ProfilePhotoMaker.API.Controllers;

public sealed record UpdateCareerControlsRequest(bool? GenerationDisabled, bool? SourcesDisabled);

/// <summary>Operator usage report and kill switches (ticket #395, ADR 0022). Admin role only.</summary>
/// <remarks>Deliberately NOT gated by Features:CareerWorkspace: operators need past usage and the kill switches
/// while the feature is off (e.g. to pause generation before turning it back on).</remarks>
[Authorize(Roles = "Admin")]
[ApiController]
[Route("api/admin/career")]
public sealed class AdminCareerController : BaseController
{
    private readonly ICareerUsageReportService _report;
    private readonly ICareerOperatorControls _controls;

    public AdminCareerController(ICareerUsageReportService report, ICareerOperatorControls controls, ILogger<AdminCareerController> logger) : base(logger)
    {
        _report = report;
        _controls = controls;
    }

    [HttpGet("usage")]
    public async Task<IActionResult> Usage([FromQuery] DateTime? from, [FromQuery] DateTime? to, CancellationToken ct)
    {
        var end = (to ?? DateTime.UtcNow).ToUniversalTime();
        var start = (from ?? end.AddDays(-30)).ToUniversalTime();
        if (start >= end)
        {
            return BadRequest(new { success = false, error = new { code = "Validation", message = "from must be before to." } });
        }
        return Ok(new { success = true, data = await _report.BuildAsync(start, end, ct) });
    }

    [HttpGet("controls")]
    public async Task<IActionResult> GetControls(CancellationToken ct) =>
        Ok(new { success = true, data = await _controls.GetAsync(ct) });

    [HttpPut("controls")]
    public async Task<IActionResult> PutControls([FromBody] UpdateCareerControlsRequest? request, CancellationToken ct)
    {
        if (request is null || (request.GenerationDisabled is null && request.SourcesDisabled is null))
        {
            return BadRequest(new { success = false, error = new { code = "Validation", message = "Send generationDisabled and/or sourcesDisabled." } });
        }
        var updated = await _controls.UpdateAsync(request.GenerationDisabled, request.SourcesDisabled, GetCurrentUserId(), ct);
        return Ok(new { success = true, data = updated });
    }
}
