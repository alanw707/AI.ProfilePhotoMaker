using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;

namespace AI.ProfilePhotoMaker.API.Hubs;

/// <summary>
/// SignalR hub for real-time prediction status updates
/// Eliminates need for frontend polling by providing instant completion notifications
/// </summary>
[Authorize]
public class PredictionHub : Hub
{
    private readonly ILogger<PredictionHub> _logger;

    public PredictionHub(ILogger<PredictionHub> logger)
    {
        _logger = logger;
    }

    public override async Task OnConnectedAsync()
    {
        var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!string.IsNullOrEmpty(userId))
        {
            // Join user-specific group for targeted notifications
            await Groups.AddToGroupAsync(Context.ConnectionId, $"user_{userId}");
            _logger.LogDebug("User {UserId} connected to prediction hub", userId);
        }
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!string.IsNullOrEmpty(userId))
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"user_{userId}");
            _logger.LogDebug("User {UserId} disconnected from prediction hub", userId);
        }
        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>
    /// Client can subscribe to specific prediction updates
    /// </summary>
    public async Task SubscribeToPrediction(string predictionId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"prediction_{predictionId}");
        _logger.LogDebug("Client subscribed to prediction {PredictionId}", predictionId);
    }
}