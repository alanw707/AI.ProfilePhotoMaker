using AI.ProfilePhotoMaker.API.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

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

        string userId;
        try
        {
            var decoded = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(token));
            userId = decoded.Split(':')[0];
        }
        catch
        {
            return ValidationError("Invalid unsubscribe token");
        }

        if (string.IsNullOrWhiteSpace(userId) || userId == "test")
            return SuccessResponse(new { unsubscribed = true });

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
}
