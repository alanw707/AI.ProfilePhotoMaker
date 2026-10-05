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

    public CareerMarketController(ICareerMarketService market, ILogger<CareerMarketController> logger) : base(logger)
    {
        _market = market;
    }

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
