namespace AI.ProfilePhotoMaker.API.Models;

/// <summary>
/// Tracks which users have already been sent an abandoned-upload nudge email,
/// so we don't send duplicates across service restarts.
/// </summary>
public class AbandonedUploadNudgeLog
{
    public int Id { get; set; }

    /// <summary>The ASP.NET Identity user ID.</summary>
    public string UserId { get; set; } = string.Empty;

    /// <summary>The abandoned-upload state this nudge covered.</summary>
    public string NudgeType { get; set; } = AbandonedUploadNudgeTypes.NoUploads;

    /// <summary>UTC timestamp when the nudge email was sent.</summary>
    public DateTime SentAtUtc { get; set; }
}
