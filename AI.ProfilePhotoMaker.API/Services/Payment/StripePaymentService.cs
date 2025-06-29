using AI.ProfilePhotoMaker.API.Data;
using AI.ProfilePhotoMaker.API.Models;
using AI.ProfilePhotoMaker.API.Models.DTOs;
using Microsoft.EntityFrameworkCore;
using Stripe;
using Stripe.Checkout;

namespace AI.ProfilePhotoMaker.API.Services.Payment;

public class StripePaymentService : IPaymentService
{
    private readonly ApplicationDbContext _context;
    private readonly IConfiguration _configuration;
    private readonly ILogger<StripePaymentService> _logger;

    public StripePaymentService(
        ApplicationDbContext context,
        IConfiguration configuration,
        ILogger<StripePaymentService> logger)
    {
        _context = context;
        _configuration = configuration;
        _logger = logger;
        
        // Configure Stripe API key
        StripeConfiguration.ApiKey = _configuration["Stripe:SecretKey"];
    }

    public async Task<UserSubscriptionDto> CreateSubscriptionAsync(string userId, CreateSubscriptionRequestDto request)
    {
        try
        {
            // Get user and subscription plan
            var user = await _context.Users.FindAsync(userId);
            if (user == null)
                throw new ArgumentException("User not found");

            var plan = await _context.SubscriptionPlans.FindAsync(request.PlanId);
            if (plan == null)
                throw new ArgumentException("Subscription plan not found");

            // Check if user already has an active subscription
            var existingSubscription = await _context.Subscriptions
                .Where(s => s.UserId == userId && s.Status == SubscriptionStatus.Active)
                .FirstOrDefaultAsync();

            if (existingSubscription != null)
                throw new InvalidOperationException("User already has an active subscription");

            // Create Stripe customer if not exists
            var customerService = new CustomerService();
            var customers = await customerService.ListAsync(new CustomerListOptions
            {
                Email = user.Email,
                Limit = 1
            });

            Customer customer;
            if (customers.Data.Any())
            {
                customer = customers.Data.First();
            }
            else
            {
                customer = await customerService.CreateAsync(new CustomerCreateOptions
                {
                    Email = user.Email,
                    Name = user.UserName,
                    PaymentMethod = request.PaymentMethodId
                });
            }

            // Attach payment method to customer
            var paymentMethodService = new PaymentMethodService();
            await paymentMethodService.AttachAsync(request.PaymentMethodId, new PaymentMethodAttachOptions
            {
                Customer = customer.Id
            });

            // Create Stripe subscription
            var subscriptionService = new SubscriptionService();
            var stripeSubscription = await subscriptionService.CreateAsync(new SubscriptionCreateOptions
            {
                Customer = customer.Id,
                Items = new List<SubscriptionItemOptions>
                {
                    new SubscriptionItemOptions
                    {
                        Price = plan.StripePriceId
                    }
                },
                DefaultPaymentMethod = request.PaymentMethodId
                // Note: Coupon handling requires different implementation in Stripe.net
            });

            // Create local subscription record
            var subscription = new Models.Subscription
            {
                UserId = userId,
                PlanId = request.PlanId,
                ExternalSubscriptionId = stripeSubscription.Id,
                ExternalCustomerId = customer.Id,
                Status = SubscriptionStatus.Active,
                StartDate = DateTime.UtcNow,
                NextBillingDate = DateTime.UtcNow.AddMonths(1), // TODO: Use actual Stripe CurrentPeriodEnd when API is properly configured
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _context.Subscriptions.Add(subscription);

            // Record payment transaction
            var transaction = new PaymentTransaction
            {
                UserId = userId,
                SubscriptionId = subscription.Id,
                ExternalTransactionId = stripeSubscription.LatestInvoiceId ?? "",
                Amount = plan.Price,
                Currency = "USD",
                Status = PaymentStatus.Completed,
                Type = PaymentType.Subscription,
                CreatedAt = DateTime.UtcNow
            };

            _context.PaymentTransactions.Add(transaction);
            await _context.SaveChangesAsync();

            // Return subscription DTO
            return new UserSubscriptionDto
            {
                Id = subscription.Id,
                UserId = userId,
                Plan = MapToSubscriptionPlanDto(plan),
                StartDate = subscription.StartDate,
                EndDate = subscription.EndDate,
                IsActive = subscription.IsActive,
                Status = subscription.Status,
                NextBillingDate = subscription.NextBillingDate,
                CancelledAt = subscription.CancelledAt,
                CancelAtPeriodEnd = subscription.CancelAtPeriodEnd != null
            };
        }
        catch (StripeException ex)
        {
            _logger.LogError(ex, "Stripe error creating subscription for user {UserId}", userId);
            throw new InvalidOperationException($"Payment processing failed: {ex.Message}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating subscription for user {UserId}", userId);
            throw;
        }
    }

    public async Task<UserSubscriptionDto> UpdateSubscriptionAsync(string userId, UpdateSubscriptionRequestDto request)
    {
        try
        {
            var subscription = await _context.Subscriptions
                .Include(s => s.Plan)
                .Where(s => s.UserId == userId && s.Status == SubscriptionStatus.Active)
                .FirstOrDefaultAsync();

            if (subscription == null)
                throw new ArgumentException("No active subscription found");

            var newPlan = await _context.SubscriptionPlans.FindAsync(request.NewPlanId);
            if (newPlan == null)
                throw new ArgumentException("New subscription plan not found");

            // Update Stripe subscription
            var subscriptionService = new SubscriptionService();
            var stripeSubscription = await subscriptionService.UpdateAsync(subscription.ExternalSubscriptionId, new SubscriptionUpdateOptions
            {
                Items = new List<SubscriptionItemOptions>
                {
                    new SubscriptionItemOptions
                    {
                        Price = newPlan.StripePriceId
                    }
                },
                ProrationBehavior = request.ProrationBehavior ? "create_prorations" : "none"
            });

            // Update local subscription
            subscription.PlanId = request.NewPlanId;
            subscription.NextBillingDate = DateTime.UtcNow.AddMonths(1); // TODO: Use actual Stripe CurrentPeriodEnd when API is properly configured
            subscription.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return new UserSubscriptionDto
            {
                Id = subscription.Id,
                UserId = userId,
                Plan = MapToSubscriptionPlanDto(newPlan),
                StartDate = subscription.StartDate,
                EndDate = subscription.EndDate,
                IsActive = subscription.IsActive,
                Status = subscription.Status,
                NextBillingDate = subscription.NextBillingDate,
                CancelledAt = subscription.CancelledAt,
                CancelAtPeriodEnd = subscription.CancelAtPeriodEnd != null
            };
        }
        catch (StripeException ex)
        {
            _logger.LogError(ex, "Stripe error updating subscription for user {UserId}", userId);
            throw new InvalidOperationException($"Payment processing failed: {ex.Message}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating subscription for user {UserId}", userId);
            throw;
        }
    }

    public async Task<UserSubscriptionDto> CancelSubscriptionAsync(string userId, CancelSubscriptionRequestDto request)
    {
        try
        {
            var subscription = await _context.Subscriptions
                .Include(s => s.Plan)
                .Where(s => s.UserId == userId && s.Status == SubscriptionStatus.Active)
                .FirstOrDefaultAsync();

            if (subscription == null)
                throw new ArgumentException("No active subscription found");

            // Cancel Stripe subscription
            var subscriptionService = new SubscriptionService();
            Stripe.Subscription stripeSubscription;

            if (request.CancelAtPeriodEnd)
            {
                stripeSubscription = await subscriptionService.UpdateAsync(subscription.ExternalSubscriptionId, new SubscriptionUpdateOptions
                {
                    CancelAtPeriodEnd = true
                });
                
                subscription.CancelAtPeriodEnd = DateTime.UtcNow;
            }
            else
            {
                stripeSubscription = await subscriptionService.CancelAsync(subscription.ExternalSubscriptionId);
                subscription.Status = SubscriptionStatus.Cancelled;
                subscription.IsActive = false;
                subscription.CancelledAt = DateTime.UtcNow;
                subscription.EndDate = DateTime.UtcNow;
            }

            subscription.CancellationReason = request.Reason;
            subscription.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return new UserSubscriptionDto
            {
                Id = subscription.Id,
                UserId = userId,
                Plan = MapToSubscriptionPlanDto(subscription.Plan!),
                StartDate = subscription.StartDate,
                EndDate = subscription.EndDate,
                IsActive = subscription.IsActive,
                Status = subscription.Status,
                NextBillingDate = subscription.NextBillingDate,
                CancelledAt = subscription.CancelledAt,
                CancelAtPeriodEnd = subscription.CancelAtPeriodEnd != null
            };
        }
        catch (StripeException ex)
        {
            _logger.LogError(ex, "Stripe error cancelling subscription for user {UserId}", userId);
            throw new InvalidOperationException($"Payment processing failed: {ex.Message}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cancelling subscription for user {UserId}", userId);
            throw;
        }
    }

    public async Task<UserSubscriptionDto?> GetUserSubscriptionAsync(string userId)
    {
        var subscription = await _context.Subscriptions
            .Include(s => s.Plan)
            .Where(s => s.UserId == userId)
            .OrderByDescending(s => s.CreatedAt)
            .FirstOrDefaultAsync();

        if (subscription == null)
            return null;

        return new UserSubscriptionDto
        {
            Id = subscription.Id,
            UserId = userId,
            Plan = MapToSubscriptionPlanDto(subscription.Plan!),
            StartDate = subscription.StartDate,
            EndDate = subscription.EndDate,
            IsActive = subscription.IsActive,
            Status = subscription.Status,
            NextBillingDate = subscription.NextBillingDate,
            CancelledAt = subscription.CancelledAt,
            CancelAtPeriodEnd = subscription.CancelAtPeriodEnd != null
        };
    }

    public async Task<IEnumerable<SubscriptionPlanDto>> GetSubscriptionPlansAsync()
    {
        var plans = await _context.SubscriptionPlans
            .Where(p => p.IsActive)
            .OrderBy(p => p.Price)
            .ToListAsync();

        return plans.Select(MapToSubscriptionPlanDto);
    }

    public async Task<SubscriptionUsageDto> GetSubscriptionUsageAsync(string userId)
    {
        var subscription = await GetUserSubscriptionAsync(userId);
        
        if (subscription == null)
        {
            // Return basic tier usage
            var basicPlan = await _context.SubscriptionPlans.FindAsync("basic-plan");
            return new SubscriptionUsageDto
            {
                ImagesGeneratedThisMonth = 0,
                ImagesRemainingThisMonth = basicPlan?.ImagesPerMonth ?? 3,
                TotalImagesAllowed = basicPlan?.ImagesPerMonth ?? 3,
                NextResetDate = DateTime.UtcNow.AddDays(7),
                CanTrainModels = false,
                CanBatchGenerate = false,
                HasHighResolution = false
            };
        }

        // Calculate usage for current billing period
        var startOfMonth = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1);
        var usageCount = await _context.UsageLogs
            .Where(u => u.UserId == userId && u.CreatedAt >= startOfMonth)
            .CountAsync();

        return new SubscriptionUsageDto
        {
            ImagesGeneratedThisMonth = usageCount,
            ImagesRemainingThisMonth = Math.Max(0, subscription.Plan.ImagesPerMonth - usageCount),
            TotalImagesAllowed = subscription.Plan.ImagesPerMonth,
            NextResetDate = subscription.NextBillingDate ?? DateTime.UtcNow.AddMonths(1),
            CanTrainModels = subscription.Plan.CanTrainCustomModels,
            CanBatchGenerate = subscription.Plan.CanBatchGenerate,
            HasHighResolution = subscription.Plan.HighResolutionOutput
        };
    }

