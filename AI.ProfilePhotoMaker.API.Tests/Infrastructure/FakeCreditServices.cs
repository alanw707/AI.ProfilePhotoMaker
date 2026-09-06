using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AI.ProfilePhotoMaker.API.Models;
using AI.ProfilePhotoMaker.API.Models.DTOs;
using AI.ProfilePhotoMaker.API.Services;
using AI.ProfilePhotoMaker.API.Services.Payments;
using AI.ProfilePhotoMaker.API.Services.Payments.Models;

namespace AI.ProfilePhotoMaker.API.Tests.Infrastructure;

public sealed class FakeCreditPackageService : ICreditPackageService
{
    public Task<IEnumerable<CreditPackageDto>> GetActiveCreditPackagesAsync()
    {
        return Task.FromResult(Enumerable.Empty<CreditPackageDto>());
    }

    public Task<CreditPurchaseResult> PurchaseCreditPackageAsync(
        string userId,
        int packageId,
        string? paymentTransactionId = null,
        string? webhookOperationKey = null,
        string? webhookOperationToken = null)
    {
        var result = new CreditPurchaseResult(
            false,
            PaymentStatus.Failed,
            null,
            "NotSupported",
            "Credit package purchases are not supported in tests.");
        return Task.FromResult(result);
    }

    public Task<IEnumerable<CreditPurchase>> GetUserPurchaseHistoryAsync(string userId)
    {
        return Task.FromResult(Enumerable.Empty<CreditPurchase>());
    }
}

public sealed class FakeStripePaymentService : IStripePaymentService
{
    public Task<PaymentIntentResponse> CreatePaymentIntentAsync(
        string userId,
        int packageId,
        string? couponCode = null,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new PaymentIntentResponse(
            "pi_test",
            "secret_test",
            "pk_test",
            "txn_test"));
    }
}
