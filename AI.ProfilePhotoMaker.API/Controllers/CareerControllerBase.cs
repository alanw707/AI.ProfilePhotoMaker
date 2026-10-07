using AI.ProfilePhotoMaker.API.Services.Career;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;

namespace AI.ProfilePhotoMaker.API.Controllers;

/// <summary>
/// Shared plumbing for career controllers: owner identity, If-Match parsing and
/// translating <see cref="CareerOutcome{T}"/> into the spec #376 status codes and
/// response envelope.
/// </summary>
[ServiceFilter(typeof(CareerReplayGuardFilter))]
public abstract class CareerControllerBase : BaseController
{
    protected CareerControllerBase(ILogger logger) : base(logger)
    {
    }

    /// <summary>
    /// Parses If-Match as <c>"{kind}-v{n}"</c>. Any other tag (including <c>*</c>)
    /// and weak tags (<c>W/"…"</c>) can never match, so they produce a 412 rather
    /// than an unconditional write.
    /// </summary>
    protected VersionPrecondition ReadIfMatch(string kind)
    {
        var tags = Request.GetTypedHeaders().IfMatch;
        if (tags == null || tags.Count == 0)
        {
            return VersionPrecondition.Absent;
        }

        var prefix = $"{kind}-v";
        var tag = tags[0].Tag.ToString().Trim('"');
        if (tags.Count == 1 && !tags[0].IsWeak && tag.StartsWith(prefix, StringComparison.Ordinal)
            && int.TryParse(tag.AsSpan(prefix.Length), out var version))
        {
            return VersionPrecondition.Expect(version);
        }
        return VersionPrecondition.Mismatch;
    }

    protected async Task<IActionResult> Respond<T>(
        Func<string, Task<CareerOutcome<T>>> operation,
        Func<T, string>? etag = null,
        Func<T, IActionResult?>? success = null)
    {
        var owner = GetCurrentUserId();
        if (string.IsNullOrEmpty(owner))
        {
            return ValidateAuthentication()!;
        }

        var outcome = await operation(owner);
        if (outcome.Kind is CareerOutcomeKind.Ok or CareerOutcomeKind.Created)
        {
            if (success?.Invoke(outcome.Value!) is { } custom)
            {
                return custom;
            }
            if (etag != null)
            {
                Response.Headers[HeaderNames.ETag] = etag(outcome.Value!);
            }
            var envelope = new { success = true, data = outcome.Value, message = (string?)null, error = (object?)null };
            return StatusCode(outcome.Kind == CareerOutcomeKind.Created ? StatusCodes.Status201Created : StatusCodes.Status200OK, envelope);
        }

        var status = outcome.Kind switch
        {
            CareerOutcomeKind.NotFound => StatusCodes.Status404NotFound,
            CareerOutcomeKind.Invalid => StatusCodes.Status400BadRequest,
            CareerOutcomeKind.PreconditionRequired => StatusCodes.Status428PreconditionRequired,
            CareerOutcomeKind.VersionConflict => StatusCodes.Status412PreconditionFailed,
            CareerOutcomeKind.AlreadyExists => StatusCodes.Status409Conflict,
            CareerOutcomeKind.TooLarge => StatusCodes.Status413PayloadTooLarge,
            CareerOutcomeKind.Unsupported => StatusCodes.Status415UnsupportedMediaType,
            CareerOutcomeKind.Rejected => StatusCodes.Status422UnprocessableEntity,
            CareerOutcomeKind.Unavailable => StatusCodes.Status503ServiceUnavailable,
            CareerOutcomeKind.QuotaExceeded => StatusCodes.Status429TooManyRequests,
            CareerOutcomeKind.Gone => StatusCodes.Status410Gone,
            _ => StatusCodes.Status500InternalServerError
        };

        if (outcome.RetryAfterSeconds is { } retryAfter)
        {
            Response.Headers[HeaderNames.RetryAfter] = retryAfter.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        return StatusCode(status, new
        {
            success = false,
            error = new
            {
                code = outcome.ErrorCode,
                message = outcome.Message,
                detail = outcome.Detail,
                fieldErrors = outcome.FieldErrors,
                currentVersion = outcome.CurrentVersion,
                retryAfterSeconds = outcome.RetryAfterSeconds,
                correlationId = HttpContext.TraceIdentifier
            }
        });
    }
}
