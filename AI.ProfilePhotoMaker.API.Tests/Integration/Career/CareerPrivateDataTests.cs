using AI.ProfilePhotoMaker.API.Data;
using AI.ProfilePhotoMaker.API.Migrations;
using AI.ProfilePhotoMaker.API.Models.Career;
using AI.ProfilePhotoMaker.API.Services.Career;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AI.ProfilePhotoMaker.API.Tests.Integration.Career;

/// <summary>
/// Every private career entity participates in owner deletion from first storage
/// (#378 acceptance criterion; wired to user controls in #392).
/// </summary>
public class CareerPrivateDataTests : IClassFixture<CareerWorkspaceEnabledFactory>
{
    private readonly CareerWorkspaceEnabledFactory _factory;

    public CareerPrivateDataTests(CareerWorkspaceEnabledFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public void DeletionCoversEveryCareerEntityInTheModel()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var careerEntities = db.Model.GetEntityTypes()
            .Select(t => t.ClrType)
            .Where(t => t.Namespace == typeof(CareerProfile).Namespace)
            .ToHashSet();

        careerEntities.Should().NotBeEmpty();
        CareerPrivateDataService.CoveredEntityTypes.Should().BeEquivalentTo(careerEntities,
            "a new private career entity must be added to owner deletion");
    }

    [Fact]
    public async Task DeleteAllForOwnerRemovesOnlyThatOwnersCareerData()
    {
        var alice = new CareerClient(_factory);
        var bob = new CareerClient(_factory);
        foreach (var user in new[] { alice, bob })
        {
            await user.CreateProfileAsync();
            await CareerClient.ReadDataAsync(await user.PutProfileAsync(CareerClient.ValidProfile("Analyst II"), "\"profile-v1\""), 200);
            await user.CreateGoalAsync();
        }

        using (var scope = _factory.Services.CreateScope())
        {
            var service = scope.ServiceProvider.GetRequiredService<ICareerPrivateDataService>();
            await service.DeleteAllForOwnerAsync(alice.UserId);
        }

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            (await db.CareerProfiles.CountAsync(p => p.OwnerId == alice.UserId)).Should().Be(0);
            (await db.CareerProfileVersions.CountAsync(v => v.OwnerId == alice.UserId)).Should().Be(0);
            (await db.CareerGoals.CountAsync(g => g.OwnerId == alice.UserId)).Should().Be(0);
            (await db.CareerGoalVersions.CountAsync(v => v.OwnerId == alice.UserId)).Should().Be(0);

            (await db.CareerProfileVersions.CountAsync(v => v.OwnerId == bob.UserId)).Should().Be(2);
            (await db.CareerGoalVersions.CountAsync(v => v.OwnerId == bob.UserId)).Should().Be(1);
        }