    public async Task<string> CreateCustomerPortalSessionAsync(string userId, string returnUrl)
    {
        try
        {
            var subscription = await _context.Subscriptions
                .Where(s => s.UserId == userId && s.Status == SubscriptionStatus.Active)
                .FirstOrDefaultAsync();

            if (subscription == null)
                throw new ArgumentException("No active subscription found");

            var sessionService = new Stripe.BillingPortal.SessionService();
            var session = await sessionService.CreateAsync(new Stripe.BillingPortal.SessionCreateOptions
            {
                Customer = subscription.ExternalCustomerId ?? "",
                ReturnUrl = returnUrl
            });

            return session.Url;
        }
        catch (StripeException ex)
        {
            _logger.LogError(ex, "Stripe error creating customer portal session for user {UserId}", userId);
            throw new InvalidOperationException($"Failed to create customer portal session: {ex.Message}");
        }
    }

    public async Task HandleWebhookEventAsync(string payload, string signature)
    {
        try
        {
            var stripeEvent = EventUtility.ConstructEvent(
                payload,
                signature,
                _configuration["Stripe:WebhookSecret"]
            );

            _logger.LogInformation("Processing Stripe webhook event: {EventType}", stripeEvent.Type);

            switch (stripeEvent.Type)
            {
                case "customer.subscription.updated":
                    await HandleSubscriptionUpdatedAsync((Stripe.Subscription)stripeEvent.Data.Object);
                    break;
                case "customer.subscription.deleted":
                    await HandleSubscriptionDeletedAsync((Stripe.Subscription)stripeEvent.Data.Object);
                    break;
                case "invoice.payment_succeeded":
                    await HandlePaymentSucceededAsync((Invoice)stripeEvent.Data.Object);
                    break;
                case "invoice.payment_failed":
                    await HandlePaymentFailedAsync((Invoice)stripeEvent.Data.Object);
                    break;
                default:
                    _logger.LogInformation("Unhandled webhook event type: {EventType}", stripeEvent.Type);
                    break;
            }
        }
        catch (StripeException ex)
        {
            _logger.LogError(ex, "Error processing Stripe webhook");
            throw;
        }
    }

