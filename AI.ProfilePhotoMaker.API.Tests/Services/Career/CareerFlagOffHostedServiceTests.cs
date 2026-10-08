using AI.ProfilePhotoMaker.API.Services.Career;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace AI.ProfilePhotoMaker.API.Tests.Services.Career;

/// <summary>With Features:CareerWorkspace off, the career background loops do no work (code review P1).</summary>
public class CareerFlagOffHostedServiceTests
{
    private sealed class Gate(bool on) : ICareerFeatureGate
    {
        public bool IsEnabled { get; set; } = on;
        public bool IsOpenToEveryone => IsEnabled;
        public bool IsEnabledFor(System.Security.Claims.ClaimsPrincipal user) => IsEnabled;
    }

    private static (Mock<IServiceScopeFactory> Factory, Mock<ICareerAgentRunner> Runner, Mock<ICareerReservationReaper> Reaper) Scopes()
    {
        var runner = new Mock<ICareerAgentRunner>();
        runner.Setup(r => r.RunOnceAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var reaper = new Mock<ICareerReservationReaper>();
        var provider = new ServiceCollection().AddSingleton(runner.Object).AddSingleton(reaper.Object).BuildServiceProvider();
        var factory = new Mock<IServiceScopeFactory>();
        factory.Setup(f => f.CreateScope()).Returns(() => provider.CreateScope());
        return (factory, runner, reaper);
    }

    private static CareerAgentWorker Worker(IServiceScopeFactory scopes, ICareerFeatureGate gate) =>
        new(scopes, Microsoft.Extensions.Options.Options.Create(new CareerAgentOptions()), NullLogger<CareerAgentWorker>.Instance, gate);

    [Fact]
    public async Task WorkerDoesNotTouchTheRunnerWhileTheFlagIsOff()
    {
        var (factory, runner, _) = Scopes();

        (await Worker(factory.Object, new Gate(false)).WorkOnceAsync(CancellationToken.None)).Should().BeFalse();

        factory.Verify(f => f.CreateScope(), Times.Never);
        runner.Verify(r => r.RunOnceAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task WorkerFollowsTheFlagWithoutARestart()
    {
        var (factory, runner, _) = Scopes();
        var gate = new Gate(false);
        var worker = Worker(factory.Object, gate);

        await worker.WorkOnceAsync(CancellationToken.None);
        gate.IsEnabled = true;
        (await worker.WorkOnceAsync(CancellationToken.None)).Should().BeTrue();

        runner.Verify(r => r.RunOnceAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ReaperDoesNothingWhileTheFlagIsOff()
    {
        var (factory, _, reaper) = Scopes();
        var service = new CareerReservationReaperService(
            factory.Object, Microsoft.Extensions.Options.Options.Create(new CareerUsagePolicy()),
            NullLogger<CareerReservationReaperService>.Instance, new Gate(false));

        await service.ReapOnceAsync(CancellationToken.None);

        factory.Verify(f => f.CreateScope(), Times.Never);
        reaper.Verify(r => r.ReapAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ReaperRunsWhileTheFlagIsOn()
    {
        var (factory, _, reaper) = Scopes();
        var service = new CareerReservationReaperService(
            factory.Object, Microsoft.Extensions.Options.Options.Create(new CareerUsagePolicy()),
            NullLogger<CareerReservationReaperService>.Instance, new Gate(true));

        await service.ReapOnceAsync(CancellationToken.None);

        reaper.Verify(r => r.ReapAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
