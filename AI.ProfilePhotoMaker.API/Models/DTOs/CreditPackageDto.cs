namespace AI.ProfilePhotoMaker.API.Models.DTOs;

public class CreditPackageDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Credits { get; set; }
    public int BonusCredits { get; set; }
    public int TotalCredits { get; set; }
    public decimal Price { get; set; }
    public string Description { get; set; } = string.Empty;
    public int DisplayOrder { get; set; }
}

public class PurchaseCreditPackageRequestDto
{
    public int PackageId { get; set; }
    public string? PaymentTransactionId { get; set; }
}

public class UserCreditStatusDto
{
    public int TotalCredits { get; set; }
    public int WeeklyCredits { get; set; }
    public int PurchasedCredits { get; set; }
    public DateTime LastCreditReset { get; set; }
    public DateTime NextResetDate { get; set; }
}