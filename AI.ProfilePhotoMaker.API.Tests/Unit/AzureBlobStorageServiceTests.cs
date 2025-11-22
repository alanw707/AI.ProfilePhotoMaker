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
    private AzureBlobStorageService CreateService()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "AzureStorage:ConnectionString", "UseDevelopmentStorage=true" },
                { "AzureStorage:ContainerName", "profile-images" },
            })
            .Build();

        var logger = new Mock<ILogger<AzureBlobStorageService>>();
        var blobClient = new BlobServiceClient("UseDevelopmentStorage=true");

        return new AzureBlobStorageService(blobClient, config, logger.Object);
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