    public async Task<bool> CanUserPerformActionAsync(string userId, string action)
    {
        var subscription = await GetUserSubscriptionAsync(userId);
        
        if (subscription == null || !subscription.IsActive)
        {
            // Basic tier permissions
            return action switch
            {
                "enhance_photo" => true,
                "generate_basic" => true,
                "train_model" => false,
                "batch_generate" => false,
                "high_resolution" => false,
                _ => false
            };
        }

        return action switch
        {
            "enhance_photo" => true,
            "generate_basic" => true,
            "train_model" => subscription.Plan.CanTrainCustomModels,
            "batch_generate" => subscription.Plan.CanBatchGenerate,
            "high_resolution" => subscription.Plan.HighResolutionOutput,
            _ => false
        };
    }

    public async Task RecordUsageAsync(string userId, string action, int quantity = 1)
    {
        var usageLog = new UsageLog
        {
            UserId = userId,
            Action = action,
            Details = $"Quantity: {quantity}",
            CreatedAt = DateTime.UtcNow
        };

        _context.UsageLogs.Add(usageLog);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Recorded usage: {Action} x{Quantity} for user {UserId}", action, quantity, userId);
    }

    private async Task HandleSubscriptionUpdatedAsync(Stripe.Subscription stripeSubscription)
    {
        var subscription = await _context.Subscriptions
            .Where(s => s.ExternalSubscriptionId == stripeSubscription.Id)
            .FirstOrDefaultAsync();

        if (subscription != null)
        {
            subscription.Status = MapStripeStatus(stripeSubscription.Status);
            subscription.NextBillingDate = DateTime.UtcNow.AddMonths(1); // TODO: Use actual Stripe CurrentPeriodEnd when API is properly configured
            subscription.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
        }
    }

