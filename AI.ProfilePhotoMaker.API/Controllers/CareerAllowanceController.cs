using AI.ProfilePhotoMaker.API.Services.Career;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AI.ProfilePhotoMaker.API.Controllers;

/// <summary>The caller's monthly run allowance (ticket #395, ADR 0022). Never gated by kill switches.</summary>
[Authorize]
[ApiController]
[Route("api/career/allowance")]
[ServiceFilter(typeof(RequireCareerWorkspaceFilter))]
public sealed class CareerAllowanceController : CareerControllerBase
{
    private readonly ICareerAgentRunService _runs;

    public CareerAllowanceController(ICareerAgentRunService runs, ILogger<CareerAllowanceController> logger) : base(logger)
    {
        _runs = runs;
    }

    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct) => await Respond(owner => _runs.GetAllowanceAsync(owner, ct));
}
