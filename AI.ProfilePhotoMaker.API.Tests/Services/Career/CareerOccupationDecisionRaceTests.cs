using AI.ProfilePhotoMaker.API.Data;
using AI.ProfilePhotoMaker.API.Models.Career;
using AI.ProfilePhotoMaker.API.Services.Career;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace AI.ProfilePhotoMaker.API.Tests.Services.Career;

/// <summary>
/// A match is decided once (ADR 0010): a dismiss that read "proposed" must not overwrite a
/// confirm that committed meanwhile. The match status is a concurrency token, which in-memory
/// EF enforces; SQL Server enforces it in the UPDATE's WHERE clause.
/// </summary>
public class CareerOccupationDecisionRaceTests
{
    private const string Owner = "owner";

    private static DbContextOptions<ApplicationDbContext> Options(string name) =>
        new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(name).Options;

    [Fact]
    public void TheMatchStatusIsAConcurrencyToken()
    {
        using var db = new ApplicationDbContext(Options($"occ-token-{Guid.NewGuid():N}"));

        db.Model.FindEntityType(typeof(CareerOccupationMatch))!
            .FindProperty(nameof(CareerOccupationMatch.Status))!.IsConcurrencyToken.Should().BeTrue();
    }

    [Fact]
    public async Task ADismissThatLosesToAConfirmLeavesTheMatchConfirmed()
    {
        var name = $"occ-race-{Guid.NewGuid():N}";
        var id = Guid.NewGuid();
        await using (var seed = new ApplicationDbContext(Options(name)))
        {
            seed.CareerOccupationMatches.Add(new CareerOccupationMatch
            {
                Id = id, OwnerId = Owner, RunId = Guid.NewGuid(), ReferenceRelease = "30.0",
                MatcherVersion = "duty-overlap-2", Status = CareerMatchStatuses.Proposed,
                ResultJson = "{\"status\":\"candidates\",\"candidates\":[]}", CreatedAt = DateTime.UtcNow
            });
            await seed.SaveChangesAsync();
        }

        await using var racing = new ConfirmsFirstContext(Options(name), id);
        var service = new CareerOccupationService(racing, Mock.Of<IOccupationReference>(), TimeProvider.System);

        var outcome = await service.DismissAsync(Owner, id);

        outcome.ErrorCode.Should().Be(CareerOccupationErrorCodes.MatchNotConfirmable);
        await using var check = new ApplicationDbContext(Options(name));
        var stored = await check.CareerOccupationMatches.SingleAsync(m => m.Id == id);
        (stored.Status, stored.ConfirmedCode).Should().Be((CareerMatchStatuses.Confirmed, "15-1252.00"));
    }

    /// <summary>Lets a confirm commit just before this context's first save.</summary>
    private sealed class ConfirmsFirstContext : ApplicationDbContext
    {
        private readonly DbContextOptions<ApplicationDbContext> _options;
        private readonly Guid _id;
        private bool _raced;

        public ConfirmsFirstContext(DbContextOptions<ApplicationDbContext> options, Guid id) : base(options)
        {
            _options = options;
            _id = id;
        }

        public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            if (!_raced)
            {
                _raced = true;
                await using var other = new ApplicationDbContext(_options);
                var match = await other.CareerOccupationMatches.SingleAsync(m => m.Id == _id, cancellationToken);
                match.Status = CareerMatchStatuses.Confirmed;
                match.ConfirmedCode = "15-1252.00";
                match.ConfirmedIntoGoalVersion = 2;
                await other.SaveChangesAsync(cancellationToken);
            }
            return await base.SaveChangesAsync(cancellationToken);
        }
    }
}
