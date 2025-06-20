using AI.ProfilePhotoMaker.API.Models;
using AI.ProfilePhotoMaker.API.Models.DTOs;

namespace AI.ProfilePhotoMaker.API.Services.Payment;

public interface IPaymentService
{
    /// <summary>
    /// Creates a new subscription for a user
    /// </summary>
    Task<UserSubscriptionDto> CreateSubscriptionAsync(string userId, CreateSubscriptionRequestDto request);
    
    /// <summary>
    /// Updates an existing subscription (upgrade/downgrade)
    /// </summary>
    Task<UserSubscriptionDto> UpdateSubscriptionAsync(string userId, UpdateSubscriptionRequestDto request);
    
    /// <summary>
    /// Cancels a user's subscription
    /// </summary>
    Task<UserSubscriptionDto> CancelSubscriptionAsync(string userId, CancelSubscriptionRequestDto request);
    
    /// <summary>
    /// Gets the user's current subscription
    /// </summary>
    Task<UserSubscriptionDto?> GetUserSubscriptionAsync(string userId);
    
    /// <summary>
    /// Gets all available subscription plans
    /// </summary>
    Task<IEnumerable<SubscriptionPlanDto>> GetSubscriptionPlansAsync();
    
    /// <summary>
    /// Gets user's subscription usage for the current billing period
    /// </summary>
    Task<SubscriptionUsageDto> GetSubscriptionUsageAsync(string userId);
    
    /// <summary>
    /// Creates a customer portal session for subscription management
    /// </summary>
    Task<string> CreateCustomerPortalSessionAsync(string userId, string returnUrl);
    
    /// <summary>
    /// Handles webhook events from Stripe
    /// </summary>
    Task HandleWebhookEventAsync(string payload, string signature);
    
    /// <summary>
    /// Checks if user can perform an action based on their subscription limits
    /// </summary>
    Task<bool> CanUserPerformActionAsync(string userId, string action);
    
    /// <summary>
    /// Records usage for billing purposes
    /// </summary>
    Task RecordUsageAsync(string userId, string action, int quantity = 1);
}