using AI.ProfilePhotoMaker.API.Services.Career;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AI.ProfilePhotoMaker.API.Controllers;

/// <summary>
/// Market briefs (ticket #382). Contract: docs/career/api-market-brief.md; design: ADR 0011.
/// Another owner's brief ids answer 404.
/// </summary>
[Authorize]
[ApiController]
[Route("api/career")]
[ServiceFilter(typeof(RequireCareerWorkspaceFilter))]
public sealed class CareerMarketController : CareerControllerBase
{
    private readonly ICareerMarketService _market;
    private readonly ICareerMarketComparisonService _comparison;
    private readonly IJobObservationService _jobs;

    public CareerMarketController(
        ICareerMarketService market, ICareerMarketComparisonService comparison, IJobObservationService jobs, ILogger<CareerMarketController> logger) : base(logger)
    {
        _market = market;
        _comparison = comparison;
        _jobs = jobs;
    }

    /// <summary>Market comparison (ticket #385). Contract: docs/career/api-market-comparison.md; design: ADR 0014.</summary>
    [HttpGet("markets/metrics")]
    public async Task<IActionResult> Metrics() =>
        await Respond(owner => Task.FromResult(_comparison.GetMetrics()));

    [HttpGet("markets/compare")]
    public async Task<IActionResult> Compare(
        [FromQuery] string? metric, [FromQuery] string? level, [FromQuery] string? areas, [FromQuery] string? q, CancellationToken ct) =>
        await Respond(owner => _comparison.CompareAsync(owner, metric, level, areas, q, ct));

    [HttpPost("markets/preference")]
    public async Task<IActionResult> SavePreference([FromBody] MarketPreferenceRequest? request, CancellationToken ct) =>
        await Respond(owner => _comparison.SavePreferenceAsync(owner, request ?? new MarketPreferenceRequest(), ReadIfMatch("goal"), ct), g => g.Etag);

    /// <summary>Job observations (ticket #386). Contract: docs/career/api-job-observations.md; design: ADR 0015.</summary>
    [HttpGet("jobs/observations")]
    public async Task<IActionResult> JobObservations(
        [FromQuery] string? area, [FromQuery] bool eligibleOnly, [FromQuery] string? remote, [FromQuery] string? q,
        [FromQuery] string? occupation, CancellationToken ct) =>
        await Respond(owner => _jobs.GetObservationsAsync(owner, area, eligibleOnly, remote, q, occupation, ct));

    [HttpGet("jobs/source")]
    public async Task<IActionResult> JobSource() =>
        await Respond(owner => Task.FromResult(_jobs.GetSource()));

    [HttpGet("market-briefs")]
    public async Task<IActionResult> List(CancellationToken ct) =>
        await Respond(owner => _market.ListAsync(owner, ct));

    [HttpGet("market-briefs/{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct) =>
        await Respond(owner => _market.GetAsync(owner, id, ct));

    [HttpGet("market/reference")]
    public async Task<IActionResult> Reference() =>
        await Respond(owner => Task.FromResult(_market.GetReference()));
}