    private async Task HandleSubscriptionDeletedAsync(Stripe.Subscription stripeSubscription)
    {
        var subscription = await _context.Subscriptions
            .Where(s => s.ExternalSubscriptionId == stripeSubscription.Id)
            .FirstOrDefaultAsync();

        if (subscription != null)
        {
            subscription.Status = SubscriptionStatus.Cancelled;
            subscription.IsActive = false;
            subscription.CancelledAt = DateTime.UtcNow;
            subscription.EndDate = DateTime.UtcNow;
            subscription.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
        }
    }

    private async Task HandlePaymentSucceededAsync(Invoice invoice)
    {
        var subscription = await _context.Subscriptions
            .Where(s => s.ExternalSubscriptionId == invoice.Id) // TODO: Use actual invoice.Subscription when API is properly configured
            .FirstOrDefaultAsync();

        if (subscription != null)
        {
            var transaction = new PaymentTransaction
            {
                UserId = subscription.UserId,
                SubscriptionId = subscription.Id,
                ExternalTransactionId = invoice.Id,
                Amount = (decimal)(invoice.Total / 100.0), // Convert from cents
                Currency = invoice.Currency.ToUpper(),
                Status = PaymentStatus.Completed,
                Type = PaymentType.Subscription,
                CreatedAt = DateTime.UtcNow
            };

            _context.PaymentTransactions.Add(transaction);

            subscription.LastPaymentDate = DateTime.UtcNow;
            subscription.NextBillingDate = DateTime.UtcNow.AddMonths(1); // TODO: Use actual invoice.PeriodEnd when API is properly configured
            subscription.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
        }
    }

