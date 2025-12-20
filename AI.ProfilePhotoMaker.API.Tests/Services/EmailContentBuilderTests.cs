using AI.ProfilePhotoMaker.API.Services.Notifications;
using Xunit;

namespace AI.ProfilePhotoMaker.API.Tests.Services;

public class EmailContentBuilderTests
{
    [Fact]
    public void BuildTextContent_StripsHtmlAndPreservesLinks()
    {
        var html = "<p>Hello <strong>World</strong></p><p>Click <a href=\"https://example.com/confirm\">Confirm email</a></p><ul><li>One</li><li>Two</li></ul>";

        var text = EmailContentBuilder.BuildTextContent(html);

        Assert.Contains("Hello World", text);
        Assert.Contains("Confirm email: https://example.com/confirm", text);
        Assert.Contains("- One", text);
        Assert.Contains("- Two", text);
        Assert.DoesNotContain("<", text);
    }
}