        await CareerClient.ReadErrorAsync(await alice.GetAsync("/api/career/profile"), 404);
        await CareerClient.ReadDataAsync(await bob.GetAsync("/api/career/profile"), 200);
    }

    private static readonly string[] CareerTables =
    {
        "CareerProfiles", "CareerProfileVersions", "CareerGoals", "CareerGoalVersions",
        "CareerResumeDocuments", "CareerProfileProposals", "CareerProfileProposalItems", "CareerPhotoSelections",
        "CareerAgentRuns", "CareerAgentSteps", "CareerAllowances", "CareerOccupationMatches", "CareerMarketBriefs", "CareerPayAnalyses"
    };

    [Fact]
    public void CareerMigrationIsAdditiveOnly()
    {
        AssertAdditive(new AddCareerProfileAndGoal().UpOperations, allowAddColumn: false);
    }

    [Fact]
    public void ResumeImportMigrationIsAdditiveOnly()
    {
        var operations = new AddCareerResumeImport().UpOperations;

        AssertAdditive(operations, allowAddColumn: true);
        operations.OfType<CreateTableOperation>().Select(o => o.Name).Should()
            .Contain(new[] { "CareerResumeDocuments", "CareerProfileProposals", "CareerProfileProposalItems" });
    }

    [Fact]
    public void PhotoSelectionMigrationIsAdditiveOnly()
    {
        var operations = new AddCareerPhotoSelection().UpOperations;

        AssertAdditive(operations, allowAddColumn: false);
        operations.OfType<CreateTableOperation>().Select(o => o.Name).Should().Equal("CareerPhotoSelections");
        operations.OfType<CreateIndexOperation>().Should().OnlyContain(i => i.Table == "CareerPhotoSelections");
    }

    [Fact]
    public void AgentRuntimeMigrationIsAdditiveOnly()
    {
        var operations = new AddCareerAgentRuntime().UpOperations;

        AssertAdditive(operations, allowAddColumn: false);
        operations.OfType<CreateTableOperation>().Select(o => o.Name).Should()
            .BeEquivalentTo(new[] { "CareerAgentRuns", "CareerAgentSteps", "CareerAllowances" });
        operations.OfType<CreateIndexOperation>().Should()
            .Contain(i => i.Table == "CareerAgentRuns" && i.IsUnique && i.Columns.SequenceEqual(new[] { "OwnerId", "IdempotencyKey" }));
        operations.OfType<CreateIndexOperation>().Should()
            .Contain(i => i.Table == "CareerAllowances" && i.IsUnique && i.Columns.SequenceEqual(new[] { "OwnerId", "PeriodStart" }));
    }

    [Fact]
    public void OccupationMatchMigrationOnlyAddsATableAndNullableGoalVersionColumns()
    {
        var operations = new AddCareerOccupationMatches().UpOperations;

        AssertAdditive(operations, allowAddColumn: true);
        operations.OfType<CreateTableOperation>().Select(o => o.Name).Should().Equal("CareerOccupationMatches");
        var added = operations.OfType<AddColumnOperation>().ToList();
        added.Should().OnlyContain(c => c.Table == "CareerGoalVersions" && c.IsNullable);
        added.Select(c => c.Name).Should().BeEquivalentTo(
            "OccupationCode", "OccupationTitle", "OccupationReferenceRelease", "OccupationMatchId");
        operations.OfType<CreateIndexOperation>().Should()
            .Contain(i => i.Table == "CareerOccupationMatches" && i.IsUnique && i.Columns.SequenceEqual(new[] { "RunId" }));
    }

    [Fact]
    public void MarketBriefMigrationOnlyAddsATableAndItsIndexes()
    {
        var operations = new AddCareerMarketBriefs().UpOperations;

        AssertAdditive(operations, allowAddColumn: false);
        operations.OfType<CreateTableOperation>().Select(o => o.Name).Should().Equal("CareerMarketBriefs");
        operations.OfType<CreateIndexOperation>().Should()
            .Contain(i => i.Table == "CareerMarketBriefs" && i.IsUnique && i.Columns.SequenceEqual(new[] { "RunId" }));
    }

    [Fact]
    public void PayAnalysisMigrationOnlyAddsATableAndItsIndexes()
    {
        var operations = new AddCareerPayAnalyses().UpOperations;
        AssertAdditive(operations, allowAddColumn: false);
        operations.OfType<CreateTableOperation>().Select(o => o.Name).Should().Equal("CareerPayAnalyses");
        operations.OfType<CreateIndexOperation>().Should()
            .Contain(i => i.Table == "CareerPayAnalyses" && i.IsUnique && i.Columns.SequenceEqual(new[] { "RunId" }));
    }

    [Fact]
    public void ThePaySourceMigrationOnlyAddsOneNullableColumn()
    {
        var operations = new AddCareerPayAnalysisPaySource().UpOperations;
        AssertAdditive(operations, allowAddColumn: true);
        var added = operations.OfType<AddColumnOperation>().Should().ContainSingle().Subject;
        (added.Table, added.Name, added.IsNullable).Should().Be(("CareerPayAnalyses", "RequestedPaySource", true));
        operations.OfType<DropTableOperation>().Should().BeEmpty();
        operations.OfType<DropColumnOperation>().Should().BeEmpty();
    }

    [Fact]
    public void ThePreferredAreaMigrationOnlyAddsThreeNullableGoalVersionColumns()
    {
        var operations = new AddCareerPreferredArea().UpOperations;

        AssertAdditive(operations, allowAddColumn: true);
        var added = operations.OfType<AddColumnOperation>().ToList();
        added.Should().OnlyContain(c => c.Table == "CareerGoalVersions" && c.IsNullable);
        added.Select(c => c.Name).Should().BeEquivalentTo("PreferredAreaCode", "PreferredAreaTitle", "PreferredAreaLevel");
        operations.Should().HaveCount(3);
    }

    private static void AssertAdditive(IReadOnlyList<MigrationOperation> operations, bool allowAddColumn)
    {
        operations.Should().NotBeEmpty();
        foreach (var operation in operations)
        {
            switch (operation)
            {
                case CreateTableOperation create:
                    CareerTables.Should().Contain(create.Name);
                    break;
                case CreateIndexOperation index:
                    CareerTables.Should().Contain(index.Table);
                    break;
                case AddColumnOperation add when allowAddColumn:
                    CareerTables.Should().Contain(add.Table);
                    break;
                default:
                    throw new Xunit.Sdk.XunitException($"Career migration must only add career tables, indexes and columns, found {operation.GetType().Name}");
            }
        }
    }
}