    private async Task HandlePaymentFailedAsync(Invoice invoice)
    {
        var subscription = await _context.Subscriptions
            .Where(s => s.ExternalSubscriptionId == invoice.Id) // TODO: Use actual invoice.Subscription when API is properly configured
            .FirstOrDefaultAsync();

        if (subscription != null)
        {
            var transaction = new PaymentTransaction
            {
                UserId = subscription.UserId,
                SubscriptionId = subscription.Id,
                ExternalTransactionId = invoice.Id,
                Amount = (decimal)(invoice.Total / 100.0),
                Currency = invoice.Currency.ToUpper(),
                Status = PaymentStatus.Failed,
                Type = PaymentType.Subscription,
                FailureReason = "Payment failed",
                CreatedAt = DateTime.UtcNow
            };

            _context.PaymentTransactions.Add(transaction);
            await _context.SaveChangesAsync();
        }
    }

    private static SubscriptionStatus MapStripeStatus(string stripeStatus)
    {
        return stripeStatus switch
        {
            "active" => SubscriptionStatus.Active,
            "canceled" => SubscriptionStatus.Cancelled,
            "incomplete" => SubscriptionStatus.Incomplete,
            "incomplete_expired" => SubscriptionStatus.IncompleteExpired,
            "past_due" => SubscriptionStatus.PastDue,
            "trialing" => SubscriptionStatus.Trialing,
            "unpaid" => SubscriptionStatus.Unpaid,
            _ => SubscriptionStatus.Cancelled
        };
    }

    private static SubscriptionPlanDto MapToSubscriptionPlanDto(SubscriptionPlan plan)
    {
        return new SubscriptionPlanDto
        {
            Id = plan.Id,
            Name = plan.Name,
            Description = plan.Description,
            Price = plan.Price,
            BillingPeriod = plan.BillingPeriod,
            ImagesPerMonth = plan.ImagesPerMonth,
            CanTrainCustomModels = plan.CanTrainCustomModels,
            CanBatchGenerate = plan.CanBatchGenerate,
            HighResolutionOutput = plan.HighResolutionOutput,
            MaxTrainingImages = plan.MaxTrainingImages,
            MaxStylesAccess = plan.MaxStylesAccess,
            IsActive = plan.IsActive,
            IsRecommended = plan.Id == "premium-monthly" // Mark premium monthly as recommended
        };
    }
}