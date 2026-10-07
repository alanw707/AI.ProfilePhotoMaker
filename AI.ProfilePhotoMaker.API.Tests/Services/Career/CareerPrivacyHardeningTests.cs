using System.Net;
using AI.ProfilePhotoMaker.API.Data;
using AI.ProfilePhotoMaker.API.Models.Career;
using AI.ProfilePhotoMaker.API.Services.Career;
using AI.ProfilePhotoMaker.API.Tests.Infrastructure;
using AI.ProfilePhotoMaker.API.Tests.Integration.Career;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace AI.ProfilePhotoMaker.API.Tests.Services.Career;

/// <summary>Review findings PRIV-1..PRIV-6 of #392: upload fence, fail-closed replay, allowance dating, batching.</summary>
public class CareerPrivacyHardeningTests
{
    private const string Owner = "owner";
    private readonly string _name = $"career-hardening-{Guid.NewGuid():N}";
    private readonly TestTimeProvider _clock = new(new DateTimeOffset(2026, 10, 4, 12, 0, 0, TimeSpan.Zero));
    private readonly InMemoryStorage _storage = new();

    private ApplicationDbContext NewDb() => new(new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(_name)
        .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning)).Options);
    private DateTime Now => _clock.GetUtcNow().UtcDateTime;

    private CareerPrivateDataService Data(ApplicationDbContext db) =>
        new(db, _storage, _clock, Options.Create(new CareerPrivacyOptions { PurgeBackoffMilliseconds = 0 }));

    private CareerTombstoneReplayer Replayer(ApplicationDbContext db, ICareerPrivateDataService? data = null) =>
        new(db, data ?? Data(db), NullLogger<CareerTombstoneReplayer>.Instance, _clock);

    // ---- PRIV-1 ----------------------------------------------------------------

    [Theory]
    [InlineData(CareerDeletionScopes.RawDocuments)]
    [InlineData(CareerDeletionScopes.CareerProfile)]
    public async Task AnUploadWhoseRowWasDeletedAndTombstonedBeforeTheBlobWriteLeavesNoBlobAndNoRow(string scope)
    {
        await using var db = NewDb();
        var service = new ResumeImportService(db, _storage, new ThrowingParser(), new IMalwareScanner[] { new ControllableScanner() },
            new ConfigurationBuilder().Build(), _clock, NullLogger<ResumeImportService>.Instance);
        _storage.BeforeSave = () =>
        {
            // The deletion lands between the row insert and the blob write.
            using var other = NewDb();
            other.CareerResumeDocuments.RemoveRange(other.CareerResumeDocuments.Where(d => d.OwnerId == Owner));
            other.CareerTombstones.Add(new CareerTombstone { Id = Guid.NewGuid(), OwnerId = Owner, Scope = scope, CreatedAt = Now });
            other.SaveChanges();
        };

        var outcome = await service.UploadAsync(Owner, ResumeFixtures.Pdf(ResumeFixtures.MorganPages), "r.pdf", true, ResumeImportService.DefaultConsentVersion);

        outcome.Kind.Should().Be(CareerOutcomeKind.Gone);
        _storage.Count.Should().Be(0, "the fence deletes a blob written after the deletion");
        await using var check = NewDb();
        check.CareerResumeDocuments.Count(d => d.OwnerId == Owner).Should().Be(0);
    }

    // ---- PRIV-2 / PRIV-6 -------------------------------------------------------

    private async Task AddTombstoneAsync(string owner, string scope, DateTime at)
    {
        await using var db = NewDb();
        db.CareerTombstones.Add(new CareerTombstone { Id = Guid.NewGuid(), OwnerId = owner, Scope = scope, CreatedAt = at });
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task AFailedReplayIsPersistedPerOwnerAndASuccessfulOneClearsIt()
    {
        await AddTombstoneAsync(Owner, CareerDeletionScopes.CareerProfile, Now);
        await AddTombstoneAsync("fine", CareerDeletionScopes.CareerProfile, Now);
        await using (var seed = NewDb())
        {
            CareerPrivacySeed.SeedAll(seed, Owner, Now.AddDays(-1), _storage);
            await seed.SaveChangesAsync();
        }
        _storage.FailNextDeletes = 5;

        await using (var db = NewDb())
        {
            (await Replayer(db).ApplyAsync()).Should().Be(1);
        }
        await using (var check = NewDb())
        {
            check.CareerTombstones.Single(t => t.OwnerId == Owner).ReplayFailedAt.Should().NotBeNull();
            check.CareerTombstones.Single(t => t.OwnerId == "fine").ReplayFailedAt.Should().BeNull();
            (await new CareerReplayGate(check).IsPendingAsync(Owner)).Should().BeTrue();
            (await new CareerReplayGate(check).IsPendingAsync("fine")).Should().BeFalse();
        }

        await using (var db = NewDb())
        {
            (await Replayer(db).ApplyAsync()).Should().Be(0);
        }
        await using var after = NewDb();
        after.CareerTombstones.Single(t => t.OwnerId == Owner).Should().Match<CareerTombstone>(t => t.ReplayFailedAt == null && t.ReplayedAt != null);
        (await new CareerReplayGate(after).IsPendingAsync(Owner)).Should().BeFalse();
    }

    [Fact]
    public async Task ReplayPurgesOncePerOwnerAndScopeWithTheNewestCutoffAndIsIdempotent()
    {
        await AddTombstoneAsync(Owner, CareerDeletionScopes.CareerProfile, Now.AddDays(-5));
        await AddTombstoneAsync(Owner, CareerDeletionScopes.CareerProfile, Now);
        await AddTombstoneAsync(Owner, CareerDeletionScopes.RawDocuments, Now);
        var counting = new CountingData();

        await using (var db = NewDb())
        {
            await Replayer(db, counting).ApplyAsync();
        }
        counting.Applied.Should().BeEquivalentTo(new[]
        {
            (Owner, CareerDeletionScopes.CareerProfile, Now), (Owner, CareerDeletionScopes.RawDocuments, Now)
        });

        await using var real = NewDb();
        (await Replayer(real).ApplyAsync()).Should().Be(0);
        (await Replayer(real).ApplyAsync()).Should().Be(0);
    }

    [Fact]
    public async Task TheHostedServiceRetriesWithBackoffUntilTheReplaySucceeds()
    {
        var replayer = new FlakyReplayer(failures: 2);
        var services = new ServiceCollection().AddSingleton<ICareerTombstoneReplayer>(replayer).BuildServiceProvider();
        var hosted = new CareerTombstoneReplayHostedService(services.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<CareerTombstoneReplayHostedService>.Instance, Options.Create(new CareerPrivacyOptions { ReplayRetryMilliseconds = 1 }));

        await hosted.StartAsync(CancellationToken.None);
        await replayer.Done.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await hosted.StopAsync(CancellationToken.None);

        replayer.Calls.Should().Be(3);
    }

    // ---- PRIV-3 ----------------------------------------------------------------

    [Fact]
    public async Task AnAllowanceCreatedAfterATombstoneInTheSameMonthSurvivesReplay()
    {
        var month = new DateTime(Now.Year, Now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        await using (var db = NewDb())
        {
            db.CareerAllowances.Add(new CareerAllowance { Id = Guid.NewGuid(), OwnerId = Owner, PeriodStart = month, CreatedAt = Now.AddHours(1) });
            db.CareerAllowances.Add(new CareerAllowance { Id = Guid.NewGuid(), OwnerId = "old", PeriodStart = month, CreatedAt = Now.AddHours(-1) });
            await db.SaveChangesAsync();
        }
        await AddTombstoneAsync(Owner, CareerDeletionScopes.CareerProfile, Now);
        await AddTombstoneAsync("old", CareerDeletionScopes.CareerProfile, Now);

        await using (var db = NewDb())
        {
            await Replayer(db).ApplyAsync();
        }

        await using var check = NewDb();
        check.CareerAllowances.Count(a => a.OwnerId == Owner).Should().Be(1, "created after the tombstone, though PeriodStart is earlier");
        check.CareerAllowances.Count(a => a.OwnerId == "old").Should().Be(0);
    }

    // ---- PRIV-2 API ------------------------------------------------------------

    [Fact]
    public async Task CareerReadsAndExportsAre503WhileReplayIsPendingAndOkAfter()
    {
        using var factory = new CareerPrivacyFactory();
        var user = new CareerClient(factory);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.CareerTombstones.Add(new CareerTombstone
            {
                Id = Guid.NewGuid(), OwnerId = user.UserId, Scope = CareerDeletionScopes.CareerProfile,
                CreatedAt = DateTime.UtcNow.AddDays(-1), ReplayFailedAt = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        }

        foreach (var path in new[] { "/api/career/profile", "/api/career/resumes", "/api/career/privacy/export" })
        {
            var response = await user.GetAsync(path);
            response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable, path);
            (await CareerClient.ReadErrorAsync(response, 503)).GetProperty("code").GetString().Should().Be("CareerPrivacyReplayPending");
        }
        // Deletion status stays reachable so the user can still delete.
        (await user.GetAsync("/api/career/privacy/retention")).StatusCode.Should().Be(HttpStatusCode.OK);

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await Replayer(db, scope.ServiceProvider.GetRequiredService<ICareerPrivateDataService>()).ApplyAsync();
        }

        (await user.GetAsync("/api/career/resumes")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await user.GetAsync("/api/career/privacy/export")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private sealed class CountingData : ICareerPrivateDataService
    {
        public List<(string, string, DateTime)> Applied { get; } = new();
        public Task DeleteAllForOwnerAsync(string ownerId, CancellationToken ct = default) => Task.CompletedTask;
        public Task<CareerDeletionRequest> DeleteAsync(string ownerId, string scope, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<CareerDeletionRequest> RetryAsync(CareerDeletionRequest request, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<bool> ApplyTombstoneAsync(CareerTombstone tombstone, CancellationToken ct = default)
        {
            Applied.Add((tombstone.OwnerId, tombstone.Scope, tombstone.CreatedAt));
            return Task.FromResult(true);
        }
    }

    private sealed class FlakyReplayer(int failures) : ICareerTombstoneReplayer
    {
        public int Calls { get; private set; }
        public TaskCompletionSource Done { get; } = new();
        public Task<int> ApplyAsync(CancellationToken ct = default)
        {
            Calls++;
            if (Calls > failures)
            {
                Done.TrySetResult();
                return Task.FromResult(0);
            }
            return Task.FromResult(1);
        }
    }
}
