using AI.ProfilePhotoMaker.API.Services.Career;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AI.ProfilePhotoMaker.API.Controllers;

[Authorize]
[ApiController]
[Route("api/career/journey")]
[ServiceFilter(typeof(RequireCareerWorkspaceFilter))]
public sealed class CareerJourneyController : CareerControllerBase
{
    private readonly ICareerJourneyService _journey;

    public CareerJourneyController(ICareerJourneyService journey, ILogger<CareerJourneyController> logger) : base(logger)
    {
        _journey = journey;
    }

    /// <summary>Read-only; deliberately takes no query or body input.</summary>
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct) => await Respond(owner => _journey.GetAsync(owner, ct));
}
