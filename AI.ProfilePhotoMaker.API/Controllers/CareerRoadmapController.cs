using AI.ProfilePhotoMaker.API.Services.Career;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AI.ProfilePhotoMaker.API.Controllers;

[Authorize]
[ApiController]
[Route("api/career/roadmaps")]
[ServiceFilter(typeof(RequireCareerWorkspaceFilter))]
public sealed class CareerRoadmapController : CareerControllerBase
{
    private readonly ICareerRoadmapService _roadmaps;
    private readonly ICareerRoadmapTrackingService _tracking;

    public CareerRoadmapController(ICareerRoadmapService roadmaps, ICareerRoadmapTrackingService tracking, ILogger<CareerRoadmapController> logger) : base(logger)
    {
        _roadmaps = roadmaps;
        _tracking = tracking;
    }

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct) => await Respond(owner => _roadmaps.ListAsync(owner, ct));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct) => await Respond(owner => _roadmaps.GetAsync(owner, id, ct));

    [HttpPost("{id:guid}/accept")]
    public async Task<IActionResult> Accept(Guid id, [FromBody] AcceptRoadmapRequest request, CancellationToken ct)
    {
        var precondition = ReadIfMatch("goal");
        return await Respond(owner => _roadmaps.AcceptAsync(owner, id, request, precondition, ct));
    }

    [HttpPost("{id:guid}/dismiss")]
    public async Task<IActionResult> Dismiss(Guid id, CancellationToken ct) => await Respond(owner => _roadmaps.DismissAsync(owner, id, ct));

    [HttpPut("{id:guid}/tasks/{taskId}")]
    public async Task<IActionResult> UpdateTask(Guid id, string taskId, [FromBody] UpdateRoadmapTaskRequest request, CancellationToken ct) =>
        await Respond(owner => _roadmaps.UpdateTaskAsync(owner, id, taskId, request, ct));

    // ---- Tracking and replans (#388, ADR 0017) ----

    [HttpGet("{id:guid}/progress")]
    public async Task<IActionResult> Progress(Guid id, CancellationToken ct) => await Respond(owner => _tracking.GetProgressAsync(owner, id, ct));

    [HttpPut("{id:guid}/progress/{taskId}")]
    public async Task<IActionResult> UpdateProgress(Guid id, string taskId, [FromBody] UpdateTaskProgressRequest? request, CancellationToken ct)
    {
        var precondition = ReadIfMatch("task");
        return await Respond(owner => _tracking.UpdateProgressAsync(owner, id, taskId, request ?? new UpdateTaskProgressRequest(), precondition, ct), t => t.Etag);
    }

    [HttpPost("{id:guid}/tasks")]
    public async Task<IActionResult> AddTask(Guid id, [FromBody] AddHumanTaskRequest? request, CancellationToken ct) =>
        await Respond(owner => _tracking.AddHumanTaskAsync(owner, id, request ?? new AddHumanTaskRequest(), ct), t => t.Etag);

    [HttpPost("{id:guid}/replan")]
    public async Task<IActionResult> Replan(Guid id, CancellationToken ct) => await Respond(owner => _tracking.ReplanAsync(owner, id, ct));

    [HttpGet("~/api/career/replans/{replanId:guid}")]
    public async Task<IActionResult> GetReplan(Guid replanId, CancellationToken ct) => await Respond(owner => _tracking.GetReplanAsync(owner, replanId, ct));

    [HttpPost("~/api/career/replans/{replanId:guid}/apply")]
    public async Task<IActionResult> ApplyReplan(Guid replanId, [FromBody] ApplyReplanRequest? request, CancellationToken ct) =>
        await Respond(owner => _tracking.ApplyReplanAsync(owner, replanId, request ?? new ApplyReplanRequest(), ct));

    [HttpPost("~/api/career/replans/{replanId:guid}/reject")]
    public async Task<IActionResult> RejectReplan(Guid replanId, CancellationToken ct) => await Respond(owner => _tracking.RejectReplanAsync(owner, replanId, ct));
}
