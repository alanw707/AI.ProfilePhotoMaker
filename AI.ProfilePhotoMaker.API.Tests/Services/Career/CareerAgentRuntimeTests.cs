using AI.ProfilePhotoMaker.API.Data;
using AI.ProfilePhotoMaker.API.Models.Career;
using AI.ProfilePhotoMaker.API.Services.Career;
using AI.ProfilePhotoMaker.API.Services.Storage;
using AI.ProfilePhotoMaker.API.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace AI.ProfilePhotoMaker.API.Tests.Services.Career;

/// <summary>
/// Agent runtime invariants (ADR 0009) on the in-memory provider, with a controllable
/// clock and a second context standing in for a second worker or request.
///
/// Simulated here, and still needing a real SQL Server to be proven:
///  - the unique (OwnerId, IdempotencyKey) and (OwnerId, PeriodStart) indexes (SQL errors
///    2601/2627): the lost race is simulated by a context that lets the winner commit and
///    then throws, the way the service classifies a lost race;
///  - rowversion/concurrency-token races under real isolation and atomic multi-row saves:
///    in-memory EF enforces tokens per row but has no transaction, so a stale fenced write
///    is only proven to be rejected, not to roll back sibling rows;
///  - lease expiry against the database clock (tests drive a manual TimeProvider).
/// </summary>
public class CareerAgentRuntimeTests
{
    private const string Owner = "owner";

    private readonly string _name = $"career-agent-{Guid.NewGuid():N}";
    private readonly TestTimeProvider _clock = new(new DateTimeOffset(2026, 10, 4, 12, 0, 0, TimeSpan.Zero));
    private readonly CareerAgentOptions _options = new();

    // ---- Harness ---------------------------------------------------------------

    private DbContextOptions<ApplicationDbContext> DbOptions => new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(_name).Options;
    private ApplicationDbContext NewDb() => new(DbOptions);

    private CareerAgentRunService Runs(ApplicationDbContext db, ICareerTextModel? model = null) =>
        new(db, Options.Create(_options), _clock, NullLogger<CareerAgentRunService>.Instance, model ?? new FakeCareerTextModel());

    private CareerAgentRunner Runner(ApplicationDbContext db, ICareerTextModel model) =>
        new(db, model, Options.Create(_options), _clock, NullLogger<CareerAgentRunner>.Instance);

    private async Task SeedProfileAsync(string owner = Owner, bool withGoal = true)
    {
        await using var db = NewDb();
        var profiles = new CareerProfileService(db, _clock);
        (await profiles.SaveProfileAsync(owner, new CareerProfileRequest
        {
            CurrentTitle = "Data analyst",
            YearsExperience = 4,
            Skills = new List<string?> { "SQL" },
            Highlights = new List<string?> { "Built the KPI report" },
            Summary = "Original summary",
            Confirmed = true
        }, VersionPrecondition.Absent)).Kind.Should().Be(CareerOutcomeKind.Ok);
        if (withGoal)
        {
            (await profiles.CreateGoalAsync(owner, new CareerGoalRequest { TargetRole = "Senior analyst", Confirmed = true })).Kind
                .Should().Be(CareerOutcomeKind.Created);
        }
    }

    private async Task<Guid> CreateRunAsync(string owner = Owner, string? key = null, ICareerTextModel? model = null)
    {
        await using var db = NewDb();
        var outcome = await Runs(db, model).CreateAsync(owner, new CreateCareerRunRequest { Task = "profile_summary" }, key ?? $"key-{Guid.NewGuid():N}");
        outcome.Kind.Should().Be(CareerOutcomeKind.Ok, outcome.Message);
        return outcome.Value!.Id;
    }

    private async Task<CareerAgentRun> LoadRunAsync(Guid id)
    {
        await using var db = NewDb();
        return await db.CareerAgentRuns.AsNoTracking().SingleAsync(r => r.Id == id);
    }

    private async Task<CareerAllowance> AllowanceAsync(string owner = Owner)
    {
        await using var db = NewDb();
        return await db.CareerAllowances.AsNoTracking().SingleAsync(a => a.OwnerId == owner);
    }

    private async Task<int> ProposalCountAsync(string owner = Owner)
    {
        await using var db = NewDb();
        return await db.CareerProfileProposals.CountAsync(p => p.OwnerId == owner);
    }

