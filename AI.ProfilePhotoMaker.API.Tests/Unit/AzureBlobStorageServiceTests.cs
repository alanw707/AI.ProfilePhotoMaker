using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using AI.ProfilePhotoMaker.API.Services.Storage;
using AI.ProfilePhotoMaker.API.Services;
using Azure.Storage.Blobs;

namespace AI.ProfilePhotoMaker.API.Tests.Unit;

public class AzureBlobStorageServiceTests
{
    private AzureBlobStorageService CreateService(Dictionary<string, string?>? overrides = null)
    {
        var settings = new Dictionary<string, string?>
        {
            { "AzureStorage:ConnectionString", "UseDevelopmentStorage=true" },
            { "AzureStorage:ContainerName", "profile-images" },
        };

        if (overrides != null)
        {
            foreach (var (key, value) in overrides)
            {
                settings[key] = value;
            }
        }

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(settings)
            .Build();

        var logger = new Mock<ILogger<AzureBlobStorageService>>();
        var blobClient = new BlobServiceClient("UseDevelopmentStorage=true");

        return new AzureBlobStorageService(blobClient, config, logger.Object);
    }

    [Fact]
    public void GetImageUrl_WhenProxyEnabled_UsesExternalApiBaseUrlForApiHostedImageProxy()
    {
        var svc = CreateService(new Dictionary<string, string?>
        {
            ["Storage:ProxyBlobRequests"] = "true",
            ["AppBaseUrl"] = "https://aiprofilephotomaker.com",
            ["ExternalApiBaseUrl"] = "https://api.aiprofilephotomaker.com"
        });

        var url = svc.GetImageUrl("generated/user-1/headshot.png");

        Assert.Equal("https://api.aiprofilephotomaker.com/profile-images/generated/user-1/headshot.png", url);
    }

    [Theory]
    [InlineData("http://127.0.0.1:10000/devstoreaccount1/profile-images/dev/training-zips/user.zip", "profile-images", "dev/training-zips/user.zip")]
    [InlineData("https://clear-anteater-usually.ngrok-free.app/devstoreaccount1/profile-images/dev/training-zips/user.zip", "profile-images", "dev/training-zips/user.zip")]
    [InlineData("profile-images/dev/training-zips/user.zip", "profile-images", "dev/training-zips/user.zip")]
    [InlineData("style-previews/foo/bar.png", "style-previews", "foo/bar.png")]
    [InlineData("relative/path/file.txt", "profile-images", "relative/path/file.txt")]
    public void ResolveContainerAndBlob_MapsCorrectly(string input, string expectedContainer, string expectedBlob)
    {
        var svc = CreateService();

        var (container, blobPath) = svc.ResolveContainerAndBlob(input);

        Assert.Equal(expectedContainer, container);
        Assert.Equal(expectedBlob, blobPath);
    }
}
