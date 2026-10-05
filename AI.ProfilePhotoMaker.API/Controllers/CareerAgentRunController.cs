using AI.ProfilePhotoMaker.API.Services.Career;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AI.ProfilePhotoMaker.API.Controllers;

/// <summary>
/// Career agent runs (ticket #380). Contract: docs/career/api-agent-runs.md; design:
/// ADR 0009. Another owner's run ids answer 404.
/// </summary>
[Authorize]
[ApiController]
[Route("api/career/runs")]
[ServiceFilter(typeof(RequireCareerWorkspaceFilter))]
public sealed class CareerAgentRunController : CareerControllerBase
{
    private readonly ICareerAgentRunService _runs;

    public CareerAgentRunController(ICareerAgentRunService runs, ILogger<CareerAgentRunController> logger) : base(logger)
    {
        _runs = runs;
    }

    [HttpPost]
    public async Task<IActionResult> Start(
        [FromBody] CreateCareerRunRequest? request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        CancellationToken ct) =>
        await Respond(
            owner => _runs.CreateAsync(owner, request ?? new CreateCareerRunRequest(), idempotencyKey, ct),
            success: Accepted);

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct) =>
        await Respond(owner => _runs.ListAsync(owner, ct));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct) =>
        await Respond(owner => _runs.GetAsync(owner, id, ct));

    [HttpPost("{id:guid}/answers")]
    public async Task<IActionResult> Answer(Guid id, [FromBody] AnswerCareerRunRequest? request, CancellationToken ct) =>
        await Respond(
            owner => _runs.AnswerAsync(owner, id, request ?? new AnswerCareerRunRequest(), ct),
            success: Accepted);

    [HttpPost("{id:guid}/cancel")]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken ct) =>
        await Respond(owner => _runs.CancelAsync(owner, id, ct));

    /// <summary>202 with the run and a Location to poll; used for new runs, replays and answers.</summary>
    private IActionResult Accepted(CareerAgentRunDto run)
    {
        Response.Headers.Location = $"/api/career/runs/{run.Id}";
        return StatusCode(StatusCodes.Status202Accepted, new { success = true, data = run, message = (string?)null, error = (object?)null });
    }
}
