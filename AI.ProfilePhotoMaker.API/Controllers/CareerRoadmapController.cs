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

    public CareerRoadmapController(ICareerRoadmapService roadmaps, ILogger<CareerRoadmapController> logger) : base(logger) => _roadmaps = roadmaps;

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
}
