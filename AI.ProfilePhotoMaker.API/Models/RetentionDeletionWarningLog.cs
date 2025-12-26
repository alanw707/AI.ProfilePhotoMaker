namespace AI.ProfilePhotoMaker.API.Models;

public class RetentionDeletionWarningLog
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public int DaysBeforeDeletion { get; set; }
    public DateTime DeletionDate { get; set; }
    public DateTime SentAtUtc { get; set; }
}