    private async Task<List<string>> StepNamesAsync(Guid runId)
    {
        await using var db = NewDb();
        return await db.CareerAgentSteps.Where(s => s.RunId == runId).OrderBy(s => s.Ordinal).Select(s => s.Name).ToListAsync();
    }

    private async Task<bool> RunOnceAsync(ICareerTextModel model, string worker = "worker-a")
    {
        await using var db = NewDb();
        return await Runner(db, model).RunOnceAsync(worker);
    }

    // ---- Creation: idempotency and allowance -----------------------------------

    [Fact]
    public async Task LosingTheIdempotencyKeyRaceReturnsTheWinnersRun()
    {
        await SeedProfileAsync();
        Guid winner = Guid.Empty;
        await using var racing = new RacingContext(DbOptions, async () =>
        {
            winner = await CreateRunAsync(key: "shared-key-1234");
            throw new DbUpdateConcurrencyException("Simulated unique (OwnerId, IdempotencyKey) violation.");
        });

        var outcome = await Runs(racing).CreateAsync(Owner, new CreateCareerRunRequest { Task = "profile_summary" }, "shared-key-1234");

        outcome.Kind.Should().Be(CareerOutcomeKind.Ok);
        outcome.Value!.Id.Should().Be(winner);
        await using var check = NewDb();
        check.CareerAgentRuns.Count().Should().Be(1);
        (await AllowanceAsync()).Reserved.Should().Be(1);
    }

    [Fact]
    public async Task TwoCreatesCompetingForTheLastAllowanceUnitYieldOneRunAndOneRefusal()
    {
        _options.MonthlyRunAllowance = 2;
        await SeedProfileAsync();
        await CreateRunAsync();

        CareerOutcome<CareerAgentRunDto>? other = null;
        await using var racing = new RacingContext(DbOptions, async () =>
        {
            await using var db = NewDb();
            other = await Runs(db).CreateAsync(Owner, new CreateCareerRunRequest { Task = "profile_summary" }, "other-key-1234");
        }, atomic: true);

        var mine = await Runs(racing).CreateAsync(Owner, new CreateCareerRunRequest { Task = "profile_summary" }, "my-key-1234");

        new[] { mine, other! }.Count(o => o.Kind == CareerOutcomeKind.Ok).Should().Be(1);
        new[] { mine, other! }.Count(o => o.Kind == CareerOutcomeKind.QuotaExceeded).Should().Be(1);
        new[] { mine, other! }.Single(o => o.Kind == CareerOutcomeKind.QuotaExceeded).ErrorCode.Should().Be("CareerAllowanceExhausted");
        (await AllowanceAsync()).Reserved.Should().Be(2);
        await using var check = NewDb();
        check.CareerAgentRuns.Count().Should().Be(2);
    }

    // ---- Claiming, leases and fencing ------------------------------------------

