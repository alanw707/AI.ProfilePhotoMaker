namespace AI.ProfilePhotoMaker.API.Models;

// Models/SubscriptionPlan.cs
public class SubscriptionPlan
{
    public string Id { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }
    public decimal Price { get; set; }
    public string BillingPeriod { get; set; }
    public int ImagesPerMonth { get; set; }
    public List<Subscription> Subscriptions { get; set; }
}
