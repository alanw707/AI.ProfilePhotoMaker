namespace AI.ProfilePhotoMaker.API.Configuration;

public class PaymentSimulationOptions
{
    public const string SectionName = "PaymentSimulation";

    public bool Enabled { get; set; } = false;
    public bool SkipStripeIntegration { get; set; } = false;
}
