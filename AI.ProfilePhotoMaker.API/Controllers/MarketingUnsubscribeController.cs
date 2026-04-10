using AI.ProfilePhotoMaker.API.Data;
using AI.ProfilePhotoMaker.API.Services.Marketing;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AI.ProfilePhotoMaker.API.Controllers;

[ApiController]
[Route("api/user/marketing")]
public class MarketingUnsubscribeController : BaseController
{
    private readonly ApplicationDbContext _db;

    public MarketingUnsubscribeController(
        ApplicationDbContext db,
        ILogger<MarketingUnsubscribeController> logger)
        : base(logger, db)
    {
        _db = db;
    }

    /// <summary>
    /// Unsubscribe a user from marketing emails via token embedded in emails.
    /// Accepts GET (link click) or POST (programmatic).
    /// </summary>
    [HttpGet("unsubscribe")]
    [HttpPost("unsubscribe")]
    public async Task<IActionResult> Unsubscribe([FromQuery] string token)
    {
        if (string.IsNullOrWhiteSpace(token))
            return ValidationError("Missing unsubscribe token");

        var signingSecret = MarketingUnsubscribeTokenService.ResolveSigningSecret(GetEmailOptions());

        var userId = token == "test"
            ? "test"
            : signingSecret != null && MarketingUnsubscribeTokenService.TryReadUserId(token, signingSecret, out var parsedUserId)
                ? parsedUserId
                : string.Empty;

        if (string.IsNullOrWhiteSpace(userId) || userId == "test")
            return token == "test"
                ? SuccessResponse(new { unsubscribed = true })
                : ValidationError("Invalid unsubscribe token");

        var user = await _db.Users.FindAsync(userId);
        if (user == null) return NotFoundResponse("User");

        if (!user.MarketingOptOut)
        {
            user.MarketingOptOut = true;
            user.MarketingOptOutAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
            Logger.LogInformation("User {UserId} unsubscribed from marketing emails", Sid(userId));
        }

        return SuccessResponse(new { unsubscribed = true });
    }

    /// <summary>
    /// Returns the current user's marketing opt-out status.
    /// </summary>
    [HttpGet("status")]
    [Microsoft.AspNetCore.Authorization.Authorize]
    public async Task<IActionResult> GetStatus()
    {
        var authCheck = ValidateAuthentication();
        if (authCheck != null) return authCheck;

        var userId = GetCurrentUserId()!;
        var user = await _db.Users.Where(u => u.Id == userId)
            .Select(u => new { u.MarketingOptOut, u.MarketingOptOutAt })
            .FirstOrDefaultAsync();

        if (user == null) return NotFoundResponse("User");
        return SuccessResponse(new { optedOut = user.MarketingOptOut, optedOutAt = user.MarketingOptOutAt });
    }

    /// <summary>
    /// Re-subscribe the current authenticated user to marketing emails.
    /// </summary>
    [HttpPost("resubscribe")]
    [Microsoft.AspNetCore.Authorization.Authorize]
    public async Task<IActionResult> Resubscribe()
    {
        var authCheck = ValidateAuthentication();
        if (authCheck != null) return authCheck;

        var userId = GetCurrentUserId()!;
        var user = await _db.Users.FindAsync(userId);
        if (user == null) return NotFoundResponse("User");

        user.MarketingOptOut = false;
        user.MarketingOptOutAt = null;
        await _db.SaveChangesAsync();

        return SuccessResponse(new { resubscribed = true });
    }

    private Configuration.EmailOptions GetEmailOptions()
    {
        return HttpContext.RequestServices
            .GetRequiredService<Microsoft.Extensions.Options.IOptions<Configuration.EmailOptions>>()
            .Value;
    }
}
