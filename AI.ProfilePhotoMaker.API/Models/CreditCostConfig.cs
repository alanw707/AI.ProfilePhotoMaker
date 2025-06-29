namespace AI.ProfilePhotoMaker.API.Models;

public static class CreditCostConfig
{
    public const int PhotoEnhancement = 1;
    public const int ModelTraining = 15;
    public const int StyledGeneration = 5;
    
    public static int GetCreditCost(string operation)
    {
        return operation.ToLowerInvariant() switch
        {
            "photo_enhancement" => PhotoEnhancement,
            "model_training" => ModelTraining,
            "styled_generation" or "image_generation" => StyledGeneration,
            _ => 1 // Default cost
        };
    }
    
    public static bool CanUseWeeklyCredits(string operation)
    {
        return operation.ToLowerInvariant() switch
        {
            "photo_enhancement" => true,
            "model_training" => false,
            "styled_generation" => false,
            "image_generation" => false,
            _ => false
        };
    }
}