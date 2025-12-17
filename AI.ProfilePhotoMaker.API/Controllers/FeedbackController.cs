using AI.ProfilePhotoMaker.API.Data;
using AI.ProfilePhotoMaker.API.Models;
using AI.ProfilePhotoMaker.API.Models.DTOs;
using AI.ProfilePhotoMaker.API.Services.Notifications;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AI.ProfilePhotoMaker.API.Controllers;

[Authorize]
public class FeedbackController : BaseController
{
    private readonly ApplicationDbContext _context;
    private readonly IEmailNotificationService _emailNotificationService;

    public FeedbackController(
        ILogger<FeedbackController> logger,
        ApplicationDbContext context,
        IEmailNotificationService emailNotificationService)
        : base(logger, context)
    {
        _context = context;
        _emailNotificationService = emailNotificationService;
    }

    [HttpPost]
    public async Task<IActionResult> SubmitFeedback([FromBody] SubmitFeedbackRequestDto request)
    {
        var authError = ValidateAuthentication();
        if (authError != null)
        {
            return authError;
        }

        if (!ModelState.IsValid)
        {
            return ValidationError("Invalid input.");
        }

        var userId = GetCurrentUserId()!;

        // Normalize and clamp fields to match DTO + DB constraints.
        var category = (request.Category ?? "General").Trim();
        if (string.IsNullOrWhiteSpace(category))
        {
            category = "General";
        }
        if (category.Length > 50)
        {
            category = category[..50];
        }

        var message = (request.Message ?? string.Empty).Trim();
        if (message.Length < 10)
        {
            return ValidationError("Message must be at least 10 characters.");
        }
        if (message.Length > 2000)
        {
            message = message[..2000];
        }

        var submission = new FeedbackSubmission
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Category = category,
            Message = message,
            PageUrl = NormalizeOptional(request.PageUrl, 2048),
            UserAgent = NormalizeOptional(request.UserAgent, 512),
            CreatedAtUtc = DateTime.UtcNow,
        };

        _context.FeedbackSubmissions.Add(submission);
        await _context.SaveChangesAsync();

        // Do not log message content (could contain sensitive info).
        LogInfo("Feedback submission received", userId);

        var userEmail = await _context.Users
            .Where(u => u.Id == userId)
            .Select(u => u.Email)
            .FirstOrDefaultAsync();

        await _emailNotificationService.SendSupportFeedbackReceivedAsync(userId, userEmail, submission);

        return SuccessResponse(new { id = submission.Id }, "Thanks — your message was sent to our team.");
    }

    // Internal/admin convenience endpoint could be added later; keep MVP surface area minimal.

    private static string? NormalizeOptional(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        if (trimmed.Length <= maxLength)
        {
            return trimmed;
        }

        return trimmed[..maxLength];
    }
}
