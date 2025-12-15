using AI.ProfilePhotoMaker.API.Services.Payments;
using Xunit;

namespace AI.ProfilePhotoMaker.API.Tests.Unit;

public class StripeClientFactoryTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\r\n")]
    public void GetSafeSecretKey_ReturnsPlaceholder_WhenMissing(string? secretKey)
    {
        var value = StripeClientFactory.GetSafeSecretKey(secretKey);
        Assert.False(string.IsNullOrWhiteSpace(value));
    }

    [Fact]
    public void Create_DoesNotThrow_WhenMissingKey()
    {
        var client = StripeClientFactory.Create("");
        Assert.NotNull(client);
    }
}
