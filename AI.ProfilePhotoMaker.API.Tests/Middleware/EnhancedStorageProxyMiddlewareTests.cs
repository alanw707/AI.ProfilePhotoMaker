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
    [InlineData("/profile-images/career-private/resumes/0f8fad5b")]
    [InlineData("/devstoreaccount1/profile-images/dev/career-private/resumes/0f8fad5b")]
    [InlineData("/PROFILE-IMAGES/CAREER-PRIVATE/resumes/x")]
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
