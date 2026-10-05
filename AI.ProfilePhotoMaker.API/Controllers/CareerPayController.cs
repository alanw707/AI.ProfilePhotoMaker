using AI.ProfilePhotoMaker.API.Services.Career;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AI.ProfilePhotoMaker.API.Controllers;

[Authorize]
[ApiController]
[Route("api/career")]
[ServiceFilter(typeof(RequireCareerWorkspaceFilter))]
public sealed class CareerPayController : CareerControllerBase
{
    private readonly ICareerPayService _pay;
    public CareerPayController(ICareerPayService pay, ILogger<CareerPayController> logger) : base(logger) => _pay = pay;

    [HttpGet("pay-analyses")]
    public async Task<IActionResult> List(CancellationToken ct) => await Respond(owner => _pay.ListAsync(owner, ct));

    [HttpGet("pay-analyses/{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct) => await Respond(owner => _pay.GetAsync(owner, id, ct));

    [HttpPost("pay-analyses/{id:guid}/recompute")]
    public async Task<IActionResult> Recompute(Guid id, CancellationToken ct) => await Respond(owner => _pay.RecomputeAsync(owner, id, ct));

    [HttpGet("pay/qualification")]
    public async Task<IActionResult> Qualification() => await Respond(owner => Task.FromResult(CareerOutcome<PayQualificationDto>.Ok(_pay.Qualification())));
}
