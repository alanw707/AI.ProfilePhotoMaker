using System.Text.Json;
using AI.ProfilePhotoMaker.API.Data;
using AI.ProfilePhotoMaker.API.Models.Career;
using AI.ProfilePhotoMaker.API.Services.Career;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AI.ProfilePhotoMaker.API.Tests.Integration.Career;

/// <summary>
/// The registry invariant of ADR 0020: a new private career entity cannot ship without purge and export coverage.
/// </summary>
public class CareerPrivacyCoverageTests : IClassFixture<CareerPrivacyFactory>
{
    /// <summary>Operator state (kill switches) has no owner and is not user data, so it is explicitly outside the private-data lists.</summary>
    public static readonly IReadOnlySet<Type> OperatorStateTypes = new HashSet<Type> { typeof(CareerOperatorState) };

    /// <summary>
    /// The only other Career* entities that are not covered private data. Both are audit records of the deletion
    /// itself: they must survive the purge they record (a tombstone that deleted itself could not block a
    /// restored backup), and the export lists them separately under auditRecords.
    /// </summary>
    public static readonly IReadOnlySet<Type> AuditRecordTypes = new HashSet<Type>
    {
        typeof(CareerDeletionRequest),
        typeof(CareerTombstone)
    };

    private readonly CareerPrivacyFactory _factory;

    public CareerPrivacyCoverageTests(CareerPrivacyFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public void EveryCareerOrResumeEntityInTheDbContextIsCoveredOrAnExplicitAuditRecord()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var contextEntities = typeof(ApplicationDbContext).GetProperties()
            .Where(p => p.PropertyType.IsGenericType && p.PropertyType.GetGenericTypeDefinition() == typeof(DbSet<>))
            .Select(p => p.PropertyType.GetGenericArguments()[0])
            .Where(t => t.Name.StartsWith("Career", StringComparison.Ordinal) || t == typeof(ResumeDocument))
            .ToList();
        var modelEntities = db.Model.GetEntityTypes().Select(t => t.ClrType)
            .Where(t => t.Name.StartsWith("Career", StringComparison.Ordinal) || t == typeof(ResumeDocument))
            .ToList();

        contextEntities.Should().NotBeEmpty();
        contextEntities.Concat(modelEntities).Distinct().Where(t => !AuditRecordTypes.Contains(t) && !OperatorStateTypes.Contains(t)).Should()
            .BeSubsetOf(CareerPrivateDataService.CoveredEntityTypes, "a new private career entity must be added to CoveredEntityTypes");
        CareerPrivateDataService.CoveredEntityTypes.Should().BeSubsetOf(contextEntities);
        AuditRecordTypes.Concat(OperatorStateTypes).Should().OnlyContain(t => !CareerPrivateDataService.CoveredEntityTypes.Contains(t));
        CareerPrivateDataService.CoveredEntityTypes.Should().Contain(typeof(CareerUsageEvent));
    }

    [Fact]
    public void EveryCoveredTypeHasAnOwnerAColumnToDateItByAndAPurgeStep()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        CareerPrivateDataService.PurgeOrder.Should().BeEquivalentTo(CareerPrivateDataService.CoveredEntityTypes);
        CareerPrivateDataService.PurgeOrder.Should().OnlyHaveUniqueItems();
        foreach (var type in CareerPrivateDataService.CoveredEntityTypes)
        {
            var entity = db.Model.FindEntityType(type)!;
            entity.FindProperty("OwnerId")!.ClrType.Should().Be(typeof(string), type.Name);
            if (type == typeof(CareerProfileProposalItem))
            {
                // Dated by its proposal: the only covered type with no timestamp of its own.
                CareerPrivateDataService.TimestampProperty(type).Should().BeNull();
                entity.FindProperty(nameof(CareerProfileProposalItem.ProposalId)).Should().NotBeNull();
                continue;
            }
            entity.FindProperty(CareerPrivateDataService.TimestampProperty(type)!)!.ClrType.Should().Be(typeof(DateTime), type.Name);
        }
    }

    [Fact]
    public async Task ExportHasASectionForEveryCoveredTypeWithItsSeededRow()
    {
        var owner = $"export-{Guid.NewGuid():N}";
        var other = $"other-{Guid.NewGuid():N}";
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            CareerPrivacySeed.SeedAll(db, owner, DateTime.UtcNow, _factory.Storage);
            CareerPrivacySeed.SeedAll(db, other, DateTime.UtcNow, _factory.Storage);
            await db.SaveChangesAsync();
        }

        using var exportScope = _factory.Services.CreateScope();
        var bytes = await exportScope.ServiceProvider.GetRequiredService<ICareerPrivacyExporter>().ExportAsync(owner);
        using var document = JsonDocument.Parse(bytes);
        var root = document.RootElement;

        root.GetProperty("format").GetString().Should().Be("career-export/v1");
        root.GetProperty("ownerId").GetString().Should().Be(owner);
        var sections = root.GetProperty("sections");
        sections.EnumerateObject().Select(p => p.Name).Should()
            .BeEquivalentTo(CareerPrivateDataService.CoveredEntityTypes.Select(t => t.Name));
        foreach (var type in CareerPrivateDataService.CoveredEntityTypes)
        {
            sections.GetProperty(type.Name).GetArrayLength().Should().Be(1, $"{type.Name} was seeded once for this owner");
        }

        var text = System.Text.Encoding.UTF8.GetString(bytes);
        text.Should().NotContain(other, "other owners' rows are never exported");
        text.Should().NotContain("storageKey").And.NotContain("career-private/resumes/", "blob keys are internal");
        sections.GetProperty(nameof(CareerExport))[0].TryGetProperty("content", out _).Should().BeFalse("blobs are metadata only");
        sections.GetProperty(nameof(CareerExport))[0].GetProperty("contentBytes").GetInt32().Should().Be(4);
        sections.GetProperty(nameof(ResumeDocument))[0].GetProperty("fileName").GetString().Should().Be("resume.pdf");
        root.GetProperty("auditRecords").EnumerateObject().Select(p => p.Name).Should()
            .BeEquivalentTo(nameof(CareerDeletionRequest), nameof(CareerTombstone));
        root.GetProperty("notes").EnumerateArray().Select(n => n.GetString()).Should().Contain(n => n!.Contains("No conversation history"));
    }
}