    [Fact]
    public async Task TwoWorkersClaimingTheSameRunExecuteItOnlyOnce()
    {
        await SeedProfileAsync();
        var runId = await CreateRunAsync();
        var model = new CountingModel();
        bool workerBRan = true;

        // Worker B has read the run as queued when worker A claims and finishes it.
        await using var dbB = new RacingContext(DbOptions, async () => await RunOnceAsync(model, "worker-a"));
        workerBRan = await Runner(dbB, model).RunOnceAsync("worker-b");

        workerBRan.Should().BeFalse();
        model.Calls.Should().Be(1);
        (await LoadRunAsync(runId)).Status.Should().Be(CareerRunStatus.Completed);
        (await LoadRunAsync(runId)).Attempts.Should().Be(1);
        (await ProposalCountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task ConcurrentRunnersProduceExactlyOneProposal()
    {
        await SeedProfileAsync();
        await CreateRunAsync();
        var model = new CountingModel();

        await Task.WhenAll(RunOnceAsync(model, "a"), RunOnceAsync(model, "b"), RunOnceAsync(model, "c"));

        (await ProposalCountAsync()).Should().Be(1);
        model.Calls.Should().Be(1);
    }

    [Fact]
    public async Task ARunWhoseLeaseExpiredIsReclaimedAndTheOldWorkersLateWriteIsDiscarded()
    {
        await SeedProfileAsync();
        var runId = await CreateRunAsync();
        var modelB = new CountingModel();

        // Worker A stalls inside its model call; the lease runs out and B finishes the run.
        var modelA = new HookModel(async () =>
        {
            _clock.Advance(TimeSpan.FromSeconds(_options.LeaseSeconds + 1));
            (await RunOnceAsync(modelB, "worker-b")).Should().BeTrue();
        });

        (await RunOnceAsync(modelA, "worker-a")).Should().BeTrue();

        var run = await LoadRunAsync(runId);
        run.Status.Should().Be(CareerRunStatus.Completed);
        run.Attempts.Should().Be(2);
        (await ProposalCountAsync()).Should().Be(1);
        (await StepNamesAsync(runId)).Should().Equal("read_profile", "read_goal", "draft_summary", "save_proposal");
        (await AllowanceAsync()).Used.Should().Be(1);
        (await AllowanceAsync()).Reserved.Should().Be(0);
    }

    [Fact]
    public async Task ARunWithALiveLeaseIsNotClaimedByAnotherWorker()
    {
        await SeedProfileAsync();
        var runId = await CreateRunAsync();
        var model = new CountingModel();
        var modelA = new HookModel(async () => (await RunOnceAsync(model, "worker-b")).Should().BeFalse());

        await RunOnceAsync(modelA, "worker-a");

        model.Calls.Should().Be(0);
        (await LoadRunAsync(runId)).Status.Should().Be(CareerRunStatus.Completed);
    }

    // ---- Crash after the provider answered -------------------------------------

    [Fact]
    public async Task CrashAfterTheModelStepWasSavedResumesWithoutCallingTheModelAgain()
    {
        await SeedProfileAsync();
        var runId = await CreateRunAsync();
        var model = new CountingModel();

        await using (var crashing = new CrashAfterSaveContext(DbOptions, step => step == "draft_summary"))
        {
            var act = () => Runner(crashing, model).RunOnceAsync("worker-a");
            await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("simulated crash");
        }
        (await ProposalCountAsync()).Should().Be(0);
        (await LoadRunAsync(runId)).Status.Should().Be(CareerRunStatus.Working);

        _clock.Advance(TimeSpan.FromSeconds(_options.LeaseSeconds + 1));
        (await RunOnceAsync(model, "worker-b")).Should().BeTrue();

        model.Calls.Should().Be(1);
        (await LoadRunAsync(runId)).Status.Should().Be(CareerRunStatus.Completed);
        (await ProposalCountAsync()).Should().Be(1);
        (await StepNamesAsync(runId)).Should().Equal("read_profile", "read_goal", "draft_summary", "save_proposal");
        (await AllowanceAsync()).Used.Should().Be(1);
    }

    // ---- Cancel ----------------------------------------------------------------

    [Fact]
    public async Task CancelDuringTheModelCallDiscardsTheWorkersCompletionAndSpendsTheUnit()
    {
        await SeedProfileAsync();
        var runId = await CreateRunAsync();
        var model = new HookModel(async () =>
        {
            await using var db = NewDb();
            (await Runs(db).CancelAsync(Owner, runId)).Value!.Status.Should().Be("cancelled");
        });

        (await RunOnceAsync(model)).Should().BeTrue();

        (await LoadRunAsync(runId)).Status.Should().Be(CareerRunStatus.Cancelled);
        (await ProposalCountAsync()).Should().Be(0);
        (await StepNamesAsync(runId)).Should().NotContain("draft_summary").And.NotContain("save_proposal");
        // The provider call had started, so the draft counts even though the result is dropped.
        var allowance = await AllowanceAsync();
        (allowance.Reserved, allowance.Used).Should().Be((0, 1));
    }

    [Fact]
    public async Task CancelIsIdempotentAndBumpsTheFencingTokenOnce()
    {
        await SeedProfileAsync();
        var runId = await CreateRunAsync();
        await using var db = NewDb();
        var service = Runs(db);

        var first = await service.CancelAsync(Owner, runId);
        var tokenAfterFirst = (await LoadRunAsync(runId)).FencingToken;
        var second = await service.CancelAsync(Owner, runId);

        first.Value!.Status.Should().Be("cancelled");
        second.Value!.Status.Should().Be("cancelled");
        (await LoadRunAsync(runId)).FencingToken.Should().Be(tokenAfterFirst);
        (await AllowanceAsync()).Reserved.Should().Be(0);
        (await RunOnceAsync(new CountingModel())).Should().BeFalse();
    }

    [Fact]
    public async Task CancelAfterTheModelWasCalledSettlesTheUnitAsUsed()
    {
        await SeedProfileAsync();
        var runId = await CreateRunAsync();
        var model = new CountingModel();
        await using (var crashing = new CrashAfterSaveContext(DbOptions, step => step == "draft_summary"))
        {
            await Runner(crashing, model).Invoking(r => r.RunOnceAsync("worker-a")).Should().ThrowAsync<InvalidOperationException>();
        }

        await using var db = NewDb();
        (await Runs(db).CancelAsync(Owner, runId)).Value!.Status.Should().Be("cancelled");

        var allowance = await AllowanceAsync();
        (allowance.Reserved, allowance.Used).Should().Be((0, 1));
        (await ProposalCountAsync()).Should().Be(0);
    }

    // ---- Review fixes: fencing on terminal writes, spend accounting, backoff ----

    [Fact]
    public async Task AWorkerThatLostItsLeaseCannotCompleteARunTheRetryLimitFailed()
    {
        await SeedProfileAsync();
        var runId = await CreateRunAsync();
        for (var attempt = 1; attempt < _options.MaxAttempts; attempt++)
        {
            await using var crashing = new CrashAfterClaimContext(DbOptions);
            await Runner(crashing, new CountingModel()).Invoking(r => r.RunOnceAsync("w")).Should().ThrowAsync<InvalidOperationException>();
            _clock.Advance(TimeSpan.FromSeconds(_options.LeaseSeconds + 1));
        }

        // Worker A takes the last allowed claim. While its model call is slow the lease
        // expires and worker B fails the run with the retry limit.
        var model = new HookModel(async () =>
        {
            _clock.Advance(TimeSpan.FromSeconds(_options.LeaseSeconds + 1));
            (await RunOnceAsync(new CountingModel(), "worker-b")).Should().BeTrue();
        });
        await RunOnceAsync(model, "worker-a");

        var run = await LoadRunAsync(runId);
        (run.Status, run.ErrorCode).Should().Be((CareerRunStatus.Failed, "CareerRetryLimit"));
        run.ProposalId.Should().BeNull();
        (await ProposalCountAsync()).Should().Be(0);
        var allowance = await AllowanceAsync();
        (allowance.Reserved, allowance.Used).Should().Be((0, 1));
    }

    [Fact]
    public async Task TheModelCallIsRecordedBeforeTheProviderIsAsked()
    {
        await SeedProfileAsync();
        var runId = await CreateRunAsync();
        bool? recordedDuringCall = null;
        var model = new HookModel(async () => recordedDuringCall = (await LoadRunAsync(runId)).ModelCalled);

        await RunOnceAsync(model);

        recordedDuringCall.Should().BeTrue();
    }

    [Fact]
    public async Task AFailedModelCallIsRetriedOnlyAfterABackoff()
    {
        await SeedProfileAsync();
        var runId = await CreateRunAsync();
        var model = new ThrowingModel { FailFirst = 1 };

        (await RunOnceAsync(model)).Should().BeTrue();
        (await RunOnceAsync(model)).Should().BeFalse();
        model.Calls.Should().Be(1);

        _clock.Advance(TimeSpan.FromSeconds(_options.RetryBackoffSeconds + 1));
        (await RunOnceAsync(model)).Should().BeTrue();

        model.Calls.Should().Be(2);
        (await LoadRunAsync(runId)).Status.Should().Be(CareerRunStatus.Completed);
    }

    [Fact]
    public async Task AModelCallThatWouldOutliveTheLeaseIsAbandonedAndRetried()
    {
        // Lease 6s gives the call a 1s budget, so another worker never re-claims mid-call.
        _options.LeaseSeconds = 6;
        await SeedProfileAsync();
        var runId = await CreateRunAsync();
        var model = new HangingModel();

        (await RunOnceAsync(model)).Should().BeTrue();

        model.SawCancellation.Should().BeTrue();
        var run = await LoadRunAsync(runId);
        run.Status.Should().Be(CareerRunStatus.Queued);
        (await ProposalCountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task AnUnansweredQuestionExpiresAndReleasesTheUnit()
    {
        await SeedProfileAsync(withGoal: false);
        var runId = await CreateRunAsync();
        await RunOnceAsync(new CountingModel());
        (await LoadRunAsync(runId)).Status.Should().Be(CareerRunStatus.NeedsInput);

        _clock.Advance(TimeSpan.FromHours(_options.QuestionExpiryHours) - TimeSpan.FromMinutes(1));
        (await RunOnceAsync(new CountingModel())).Should().BeFalse();
        _clock.Advance(TimeSpan.FromMinutes(2));
        (await RunOnceAsync(new CountingModel())).Should().BeTrue();

        var run = await LoadRunAsync(runId);
        (run.Status, run.ErrorCode).Should().Be((CareerRunStatus.Failed, "CareerQuestionExpired"));
        var allowance = await AllowanceAsync();
        (allowance.Reserved, allowance.Used).Should().Be((0, 0));
    }

    [Fact]
    public async Task ANonRetryableModelErrorFailsTheRunWithoutRetrying()
    {
        await SeedProfileAsync();
        var runId = await CreateRunAsync();
        var model = new FatalModel();

        (await RunOnceAsync(model)).Should().BeTrue();
        _clock.Advance(TimeSpan.FromSeconds(_options.RetryBackoffSeconds * _options.MaxAttempts + 1));
        (await RunOnceAsync(model)).Should().BeFalse();

        var run = await LoadRunAsync(runId);
        (run.Status, run.ErrorCode).Should().Be((CareerRunStatus.Failed, "CareerModelFailed"));
        model.Calls.Should().Be(1);
        // The call was made, so the reserved unit is spent, never left reserved.
        var allowance = await AllowanceAsync();
        (allowance.Reserved, allowance.Used).Should().Be((0, 1));
    }

    [Fact]
    public async Task ARetryableModelErrorIsRetried()
    {
        await SeedProfileAsync();
        var runId = await CreateRunAsync();
        var model = new FatalModel { Retryable = true };

        await RunOnceAsync(model);

        (await LoadRunAsync(runId)).Status.Should().Be(CareerRunStatus.Queued);
    }

    [Fact]
    public async Task UnusableButPaidModelResponsesCountTowardTheCostCeiling()
    {
        await SeedProfileAsync();
        var runId = await CreateRunAsync();
        var model = new FatalModel { Retryable = true, CostCents = _options.MaxCostCents + 1 };

        await RunOnceAsync(model);

        var run = await LoadRunAsync(runId);
        (run.Status, run.ErrorCode).Should().Be((CareerRunStatus.Failed, "CareerCostLimit"));
        run.CostCents.Should().Be(_options.MaxCostCents + 1);
        model.Calls.Should().Be(1);
    }

    [Fact]
    public async Task ARetryableFailureKeepsItsCostOnTheRun()
    {
        await SeedProfileAsync();
        var runId = await CreateRunAsync();
        var model = new FatalModel { Retryable = true, CostCents = 2 };

        await RunOnceAsync(model);

        var run = await LoadRunAsync(runId);
        (run.Status, run.CostCents).Should().Be((CareerRunStatus.Queued, 2));
    }

    // ---- Tools, ceilings and failures ------------------------------------------

    [Fact]
    public async Task AModelAskingForADisallowedToolFailsTheRunWithNoProposal()
    {
        await SeedProfileAsync();
        var runId = await CreateRunAsync();

        await RunOnceAsync(new ToolRequestingModel("send_email"));

        var run = await LoadRunAsync(runId);
        (run.Status, run.ErrorCode).Should().Be((CareerRunStatus.Failed, "CareerToolNotAllowed"));
        (await ProposalCountAsync()).Should().Be(0);
        (await AllowanceAsync()).Used.Should().Be(1);
    }

    [Fact]
    public async Task TheStepCeilingFailsTheRunBeforeTheModelIsCalled()
    {
        _options.MaxSteps = 2;
        await SeedProfileAsync();
        var runId = await CreateRunAsync();
        var model = new CountingModel();

        await RunOnceAsync(model);

        var run = await LoadRunAsync(runId);
        (run.Status, run.ErrorCode).Should().Be((CareerRunStatus.Failed, "CareerStepLimit"));
        model.Calls.Should().Be(0);
        (await ProposalCountAsync()).Should().Be(0);
        (await AllowanceAsync()).Reserved.Should().Be(0);
        (await AllowanceAsync()).Used.Should().Be(0);
    }

    [Fact]
    public async Task TheTimeCeilingFailsARunThatOutlastsItsDeadline()
    {
        await SeedProfileAsync();
        var runId = await CreateRunAsync();
        var model = new HookModel(() =>
        {
            _clock.Advance(TimeSpan.FromSeconds(_options.MaxRunSeconds + 1));
            return Task.CompletedTask;
        });

        await RunOnceAsync(model);

        var run = await LoadRunAsync(runId);
        (run.Status, run.ErrorCode).Should().Be((CareerRunStatus.Failed, "CareerTimeLimit"));
        (await ProposalCountAsync()).Should().Be(0);
        (await AllowanceAsync()).Used.Should().Be(1);
    }

    [Fact]
    public async Task TheCostCeilingFailsARunWhoseModelCostTooMuch()
    {
        await SeedProfileAsync();
        var runId = await CreateRunAsync();

        await RunOnceAsync(new CostlyModel(_options.MaxCostCents + 1));

        var run = await LoadRunAsync(runId);
        (run.Status, run.ErrorCode).Should().Be((CareerRunStatus.Failed, "CareerCostLimit"));
        run.CostCents.Should().Be(_options.MaxCostCents + 1);
        (await ProposalCountAsync()).Should().Be(0);
        (await AllowanceAsync()).Used.Should().Be(1);
    }

    [Fact]
    public async Task RunsThatKeepLosingTheirWorkerFailWithTheRetryLimit()
    {
        await SeedProfileAsync();
        var runId = await CreateRunAsync();
        var model = new CountingModel();
        for (var attempt = 1; attempt <= _options.MaxAttempts; attempt++)
        {
            await using var crashing = new CrashAfterClaimContext(DbOptions);
            await Runner(crashing, model).Invoking(r => r.RunOnceAsync("w")).Should().ThrowAsync<InvalidOperationException>();
            _clock.Advance(TimeSpan.FromSeconds(_options.LeaseSeconds + 1));
        }

        (await RunOnceAsync(model)).Should().BeTrue();

        var run = await LoadRunAsync(runId);
        (run.Status, run.ErrorCode).Should().Be((CareerRunStatus.Failed, "CareerRetryLimit"));
        model.Calls.Should().Be(0);
        (await AllowanceAsync()).Reserved.Should().Be(0);
        (await AllowanceAsync()).Used.Should().Be(0);
    }

    [Fact]
    public async Task AModelThatKeepsFailingIsRetriedThenFailsWithModelFailed()
    {
        await SeedProfileAsync();
        var runId = await CreateRunAsync();
        var model = new ThrowingModel();

        for (var attempt = 1; attempt < _options.MaxAttempts; attempt++)
        {
            (await RunOnceAsync(model)).Should().BeTrue();
            var retrying = await LoadRunAsync(runId);
            retrying.Status.Should().Be(CareerRunStatus.Queued);
            retrying.LeaseOwner.Should().BeNull();
            _clock.Advance(TimeSpan.FromSeconds(_options.RetryBackoffSeconds * attempt + 1));
        }
        (await RunOnceAsync(model)).Should().BeTrue();

        var run = await LoadRunAsync(runId);
        (run.Status, run.ErrorCode).Should().Be((CareerRunStatus.Failed, "CareerModelFailed"));
        model.Calls.Should().Be(_options.MaxAttempts);
        (await ProposalCountAsync()).Should().Be(0);
        // Calls were started (the provider may have billed them), so the one reserved unit is spent.
        (await AllowanceAsync()).Used.Should().Be(1);
        (await AllowanceAsync()).Reserved.Should().Be(0);
    }

    [Fact]
    public async Task AModelThatRecoversOnRetryCompletesTheRun()
    {
        await SeedProfileAsync();
        var runId = await CreateRunAsync();
        var model = new ThrowingModel { FailFirst = 1 };

        await RunOnceAsync(model);
        _clock.Advance(TimeSpan.FromSeconds(_options.RetryBackoffSeconds + 1));
        await RunOnceAsync(model);

        (await LoadRunAsync(runId)).Status.Should().Be(CareerRunStatus.Completed);
        (await ProposalCountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task ARunWithoutAGoalWaitsForInputWithoutHoldingALease()
    {
        await SeedProfileAsync(withGoal: false);
        var runId = await CreateRunAsync();
        var model = new CountingModel();

        await RunOnceAsync(model);

        var run = await LoadRunAsync(runId);
        run.Status.Should().Be(CareerRunStatus.NeedsInput);
        (run.LeaseOwner, run.LeaseExpiresAt).Should().Be(((string?)null, (DateTime?)null));
        model.Calls.Should().Be(0);
        (await RunOnceAsync(model)).Should().BeFalse();
    }

    [Fact]
    public void TheFencingTokenAndAllowanceVersionAreConcurrencyTokens()
    {
        using var db = NewDb();

        db.Model.FindEntityType(typeof(CareerAgentRun))!.FindProperty(nameof(CareerAgentRun.FencingToken))!.IsConcurrencyToken.Should().BeTrue();
        db.Model.FindEntityType(typeof(CareerAllowance))!.FindProperty(nameof(CareerAllowance.Version))!.IsConcurrencyToken.Should().BeTrue();
    }

    [Fact]
    public async Task AStaleFencingTokenCannotSaveTheRun()
    {
        await SeedProfileAsync();
        var runId = await CreateRunAsync();
        await using var stale = NewDb();
        var run = await stale.CareerAgentRuns.SingleAsync(r => r.Id == runId);
        await using (var other = NewDb())
        {
            (await Runs(other).CancelAsync(Owner, runId)).Value!.Status.Should().Be("cancelled");
        }

        run.Status = CareerRunStatus.Completed;
        var act = () => stale.SaveChangesAsync();

        await act.Should().ThrowAsync<DbUpdateConcurrencyException>();
    }

    // ---- Private data ----------------------------------------------------------

    [Fact]
    public async Task DeletingAnOwnersCareerDataRemovesTheirRunsStepsAndAllowancesOnly()
    {
        await SeedProfileAsync();
        await SeedProfileAsync("other");
        await CreateRunAsync();
        await CreateRunAsync("other");
        await RunOnceAsync(new CountingModel());

        await using (var db = NewDb())
        {
            await new CareerPrivateDataService(db, Mock.Of<IStorageService>()).DeleteAllForOwnerAsync(Owner);
        }

        await using var check = NewDb();
        check.CareerAgentRuns.Where(r => r.OwnerId == Owner).Should().BeEmpty();
        check.CareerAgentSteps.Where(s => s.OwnerId == Owner).Should().BeEmpty();
        check.CareerAllowances.Where(a => a.OwnerId == Owner).Should().BeEmpty();
        check.CareerAgentRuns.Where(r => r.OwnerId == "other").Should().HaveCount(1);
        check.CareerAllowances.Where(a => a.OwnerId == "other").Should().HaveCount(1);
    }

    // ---- Test doubles ----------------------------------------------------------

    /// <summary>Runs a callback just before the first save, then saves; the callback may throw to fail the save.</summary>
    private sealed class RacingContext : ApplicationDbContext
    {
        private readonly Func<Task> _beforeFirstSave;
        private bool _raced;

        private readonly bool _atomic;
        private readonly DbContextOptions<ApplicationDbContext> _dbOptions;

        public RacingContext(DbContextOptions<ApplicationDbContext> options, Func<Task> beforeFirstSave, bool atomic = false) : base(options)
        {
            _dbOptions = options;
            _beforeFirstSave = beforeFirstSave;
            _atomic = atomic;
        }

        public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            if (!_raced)
            {
                _raced = true;
                await _beforeFirstSave();
                if (_atomic)
                {
                    await RejectWholeSaveIfAllowanceMovedAsync(cancellationToken);
                }
            }
            return await base.SaveChangesAsync(cancellationToken);
        }

        /// <summary>
        /// SQL Server rolls the whole save back when one row's token is stale; in-memory EF has no
        /// transaction and would keep the rows it already applied. Reject up front to match SQL.
        /// </summary>
        private async Task RejectWholeSaveIfAllowanceMovedAsync(CancellationToken ct)
        {
            foreach (var entry in ChangeTracker.Entries<CareerAllowance>().Where(e => e.State == EntityState.Modified))
            {
                var expected = entry.OriginalValues.GetValue<int>(nameof(CareerAllowance.Version));
                await using var other = new ApplicationDbContext(_dbOptions);
                var stored = await other.CareerAllowances.AsNoTracking().SingleAsync(a => a.Id == entry.Entity.Id, ct);
                if (stored.Version != expected)
                {
                    throw new DbUpdateConcurrencyException("Simulated stale allowance version; the whole save rolls back.");
                }
            }
        }
    }

    /// <summary>Throws right after a save that stored the named step, as a process dying after the commit would.</summary>
    private sealed class CrashAfterSaveContext : ApplicationDbContext
    {
        private readonly Func<string, bool> _crashOnStep;

        public CrashAfterSaveContext(DbContextOptions<ApplicationDbContext> options, Func<string, bool> crashOnStep) : base(options)
        {
            _crashOnStep = crashOnStep;
        }

        public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            var crash = ChangeTracker.Entries<CareerAgentStep>()
                .Any(e => e.State == EntityState.Added && _crashOnStep(e.Entity.Name));
            var saved = await base.SaveChangesAsync(cancellationToken);
            if (crash)
            {
                throw new InvalidOperationException("simulated crash");
            }
            return saved;
        }
    }

    /// <summary>Throws right after the claim is committed, so the worker "dies" holding a lease.</summary>
    private sealed class CrashAfterClaimContext : ApplicationDbContext
    {
        public CrashAfterClaimContext(DbContextOptions<ApplicationDbContext> options) : base(options)
        {
        }

        public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            var claiming = ChangeTracker.Entries<CareerAgentRun>()
                .Any(e => e.State == EntityState.Modified && e.Property(r => r.FencingToken).IsModified);
            var saved = await base.SaveChangesAsync(cancellationToken);
            if (claiming)
            {
                throw new InvalidOperationException("simulated crash");
            }
            return saved;
        }
    }

    private sealed class CountingModel : ICareerTextModel
    {
        private readonly FakeCareerTextModel _inner = new();
        public int Calls { get; private set; }

        public Task<CareerModelResult> CompleteAsync(CareerModelRequest request, CancellationToken ct = default)
        {
            Calls++;
            return _inner.CompleteAsync(request, ct);
        }
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

    /// <summary>Never answers on its own; returns only when the call is cancelled.</summary>
    private sealed class HangingModel : ICareerTextModel
    {
        public bool SawCancellation { get; private set; }

        public async Task<CareerModelResult> CompleteAsync(CareerModelRequest request, CancellationToken ct = default)
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(30), ct);
            }
            catch (OperationCanceledException)
            {
                SawCancellation = true;
                throw;
            }
            return CareerModelResult.Text("too late", usageTokens: 1, costCents: 0);
        }
    }

    private sealed class FatalModel : ICareerTextModel
    {
        public bool Retryable { get; init; }
        public int CostCents { get; init; }
        public int Calls { get; private set; }

        public Task<CareerModelResult> CompleteAsync(CareerModelRequest request, CancellationToken ct = default)
        {
            Calls++;
            throw new CareerModelException("invalid_api_key", Retryable, 401, CostCents);
        }
    }

    private sealed class ToolRequestingModel : ICareerTextModel
    {
        private readonly string _tool;
        public ToolRequestingModel(string tool) => _tool = tool;

        public Task<CareerModelResult> CompleteAsync(CareerModelRequest request, CancellationToken ct = default) =>
            Task.FromResult(CareerModelResult.Tool(_tool, usageTokens: 10, costCents: 1));
    }

    private sealed class ThrowingModel : ICareerTextModel
    {
        /// <summary>Fails this many calls, then answers; int.MaxValue means always fail.</summary>
        public int FailFirst { get; init; } = int.MaxValue;
        public int Calls { get; private set; }

        public Task<CareerModelResult> CompleteAsync(CareerModelRequest request, CancellationToken ct = default)
        {
            Calls++;
            if (Calls <= FailFirst)
            {
                throw new HttpRequestException("provider unavailable");
            }
            return new FakeCareerTextModel().CompleteAsync(request, ct);
        }
    }

    private sealed class CostlyModel : ICareerTextModel
    {
        private readonly int _cost;
        public CostlyModel(int cost) => _cost = cost;

        public Task<CareerModelResult> CompleteAsync(CareerModelRequest request, CancellationToken ct = default) =>
            Task.FromResult(CareerModelResult.Text("A costly summary.", usageTokens: 9000, costCents: _cost));
    }
}
