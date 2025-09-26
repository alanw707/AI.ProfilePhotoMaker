using AI.ProfilePhotoMaker.API.Infrastructure.Logging;
using Xunit;

namespace AI.ProfilePhotoMaker.API.Tests.Infrastructure.Logging;

public class LoggingSanitizerTests
{
    [Theory]
    [InlineData(null, "[redacted]")]
    [InlineData("", "[redacted]")]
    [InlineData("   ", "[redacted]")]
    public void Sanitize_ReturnsRedacted_ForNullOrWhitespace(string? input, string expected)
    {
        var result = LoggingSanitizer.Sanitize(input);

        Assert.Equal(expected, result);
    }

    [Fact]
    public void Sanitize_RemovesControlCharacters_AndKeepsTab()
    {
        var input = "ABC\nDEF\r\tGHI";

        var result = LoggingSanitizer.Sanitize(input);

        Assert.Equal("ABCDEF\tGHI", result);
        Assert.DoesNotContain('\n', result);
        Assert.DoesNotContain('\r', result);
    }

    [Fact]
    public void Sanitize_Truncates_WhenExceedingMaxLength()
    {
        var input = new string('a', 300);

        var result = LoggingSanitizer.Sanitize(input, maxLength: 50);

        Assert.Equal(50, result.Length);
    }

    [Fact]
    public void SanitizeId_UsesReducedMaxLength()
    {
        var input = new string('b', 200);

        var result = LoggingSanitizer.SanitizeId(input);

        Assert.Equal(128, result.Length);
    }
}
