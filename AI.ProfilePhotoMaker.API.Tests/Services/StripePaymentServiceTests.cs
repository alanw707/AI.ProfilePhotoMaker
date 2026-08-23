using AI.ProfilePhotoMaker.API.Configuration;
using AI.ProfilePhotoMaker.API.Data;
using AI.ProfilePhotoMaker.API.Models;
using AI.ProfilePhotoMaker.API.Services;
using AI.ProfilePhotoMaker.API.Services.Payments;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Stripe;
using Xunit;

namespace AI.ProfilePhotoMaker.API.Tests.Services;

public class StripePaymentServiceTests
{
    [Fact]
    public async Task CreatePaymentIntent_RejectsUnavailablePreviewBeforeContactingStripe()
    {
        await using var context = CreateContext();
        var user = new ApplicationUser
        {
            Id = "checkout-user",
            UserName = "checkout@example.com",
            Email = "checkout@example.com"
        };
        var package = new CreditPackage
        {
            Name = "Starter",
            Credits = 10,
            Price = 9.99m,
            Description = "Starter package",
            IsActive = true
        };
        context.Users.Add(user);
        context.CreditPackages.Add(package);
        await context.SaveChangesAsync();
        var outcomePackages = new Mock<IOutcomePackageService>();
        outcomePackages
            .Setup(service => service.ReservePreviewForPurchaseAsync(user.Id, 123, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        var environment = new Mock<IWebHostEnvironment>();
        environment.SetupGet(value => value.EnvironmentName).Returns("Production");
        var service = new StripePaymentService(
            context,
            NullLogger<StripePaymentService>.Instance,
            Options.Create(new StripeOptions
            {
                PublishableKey = "pk_test_default",
                SecretKey = "sk_test_default",
                WebhookSecret = "whsec_default"
            }),
            new StripeClient("sk_test_default"),
            environment.Object,
            Mock.Of<ICouponService>(),
            outcomePackages.Object);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreatePaymentIntentAsync(user.Id, package.Id, null, 123));

        Assert.Contains("expired", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(context.PaymentTransactions);
        outcomePackages.Verify(service => service.ReservePreviewForPurchaseAsync(
            user.Id,
            123,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(options);
    }
}
