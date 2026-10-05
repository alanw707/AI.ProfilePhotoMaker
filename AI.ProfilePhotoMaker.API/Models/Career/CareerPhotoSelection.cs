namespace AI.ProfilePhotoMaker.API.Models.Career;

/// <summary>
/// The owned photo a user chose to use with their career goal (ADR 0008). One per
/// owner. <see cref="ProcessedImageId"/> is deliberately not a foreign key: photos
/// expire on their own retention schedule, and reads report the selection as
/// unavailable instead of the delete being blocked by career data.
/// </summary>
public class CareerPhotoSelection
{
    public Guid Id { get; set; }
    public string OwnerId { get; set; } = string.Empty;
    public int ProcessedImageId { get; set; }

    /// <summary>The owner's goal when the photo was chosen; null if they had none.</summary>
    public Guid? CareerGoalId { get; set; }

    public DateTime SelectedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
