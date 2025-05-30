namespace AI.ProfilePhotoMaker.API.Models;

public class Subscription
{
    public int Id { get; set; }
    public string UserId { get; set; }
    public ApplicationUser User { get; set; }
    public string PlanId { get; set; }
    public SubscriptionPlan Plan { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public bool IsActive { get; set; }
    public string PaymentProvider { get; set; }
    public string ExternalSubscriptionId { get; set; }
}
