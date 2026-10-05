using AI.ProfilePhotoMaker.API.Data;
using AI.ProfilePhotoMaker.API.Models;
using AI.ProfilePhotoMaker.API.Models.Career;
using AI.ProfilePhotoMaker.API.Services;
using AI.ProfilePhotoMaker.API.Services.Career;
using AI.ProfilePhotoMaker.API.Tests.Infrastructure;
using AI.ProfilePhotoMaker.API.Tests.Integration.Career;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace AI.ProfilePhotoMaker.API.Tests.Services.Career;

/// <summary>
/// Purge, tombstone replay, the worker/deletion race and account-deletion wiring (ADR 0020) on the
/// in-memory provider. What still needs a real SQL Server: the tombstone insert and the row deletes are
/// separate saves here and not one transaction, and blob deletion is only simulated by InMemoryStorage.
/// </summary>
public class CareerPrivacyPurgeTests
{
    private const string Owner = "owner";
    private const string Other = "other";

    private readonly string _name = $"career-privacy-{Guid.NewGuid():N}";
    private readonly TestTimeProvider _clock = new(new DateTimeOffset(2026, 10, 4, 12, 0, 0, TimeSpan.Zero));
    private readonly InMemoryStorage _storage = new();

    private DbContextOptions<ApplicationDbContext> DbOptions => new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(_name)
        .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning)).Options;
    private ApplicationDbContext NewDb() => new(DbOptions);
    private DateTime Now => _clock.GetUtcNow().UtcDateTime;

    private CareerPrivateDataService Data(ApplicationDbContext db) =>
        new(db, _storage, _clock, Options.Create(new CareerPrivacyOptions { PurgeBackoffMilliseconds = 0 }));

    private async Task SeedAsync(string owner, DateTime at)
    {
        await using var db = NewDb();
        CareerPrivacySeed.SeedAll(db, owner, at, _storage);
        await db.SaveChangesAsync();
    }

    private async Task<int> TotalAsync(string owner)
    {
        await using var db = NewDb();
        return await CareerPrivacySeed.TotalAsync(db, owner);
    }

    // ---- Scopes ----------------------------------------------------------------

    [Fact]
    public async Task RawDocumentsRemovesFilesResumeRowsAndExcerptsOnly()
    {
        await SeedAsync(Owner, Now.AddDays(-1));
        await SeedAsync(Other, Now.AddDays(-1));
        var before = await TotalAsync(Owner);

        await using (var db = NewDb())
        {
            var request = await Data(db).DeleteAsync(Owner, CareerDeletionScopes.RawDocuments);
            request.Status.Should().Be(CareerDeletionStatuses.Completed);
        }

        (await TotalAsync(Owner)).Should().Be(before - 1);
        (await TotalAsync(Other)).Should().Be(before);
        _storage.Count.Should().Be(1, "only the other owner's file is left");
        await using var check = NewDb();
        check.CareerProfileProposalItems.Where(i => i.OwnerId == Owner).Should().OnlyContain(i => i.Excerpt == "");
        check.CareerProfileProposalItems.Where(i => i.OwnerId == Other).Should().OnlyContain(i => i.Excerpt == CareerPrivacySeed.Excerpt);
        check.CareerProfiles.Count(p => p.OwnerId == Owner).Should().Be(1);
    }

    [Fact]
    public async Task CareerProfileRemovesActiveRunsAndEveryRowButKeepsTheAuditRecords()
    {
        await SeedAsync(Owner, Now.AddDays(-1));
        await using (var db = NewDb())
        {
            db.CareerAgentRuns.Single(r => r.OwnerId == Owner).Status = CareerRunStatus.Working;
            await db.SaveChangesAsync();
        }

        CareerDeletionRequest request;
        await using (var db = NewDb())
        {
            request = await Data(db).DeleteAsync(Owner, CareerDeletionScopes.CareerProfile);
        }

        request.Status.Should().Be(CareerDeletionStatuses.Completed);
        (await TotalAsync(Owner)).Should().Be(0);
        _storage.Count.Should().Be(0);
        await using var check = NewDb();
        check.CareerTombstones.Single(t => t.OwnerId == Owner).Scope.Should().Be("career_profile");
        check.CareerDeletionRequests.Single(r => r.OwnerId == Owner).Status.Should().Be("completed");
    }

    [Fact]
    public async Task ABlobThatFailsFiveRoundsLeavesItsRowAndFailsTheRequestUntilRetried()
    {
        await SeedAsync(Owner, Now.AddDays(-1));
        _storage.FailNextDeletes = 5;
        Guid id;
        await using (var db = NewDb())
        {
            var request = await Data(db).DeleteAsync(Owner, CareerDeletionScopes.CareerProfile);
            id = request.Id;
            request.Status.Should().Be(CareerDeletionStatuses.Failed);
            request.Attempts.Should().Be(5);
            request.LastError.Should().Be("storage_unavailable");
        }
        (await TotalAsync(Owner)).Should().Be(1, "the resume row is the work queue for the missing blob delete");

        await using (var db = NewDb())
        {
            var request = await db.CareerDeletionRequests.SingleAsync(r => r.Id == id);
            var retried = await Data(db).RetryAsync(request);
            retried.Status.Should().Be(CareerDeletionStatuses.Completed);
            retried.LastError.Should().BeNull();
            retried.Attempts.Should().Be(6);
        }
        (await TotalAsync(Owner)).Should().Be(0);
        _storage.Count.Should().Be(0);
    }

    [Fact]
    public async Task ARetryDoesNotDeleteWhatTheUserCreatedAfterAskingForTheDeletion()
    {
        await SeedAsync(Owner, Now.AddDays(-1));
        _storage.FailNextDeletes = 5;
        Guid id;
        await using (var db = NewDb())
        {
            id = (await Data(db).DeleteAsync(Owner, CareerDeletionScopes.CareerProfile)).Id;
        }
        await SeedAsync(Owner, Now.AddDays(2));

        await using (var db = NewDb())
        {
            await Data(db).RetryAsync(await db.CareerDeletionRequests.SingleAsync(r => r.Id == id));
        }

        (await TotalAsync(Owner)).Should().Be(CareerPrivateDataService.CoveredEntityTypes.Count);
    }

    [Fact]
    public async Task DeleteAllForOwnerThrowsWhenAFileCannotBeRemoved()
    {
        await SeedAsync(Owner, Now.AddDays(-1));
        _storage.FailNextDeletes = int.MaxValue;
        await using var db = NewDb();

        var act = () => Data(db).DeleteAllForOwnerAsync(Owner);

        await act.Should().ThrowAsync<CareerPurgeException>();
    }

    // ---- Tombstone replay (restored backup) ------------------------------------

    [Fact]
    public async Task ReplayRemovesRowsRestoredFromBeforeATombstoneAndKeepsNewerOnes()
    {
        var tombstoneAt = Now;
        await using (var db = NewDb())
        {
            db.CareerTombstones.Add(new CareerTombstone { Id = Guid.NewGuid(), OwnerId = Owner, Scope = CareerDeletionScopes.CareerProfile, CreatedAt = tombstoneAt });
            await db.SaveChangesAsync();
        }
        // The "restored backup": rows from before the deletion come back, next to a row made after it.
        await SeedAsync(Owner, tombstoneAt.AddDays(-30));
        await SeedAsync(Owner, tombstoneAt.AddDays(5));
        await SeedAsync(Other, tombstoneAt.AddDays(-30));
        var perSeed = CareerPrivateDataService.CoveredEntityTypes.Count;

        int failed;
        await using (var db = NewDb())
        {
            failed = await Replayer(db).ApplyAsync();
        }

        failed.Should().Be(0);
        (await TotalAsync(Owner)).Should().Be(perSeed, "only the newer seed survives");
        await using (var check = NewDb())
        {
            check.CareerProfiles.Where(p => p.OwnerId == Owner).Should().OnlyContain(p => p.CreatedAt > tombstoneAt);
            check.CareerAgentRuns.Where(p => p.OwnerId == Owner).Should().OnlyContain(p => p.CreatedAt > tombstoneAt);
            check.CareerAllowances.Where(p => p.OwnerId == Owner).Should().OnlyContain(p => p.PeriodStart > tombstoneAt);
        }
        (await TotalAsync(Other)).Should().Be(perSeed, "another owner has no tombstone");
        _storage.Count.Should().Be(2, "the old owner file is deleted; the new one and the other owner's remain");

        await using var again = NewDb();
        (await Replayer(again).ApplyAsync()).Should().Be(0);
        (await TotalAsync(Owner)).Should().Be(perSeed, "replaying twice changes nothing");
    }

    [Fact]
    public async Task ReplayOfARawDocumentsTombstoneOnlyRemovesOldFilesAndExcerpts()
    {
        var tombstoneAt = Now;
        await using (var db = NewDb())
        {
            db.CareerTombstones.Add(new CareerTombstone { Id = Guid.NewGuid(), OwnerId = Owner, Scope = CareerDeletionScopes.RawDocuments, CreatedAt = tombstoneAt });
            await db.SaveChangesAsync();
        }
        await SeedAsync(Owner, tombstoneAt.AddDays(-3));
        await SeedAsync(Owner, tombstoneAt.AddDays(3));
        var total = await TotalAsync(Owner);

        await using (var db = NewDb())
        {
            await Replayer(db).ApplyAsync();
        }

        (await TotalAsync(Owner)).Should().Be(total - 1);
        await using var check = NewDb();
        check.CareerResumeDocuments.Count(d => d.OwnerId == Owner).Should().Be(1);
        check.CareerProfileProposalItems.Count(i => i.OwnerId == Owner && i.Excerpt == "").Should().Be(1);
        check.CareerProfileProposalItems.Count(i => i.OwnerId == Owner && i.Excerpt == CareerPrivacySeed.Excerpt).Should().Be(1);
    }

    [Fact]
    public async Task ReplayReportsAnIncompleteTombstoneAndKeepsGoing()
    {
        await using (var db = NewDb())
        {
            db.CareerTombstones.Add(new CareerTombstone { Id = Guid.NewGuid(), OwnerId = Owner, Scope = CareerDeletionScopes.CareerProfile, CreatedAt = Now });
            db.CareerTombstones.Add(new CareerTombstone { Id = Guid.NewGuid(), OwnerId = Other, Scope = CareerDeletionScopes.CareerProfile, CreatedAt = Now });
            await db.SaveChangesAsync();
        }
        await SeedAsync(Owner, Now.AddDays(-1));
        await SeedAsync(Other, Now.AddDays(-1));
        _storage.FailNextDeletes = 5;

        await using var replay = NewDb();
        (await Replayer(replay).ApplyAsync()).Should().Be(1);

        (await TotalAsync(Other)).Should().Be(0);
    }

    private CareerTombstoneReplayer Replayer(ApplicationDbContext db) =>
        new(db, Data(db), NullLogger<CareerTombstoneReplayer>.Instance);

    // ---- Worker race -----------------------------------------------------------

    private CareerAgentRunner Runner(ApplicationDbContext db, ICareerTextModel model) =>
        new(db, model, Options.Create(new CareerAgentOptions()), _clock, NullLogger<CareerAgentRunner>.Instance);

    private async Task<Guid> QueueSummaryRunAsync()
    {
        await using var db = NewDb();
        var profiles = new CareerProfileService(db, _clock);
        (await profiles.SaveProfileAsync(Owner, new CareerProfileRequest
        {
            CurrentTitle = "Data analyst", YearsExperience = 4, Skills = new List<string?> { "SQL" },
            Highlights = new List<string?> { "Built the KPI report" }, Summary = "Original", Confirmed = true
        }, VersionPrecondition.Absent)).Kind.Should().Be(CareerOutcomeKind.Ok);
        await profiles.CreateGoalAsync(Owner, new CareerGoalRequest { TargetRole = "Senior analyst", Confirmed = true });
        var runs = new CareerAgentRunService(db, Options.Create(new CareerAgentOptions()), _clock,
            NullLogger<CareerAgentRunService>.Instance, new FakeCareerTextModel());
        var outcome = await runs.CreateAsync(Owner, new CreateCareerRunRequest { Task = "profile_summary" }, $"key-{Guid.NewGuid():N}");
        return outcome.Value!.Id;
    }

    [Fact]
    public async Task ARunStartedBeforeDeletionCannotResurrectRowsWhenItFinishesAfter()
    {
        await QueueSummaryRunAsync();
        var model = new HookModel(async () =>
        {
            // The user deletes everything while the worker is inside the model call.
            await using var db = NewDb();
            (await Data(db).DeleteAsync(Owner, CareerDeletionScopes.CareerProfile)).Status.Should().Be("completed");
        });

        await using (var db = NewDb())
        {
            await Runner(db, model).RunOnceAsync("worker-a");
        }

        (await TotalAsync(Owner)).Should().Be(0, "no run, step, proposal or allowance row came back");
    }

    [Fact]
    public async Task AFinishDiscardsItsResultWhenATombstoneIsNewerThanTheRunStart()
    {
        var runId = await QueueSummaryRunAsync();
        var model = new HookModel(async () =>
        {
            // Only the tombstone exists (for example the purge is still running): the fence alone is not lost.
            await using var db = NewDb();
            db.CareerTombstones.Add(new CareerTombstone
            {
                Id = Guid.NewGuid(), OwnerId = Owner, Scope = CareerDeletionScopes.CareerProfile, CreatedAt = Now.AddSeconds(1)
            });
            await db.SaveChangesAsync();
        });

        await using (var db = NewDb())
        {
            await Runner(db, model).RunOnceAsync("worker-a");
        }

        await using var check = NewDb();
        check.CareerProfileProposals.Count(p => p.OwnerId == Owner).Should().Be(0);
        check.CareerAgentRuns.Single(r => r.Id == runId).Status.Should().NotBe(CareerRunStatus.Completed);
    }

    [Fact]
    public async Task ATombstoneFromBeforeTheRunStartedOrForRawDocumentsDoesNotBlockIt()
    {
        var runId = await QueueSummaryRunAsync();
        await using (var db = NewDb())
        {
            db.CareerTombstones.Add(new CareerTombstone { Id = Guid.NewGuid(), OwnerId = Owner, Scope = CareerDeletionScopes.CareerProfile, CreatedAt = Now.AddDays(-1) });
            db.CareerTombstones.Add(new CareerTombstone { Id = Guid.NewGuid(), OwnerId = Owner, Scope = CareerDeletionScopes.RawDocuments, CreatedAt = Now.AddDays(1) });
            await db.SaveChangesAsync();
        }

        await using (var db = NewDb())
        {
            await Runner(db, new FakeCareerTextModel()).RunOnceAsync("worker-a");
        }

        await using var check = NewDb();
        check.CareerAgentRuns.Single(r => r.Id == runId).Status.Should().Be(CareerRunStatus.Completed);
        check.CareerProfileProposals.Count(p => p.OwnerId == Owner).Should().Be(1);
    }

    // ---- Account deletion ------------------------------------------------------

    private async Task<(string UserId, ApplicationUser User)> SeedAccountAsync()
    {
        var userId = Guid.NewGuid().ToString();
        var user = new ApplicationUser { Id = userId, UserName = $"u_{userId}", Email = $"{userId}@example.com" };
        await using var db = NewDb();
        db.Users.Add(user);
        db.UserProfiles.Add(new UserProfile { UserId = userId, User = user, Credits = 3, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow, LastCreditReset = DateTime.UtcNow });
        CareerPrivacySeed.SeedAll(db, userId, Now.AddDays(-1), _storage);
        await db.SaveChangesAsync();
        return (userId, user);
    }

    private AdminService Admin(ApplicationDbContext db, ApplicationUser user)
    {
        var userManager = UserManagerMockFactory.Create();
        userManager.Setup(m => m.FindByIdAsync(user.Id)).ReturnsAsync(user);
        userManager.Setup(m => m.GetUsersInRoleAsync("Admin")).ReturnsAsync(new List<ApplicationUser>
        {
            new() { Id = "admin-a", UserName = "a", Email = "a@test.com" }, new() { Id = "admin-b", UserName = "b", Email = "b@test.com" }
        });
        userManager.Setup(m => m.DeleteAsync(user)).ReturnsAsync(IdentityResult.Success);
        return new AdminService(db, new UserProfileRepository(db), Mock.Of<ICreditPackageService>(), userManager.Object,
            _storage, NullLogger<AdminService>.Instance, Data(db));
    }

    [Fact]
    public async Task AdminUserDeletionPurgesCareerDataFirstAndLeavesATombstone()
    {
        var (userId, user) = await SeedAccountAsync();

        await using (var db = NewDb())
        {
            var result = await Admin(db, user).DeleteUserAsync(userId, "admin-a", "cleanup");
            result.Success.Should().BeTrue(result.Message);
        }

        (await TotalAsync(userId)).Should().Be(0);
        _storage.Count.Should().Be(0);
        await using var check = NewDb();
        check.UserProfiles.Count(p => p.UserId == userId).Should().Be(0);
        check.CareerTombstones.Count(t => t.OwnerId == userId).Should().Be(1);
    }

    [Fact]
    public async Task AdminUserDeletionAbortsWhenTheCareerPurgeFails()
    {
        var (userId, user) = await SeedAccountAsync();
        _storage.FailNextDeletes = int.MaxValue;

        await using (var db = NewDb())
        {
            var result = await Admin(db, user).DeleteUserAsync(userId, "admin-a", "cleanup");
            result.Success.Should().BeFalse();
            result.Message.Should().Contain("career data");
        }

        await using var check = NewDb();
        check.UserProfiles.Count(p => p.UserId == userId).Should().Be(1, "the account stays until its career data is gone");
        check.AdminAuditLogs.Count(l => l.Action == "UserDeleted" && l.TargetUserId == userId).Should().Be(0);
    }

    /// <summary>Runs a callback while the model "call" is in flight, then answers normally.</summary>
    private sealed class HookModel : ICareerTextModel
    {
        private readonly Func<Task> _during;
        public HookModel(Func<Task> during) => _during = during;

        public async Task<CareerModelResult> CompleteAsync(CareerModelRequest request, CancellationToken ct = default)
        {
            await _during();
            return await new FakeCareerTextModel().CompleteAsync(request, ct);
        }
    }
}
