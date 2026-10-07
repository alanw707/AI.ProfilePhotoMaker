using AI.ProfilePhotoMaker.API.Services.Career;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AI.ProfilePhotoMaker.API.Controllers;

/// <summary>
/// Targeted resumes (ticket #389). Contract: docs/career/api-targeted-resume.md; design: ADR 0018.
/// Another owner's ids answer 404.
/// </summary>
[Authorize]
[ApiController]
[Route("api/career/materials")]
[ServiceFilter(typeof(RequireCareerWorkspaceFilter))]
public sealed class CareerMaterialController : CareerControllerBase
{
    private readonly ICareerMaterialService _materials;

    public CareerMaterialController(ICareerMaterialService materials, ILogger<CareerMaterialController> logger) : base(logger)
    {
        _materials = materials;
    }

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] string? kind, CancellationToken ct) =>
        await Respond(owner => _materials.ListAsync(owner, kind, ct));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct) =>
        await Respond(owner => _materials.GetAsync(owner, id, ct), m => m.Etag);

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Save(Guid id, [FromBody] SaveMaterialRequest? request, CancellationToken ct)
    {
        var precondition = ReadIfMatch("material");
        return await Respond(owner => _materials.SaveAsync(owner, id, request ?? new SaveMaterialRequest(), precondition, ct), m => m.Etag);
    }

    [HttpGet("{id:guid}/versions")]
    public async Task<IActionResult> Versions(Guid id, [FromQuery] int page = 1, CancellationToken ct = default) =>
        await Respond(owner => _materials.ListVersionsAsync(owner, id, page, ct));

    [HttpGet("{id:guid}/versions/{number:int}")]
    public async Task<IActionResult> Version(Guid id, int number, CancellationToken ct) =>
        await Respond(owner => _materials.GetVersionAsync(owner, id, number, ct));

    [HttpPost("{id:guid}/versions/{number:int}/restore")]
    public async Task<IActionResult> Restore(Guid id, int number, CancellationToken ct)
    {
        var precondition = ReadIfMatch("material");
        return await Respond(owner => _materials.RestoreAsync(owner, id, number, precondition, ct), m => m.Etag);
    }

    [HttpGet("{id:guid}/proposals/{proposalId:guid}")]
    public async Task<IActionResult> Proposal(Guid id, Guid proposalId, CancellationToken ct) =>
        await Respond(owner => _materials.GetProposalAsync(owner, id, proposalId, ct));

    [HttpPost("{id:guid}/proposals/{proposalId:guid}/apply")]
    public async Task<IActionResult> Apply(Guid id, Guid proposalId, [FromBody] ApplyMaterialProposalRequest? request, CancellationToken ct)
    {
        var precondition = ReadIfMatch("material");
        return await Respond(owner => _materials.ApplyProposalAsync(owner, id, proposalId, request ?? new ApplyMaterialProposalRequest(), precondition, ct), m => m.Etag);
    }

    [HttpPost("{id:guid}/proposals/{proposalId:guid}/reject")]
    public async Task<IActionResult> Reject(Guid id, Guid proposalId, CancellationToken ct) =>
        await Respond(owner => _materials.RejectProposalAsync(owner, id, proposalId, ct));
}
