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
    private readonly ICareerFunnelReportService _funnel;

    public AdminCareerController(ICareerUsageReportService report, ICareerOperatorControls controls, ICareerFunnelReportService funnel,
        ILogger<AdminCareerController> logger) : base(logger)
    {
        _report = report;
        _controls = controls;
        _funnel = funnel;
    }

    [HttpGet("usage")]
    public async Task<IActionResult> Usage([FromQuery] DateTime? from, [FromQuery] DateTime? to, CancellationToken ct)
    {
        if (!TryWindow(from, to, out var start, out var end, out var invalid)) return invalid;
        return Ok(new { success = true, data = await _report.BuildAsync(start, end, ct) });
    }

    /// <summary>How far people who started the workspace in [from, to) have got. Counts only.</summary>
    [HttpGet("funnel")]
    public async Task<IActionResult> Funnel([FromQuery] DateTime? from, [FromQuery] DateTime? to, CancellationToken ct)
    {
        if (!TryWindow(from, to, out var start, out var end, out var invalid)) return invalid;
        return Ok(new { success = true, data = await _funnel.BuildAsync(start, end, ct) });
    }

    /// <summary>Defaults to the last 30 days; both ends are read as UTC.</summary>
    private bool TryWindow(DateTime? from, DateTime? to, out DateTime start, out DateTime end, out IActionResult invalid)
    {
        end = (to ?? DateTime.UtcNow).ToUniversalTime();
        start = (from ?? end.AddDays(-30)).ToUniversalTime();
        invalid = BadRequest(new { success = false, error = new { code = "Validation", message = "from must be before to." } });
        return start < end;
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
