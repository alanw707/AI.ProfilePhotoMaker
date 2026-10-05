using AI.ProfilePhotoMaker.API.Services.Career;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AI.ProfilePhotoMaker.API.Controllers;

/// <summary>
/// Resume import and profile proposals (ticket #379). Contract:
/// docs/career/api-resume-import.md. Another owner's ids answer 404.
/// </summary>
[Authorize]
[ApiController]
[Route("api/career")]
[ServiceFilter(typeof(RequireCareerWorkspaceFilter))]
public sealed class CareerResumeController : CareerControllerBase
{
    /// <summary>A little above the 10 MiB file limit so multipart overhead still reaches the service's 413.</summary>
    private const long RequestLimitBytes = 11 * 1024 * 1024;

    private readonly IResumeImportService _resumes;
    private readonly ICareerProposalService _proposals;

    public CareerResumeController(IResumeImportService resumes, ICareerProposalService proposals, ILogger<CareerResumeController> logger)
        : base(logger)
    {
        _resumes = resumes;
        _proposals = proposals;
    }

    [HttpPost("resumes")]
    [RequestSizeLimit(RequestLimitBytes)]
    // Without an explicit list, ASP.NET infers multipart-only and answers 415 at routing,
    // before the feature-flag filter can answer 403 CareerWorkspaceDisabled.
    [Consumes("multipart/form-data", "application/json")]
    public async Task<IActionResult> Upload(
        IFormFile? file, [FromForm] string? consent, [FromForm] string? consentVersion, CancellationToken ct)
    {
        // Read at most one byte past the limit, into one exact-size array: enough for
        // the service to answer 413 without buffering an arbitrarily large body.
        byte[]? bytes = null;
        if (file != null)
        {
            var length = (int)Math.Min(file.Length, ResumeFileInspector.MaxBytes + 1);
            bytes = new byte[length];
            await using var stream = file.OpenReadStream();
            var read = await stream.ReadAtLeastAsync(bytes, length, throwOnEndOfStream: false, ct);
            if (read < length)
            {
                Array.Resize(ref bytes, read);
            }
        }

        var consented = bool.TryParse(consent, out var value) && value;
        return await Respond(owner => _resumes.UploadAsync(owner, bytes, file?.FileName, consented, consentVersion, ct));
    }

    [HttpGet("resumes")]
    public async Task<IActionResult> List(CancellationToken ct) =>
        await Respond(owner => _resumes.ListAsync(owner, ct));

    [HttpGet("resumes/{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct) =>
        await Respond(owner => _resumes.GetAsync(owner, id, ct));

    [HttpGet("resumes/{id:guid}/file")]
    public async Task<IActionResult> GetFile(Guid id, CancellationToken ct) =>
        await Respond(
            owner => _resumes.GetFileAsync(owner, id, ct),
            success: file => File(file.Content, file.ContentType, file.FileName));

    [HttpDelete("resumes/{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct) =>
        await Respond(owner => _resumes.DeleteAsync(owner, id, ct), success: _ => NoContent());

    [HttpPost("profile/proposals")]
    public async Task<IActionResult> Paste([FromBody] PasteProposalRequest? request, CancellationToken ct) =>
        await Respond(owner => _proposals.CreateFromPasteAsync(owner, request ?? new PasteProposalRequest(), ct));

    [HttpGet("profile/proposals/{id:guid}")]
    public async Task<IActionResult> GetProposal(Guid id, CancellationToken ct) =>
        await Respond(owner => _proposals.GetAsync(owner, id, ct));

    [HttpPost("profile/proposals/{id:guid}/accept")]
    public async Task<IActionResult> Accept(Guid id, [FromBody] AcceptProposalRequest? request, CancellationToken ct) =>
        await Respond(
            owner => _proposals.AcceptAsync(owner, id, request ?? new AcceptProposalRequest(), ReadIfMatch("profile"), ct),
            p => p.Etag);

    [HttpPost("profile/proposals/{id:guid}/dismiss")]
    public async Task<IActionResult> Dismiss(Guid id, CancellationToken ct) =>
        await Respond(owner => _proposals.DismissAsync(owner, id, ct));
}
