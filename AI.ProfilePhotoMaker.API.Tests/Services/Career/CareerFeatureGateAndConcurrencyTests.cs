using AI.ProfilePhotoMaker.API.Data;
using AI.ProfilePhotoMaker.API.Models.Career;
using AI.ProfilePhotoMaker.API.Services.Career;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace AI.ProfilePhotoMaker.API.Tests.Services.Career;

public class CareerFeatureGateTests
{
    private static ICareerFeatureGate Gate(string? value)
    {
        var settings = new Dictionary<string, string?>();
        if (value != null)
        {
            settings[CareerFeatureGate.ConfigKey] = value;
        }
        return new CareerFeatureGate(new ConfigurationBuilder().AddInMemoryCollection(settings).Build());
    }

    [Fact]
    public void IsOffWhenTheSettingIsMissing() => Gate(null).IsEnabled.Should().BeFalse();

    [Theory]
    [InlineData("false", false)]
    [InlineData("true", true)]
    [InlineData("True", true)]
    public void FollowsAnExplicitSetting(string value, bool expected) => Gate(value).IsEnabled.Should().Be(expected);

    [Fact]
    public void StaleRuleTreatsALaterOrNewProfileAsStale()
    {
        CareerProfileService.IsGoalStale(basedOnProfileVersion: null, currentProfileVersion: null).Should().BeFalse();
        CareerProfileService.IsGoalStale(basedOnProfileVersion: 2, currentProfileVersion: 2).Should().BeFalse();
        CareerProfileService.IsGoalStale(basedOnProfileVersion: 2, currentProfileVersion: 3).Should().BeTrue();
        CareerProfileService.IsGoalStale(basedOnProfileVersion: null, currentProfileVersion: 1).Should().BeTrue();
    }
}

/// <summary>
/// The active-version pointer is the optimistic concurrency token: two writers that
/// read the same version cannot both move it. (In-memory EF enforces concurrency
/// tokens; SQL uniqueness/isolation is proved separately against SQL Server.)
/// </summary>
public class CareerConcurrencyTokenTests
{
    private static ApplicationDbContext NewContext(string name) =>
        new(new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(name).Options);

    [Fact]
    public async Task SecondWriterFromTheSameVersionLoses()
    {
        var name = $"career-concurrency-{Guid.NewGuid():N}";
        var id = Guid.NewGuid();
        await using (var seed = NewContext(name))
        {
            seed.CareerProfiles.Add(new CareerProfile { Id = id, OwnerId = "owner", ActiveVersionNumber = 1 });
            await seed.SaveChangesAsync();
        }

        await using var first = NewContext(name);
        await using var second = NewContext(name);
        var a = await first.CareerProfiles.SingleAsync(p => p.Id == id);
        var b = await second.CareerProfiles.SingleAsync(p => p.Id == id);

        a.ActiveVersionNumber = 2;
        await first.SaveChangesAsync();

        b.ActiveVersionNumber = 2;
        var act = () => second.SaveChangesAsync();
        await act.Should().ThrowAsync<DbUpdateConcurrencyException>();
    }

    [Fact]
    public async Task ServiceReportsAConflictWhenItLosesTheRace()
    {
        var name = $"career-race-{Guid.NewGuid():N}";
        await using (var seed = NewContext(name))
        {
            var service = new CareerProfileService(seed, TimeProvider.System);
            (await service.SaveProfileAsync("owner", Request("v1"), VersionPrecondition.Absent)).Kind.Should().Be(CareerOutcomeKind.Ok);
        }

        // A context whose tracked profile is moved on by another writer between
        // its read and its save, exactly as a concurrent request would.
        await using var racing = new InterleavingContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(name).Options, name);
        var racingService = new CareerProfileService(racing, TimeProvider.System);

        var outcome = await racingService.SaveProfileAsync("owner", Request("mine"), VersionPrecondition.Expect(1));

        outcome.Kind.Should().Be(CareerOutcomeKind.VersionConflict);
        outcome.CurrentVersion.Should().Be(2);
    }

    private static CareerProfileRequest Request(string title) => new()
    {
        CurrentTitle = title,
        Skills = new List<string?>(),
        Highlights = new List<string?>(),
        Confirmed = true
    };

    /// <summary>Lets another writer commit version 2 just before this context saves.</summary>
    private sealed class InterleavingContext : ApplicationDbContext
    {
        private readonly string _name;
        private bool _interleaved;

        public InterleavingContext(DbContextOptions<ApplicationDbContext> options, string name) : base(options)
        {
            _name = name;
        }

        public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            if (!_interleaved)
            {
                _interleaved = true;
                await using var other = NewContext(_name);
                var service = new CareerProfileService(other, TimeProvider.System);
                await service.SaveProfileAsync("owner", Request("theirs"), VersionPrecondition.Expect(1), cancellationToken);
            }
            return await base.SaveChangesAsync(cancellationToken);
        }
    }
}
