namespace AI.ProfilePhotoMaker.API.Services;

public interface ICouponService
{
    Task<(bool IsValid, string Message, decimal DiscountAmount)> ValidateCouponAsync(string code, string userId, decimal originalPrice);
    Task<bool> RedeemCouponAsync(string code, string userId, decimal originalPrice, decimal discountApplied, int? paymentTransactionId = null);
}
