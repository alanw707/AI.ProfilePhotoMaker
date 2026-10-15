using System.Net;
using AI.ProfilePhotoMaker.API.Data;
using AI.ProfilePhotoMaker.API.Models;
using AI.ProfilePhotoMaker.API.Models.Career;
using AI.ProfilePhotoMaker.API.Services.Career;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AI.ProfilePhotoMaker.API.Tests.Integration.Career;

/// <summary>
/// Funnel math against SQLite, so the queries are proven to translate to SQL (the in-memory provider would accept
/// queries a relational database rejects). Foreign keys are off so each test seeds only the rows the report reads.
/// </summary>
public sealed class CareerFunnelReportTests : IDisposable
{
    private static readonly DateTime From = new(2019, 3, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime To = new(2019, 4, 1, 0, 0, 0, DateTimeKind.Utc);
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"career-funnel-{Guid.NewGuid():N}.db");

    private DbContextOptions<ApplicationDbContext> Options =>
        new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite($"Data Source={_path};Foreign Keys=False").Options;

    public void Dispose()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        File.Delete(_path);
    }

    private static int _payment;

    private static DateTime Day(int month, int day) => new(2019, month, day, 12, 0, 0, DateTimeKind.Utc);

    private static void Profile(ApplicationDbContext db, string owner, DateTime at)
    {
        var id = Guid.NewGuid();
        db.CareerProfiles.Add(new CareerProfile { Id = id, OwnerId = owner, ActiveVersionNumber = 1, CreatedAt = at, UpdatedAt = at });
        db.CareerProfileVersions.Add(new CareerProfileVersion { Id = Guid.NewGuid(), CareerProfileId = id, OwnerId = owner, VersionNumber = 1, CreatedAt = at });
    }

    private static void Goal(ApplicationDbContext db, string owner, DateTime at, string? occupation)
    {
        var id = Guid.NewGuid();
        db.CareerGoals.Add(new CareerGoal { Id = id, OwnerId = owner, ActiveVersionNumber = 1, CreatedAt = at, UpdatedAt = at });
        db.CareerGoalVersions.Add(new CareerGoalVersion { Id = Guid.NewGuid(), CareerGoalId = id, OwnerId = owner, VersionNumber = 1, OccupationCode = occupation, CreatedAt = at });
    }

    private static void Everything(ApplicationDbContext db, string owner, DateTime at, DateTime later)
    {
        Profile(db, owner, at);
        Goal(db, owner, at, "15-1252.00");
        db.CareerMarketBriefs.Add(new CareerMarketBrief { Id = Guid.NewGuid(), RunId = Guid.NewGuid(), OwnerId = owner, CreatedAt = at });
        db.CareerPayAnalyses.Add(new CareerPayAnalysis { Id = Guid.NewGuid(), RunId = Guid.NewGuid(), OwnerId = owner, CreatedAt = at });
        db.CareerRoadmaps.Add(new CareerRoadmap { Id = Guid.NewGuid(), OwnerId = owner, Status = CareerRoadmapStatuses.Accepted, CreatedAt = at });
        var material = Guid.NewGuid();
        db.CareerMaterials.Add(new CareerMaterial { Id = material, OwnerId = owner, Kind = CareerMaterialKinds.Resume, CreatedAt = at, UpdatedAt = later });
        db.CareerMaterialVersions.Add(new CareerMaterialVersion { Id = Guid.NewGuid(), MaterialId = material, OwnerId = owner, Number = 1, CreatedAt = later });
        db.CareerUsageEvents.Add(new CareerUsageEvent { Id = Guid.NewGuid(), OwnerId = owner, Action = CareerUsageActions.Export, Outcome = CareerUsageOutcomes.Ok, CreatedAt = later });
        db.CareerPhotoSelections.Add(new CareerPhotoSelection { Id = Guid.NewGuid(), OwnerId = owner, ProcessedImageId = 1, SelectedAt = later, UpdatedAt = later });
        Purchase(db, owner, later, PackageEntitlementStatus.Active);
    }

    private static void Purchase(ApplicationDbContext db, string owner, DateTime at, PackageEntitlementStatus status) =>
        db.UserPackageEntitlements.Add(new UserPackageEntitlement
        {
            UserId = owner, OutcomePackageDefinitionId = 1, SourcePaymentTransactionId = Interlocked.Increment(ref _payment), Status = status, CreatedAt = at, UpdatedAt = at
        });

    private async Task<CareerFunnelReportDto> ReportAsync(Action<ApplicationDbContext> seed)
    {
        await using (var db = new ApplicationDbContext(Options))
        {
            await db.Database.EnsureCreatedAsync();
            seed(db);
            await db.SaveChangesAsync();
        }
        await using var read = new ApplicationDbContext(Options);
        return await new CareerFunnelReportService(read, TimeProvider.System).BuildAsync(From, To);
    }

    private static Dictionary<string, int> Steps(CareerFunnelReportDto r) => r.Steps.ToDictionary(s => s.Step, s => s.Users);

    [Fact]
    public async Task CountsEachStepForPeopleWhoStartedInTheWindowOnly()
    {
        var report = await ReportAsync(db =>
        {
            Everything(db, "a", Day(3, 2), Day(3, 20));
            // b: goal without an occupation, a proposed roadmap, a failed export, purchases that must not count.
            Profile(db, "b", Day(3, 5));
            Goal(db, "b", Day(3, 5), occupation: null);
            db.CareerRoadmaps.Add(new CareerRoadmap { Id = Guid.NewGuid(), OwnerId = "b", Status = CareerRoadmapStatuses.Proposed, CreatedAt = Day(3, 5) });
            db.CareerUsageEvents.Add(new CareerUsageEvent { Id = Guid.NewGuid(), OwnerId = "b", Action = CareerUsageActions.Export, Outcome = CareerUsageOutcomes.Failed, CreatedAt = Day(3, 5) });
            Purchase(db, "b", Day(3, 1), PackageEntitlementStatus.Active); // before starting career
            Purchase(db, "b", Day(3, 6), PackageEntitlementStatus.Refunded);
            // c started before the window: none of its steps count.
            Everything(db, "c", Day(2, 20), Day(3, 20));
            // d: started only, then came back the next day for one draft.
            Profile(db, "d", Day(3, 10));
            db.CareerAgentRuns.Add(new CareerAgentRun { Id = Guid.NewGuid(), OwnerId = "d", Task = "profile_summary", IdempotencyKey = "k", CreatedAt = Day(3, 11), UpdatedAt = Day(3, 11) });
        });

        report.Started.Should().Be(3);
        report.Steps.Select(s => s.Step).Should().Equal(CareerFunnelReportService.StepOrder);
        Steps(report).Should().BeEquivalentTo(new Dictionary<string, int>
        {
            ["profile_saved"] = 3, ["goal_saved"] = 2, ["occupation_confirmed"] = 1, ["market_brief"] = 1, ["pay_analysis"] = 1,
            ["roadmap_built"] = 2, ["roadmap_accepted"] = 1, ["resume_drafted"] = 1, ["material_exported"] = 1,
            ["photo_selected"] = 1, ["photo_package_bought"] = 1,
        });
        report.Returns.Should().Be(new CareerFunnelReturnsDto(ActiveOnTwoOrMoreDays: 2, ActiveSevenOrMoreDaysAfterStart: 1, ActiveDaysP50: 2, ActiveDaysMax: 2));
    }

    [Fact]
    public async Task AnEmptyWindowReportsZeroesInJourneyOrder()
    {
        var report = await ReportAsync(db => Everything(db, "outside", Day(5, 1), Day(5, 9)));

        report.Started.Should().Be(0);
        report.Steps.Should().OnlyContain(s => s.Users == 0).And.HaveCount(CareerFunnelReportService.StepOrder.Count);
        report.Returns.Should().Be(new CareerFunnelReturnsDto(0, 0, 0, 0));
    }
}

public class CareerAdminFunnelEndpointTests : IClassFixture<CareerUsageFactory>
{
    private readonly CareerUsageFactory _f;

    public CareerAdminFunnelEndpointTests(CareerUsageFactory f) => _f = f;

    [Fact]
    public async Task NonAdminsGet403()
    {
        var user = await _f.UserAsync();
        (await user.SendAsync(HttpMethod.Get, "/api/admin/career/funnel", null)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task AdminGetsCountsOnlyAndAReversedWindowIs400()
    {
        var user = await _f.UserAsync();
        var admin = _f.Admin();

        var report = await CareerClient.ReadDataAsync(await admin.GetAsync("/api/admin/career/funnel"), 200);

        report.GetProperty("started").GetInt32().Should().BeGreaterThanOrEqualTo(1);
        report.GetProperty("steps")[0].GetProperty("step").GetString().Should().Be("profile_saved");
        report.GetProperty("returns").GetProperty("activeOnTwoOrMoreDays").ValueKind.Should().Be(System.Text.Json.JsonValueKind.Number);
        report.GetRawText().Should().NotContain(user.UserId, "the funnel reports counts, never owners");
        (await admin.GetAsync("/api/admin/career/funnel?from=2020-02-01&to=2020-01-01")).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
