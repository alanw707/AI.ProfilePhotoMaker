using AI.ProfilePhotoMaker.API.Middleware;
using Xunit;

namespace AI.ProfilePhotoMaker.API.Tests.Middleware;

public class EnhancedStorageProxyMiddlewareTests
{
    [Theory]
    [InlineData("/profile-images/generated-private/user/raw.png")]
    [InlineData("/profile-images/dev/generated-private/user/raw.png")]
    [InlineData("/devstoreaccount1/profile-images/dev/generated-private/user/raw.png")]
    [InlineData("/PROFILE-IMAGES/DEV/GENERATED-PRIVATE/user/raw.png")]
    public void IsPrivateStoragePath_BlocksPrivateFolderInEveryProxyShape(string path)
    {
        Assert.True(EnhancedStorageProxyMiddleware.IsPrivateStoragePath(path));
    }

    [Theory]
    [InlineData("/profile-images/dev/generated/user/image.png")]
    [InlineData("/devstoreaccount1/profile-images/dev/enhanced/user/image.png")]
    [InlineData("/generated-private-preview/user/image.png")]
    public void IsPrivateStoragePath_AllowsNonPrivateFolders(string path)
    {
        Assert.False(EnhancedStorageProxyMiddleware.IsPrivateStoragePath(path));
    }
}
