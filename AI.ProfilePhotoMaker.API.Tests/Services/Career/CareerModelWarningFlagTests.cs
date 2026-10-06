using AI.ProfilePhotoMaker.API.Services.Career;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace AI.ProfilePhotoMaker.API.Tests.Services.Career;

/// <summary>A partly configured career model only warns when the career feature is on (code review P2).</summary>
public class CareerModelWarningFlagTests
{
    private sealed class Gate(bool on) : ICareerFeatureGate
    {
        public bool IsEnabled => on;
    }

    [Theory]
    [InlineData(false, 0)]
    [InlineData(true, 1)]
    public async Task WarnsOnlyWhileTheFlagIsOn(bool on, int warnings)
    {
        var logger = new Mock<ILogger<CareerTextModelConfigurationWarning>>();
        await new CareerTextModelConfigurationWarning("problem", logger.Object, new Gate(on)).StartAsync(CancellationToken.None);
        logger.Invocations.Count(i => i.Method.Name == "Log" && (LogLevel)i.Arguments[0] == LogLevel.Warning).Should().Be(warnings);
    }
}
