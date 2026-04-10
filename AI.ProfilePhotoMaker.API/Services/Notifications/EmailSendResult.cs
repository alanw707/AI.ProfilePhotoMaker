namespace AI.ProfilePhotoMaker.API.Services.Notifications;

public sealed record EmailSendResult(bool Success, string? ProviderMessageId = null);
