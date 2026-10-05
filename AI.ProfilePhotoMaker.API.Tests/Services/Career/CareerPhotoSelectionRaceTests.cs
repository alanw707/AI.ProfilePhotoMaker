using AI.ProfilePhotoMaker.API.Data;
using AI.ProfilePhotoMaker.API.Models;
using AI.ProfilePhotoMaker.API.Models.Career;
using AI.ProfilePhotoMaker.API.Services;
using AI.ProfilePhotoMaker.API.Services.Career;
using AI.ProfilePhotoMaker.API.Services.Storage;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace AI.ProfilePhotoMaker.API.Tests.Services.Career;

/// <summary>
/// Two first-time choices from the same owner race on the unique owner index. The loser
/// must not surface a 500: choosing a photo is idempotent, so it applies its choice to
/// the row the winner created. (In-memory EF does not enforce unique indexes, so the
/// lost race is simulated the way SQL Server reports it to the service.)
/// </summary>
public class CareerPhotoSelectionRaceTests
{
    private const string Owner = "owner";

    private static DbContextOptions<ApplicationDbContext> Options(string name) =>
        new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(name).Options;

    private static CareerPhotoService Service(ApplicationDbContext db) =>
        new(db, Mock.Of<IStorageService>(), Mock.Of<IOutcomePackageService>(), TimeProvider.System);

    [Fact]
    public async Task LosingTheFirstChoiceRaceUpdatesTheWinnersRow()
    {
        var name = $"career-photo-race-{Guid.NewGuid():N}";
        int mine, theirs;
        await using (var seed = new ApplicationDbContext(Options(name)))
        {
            var profile = new UserProfile { UserId = Owner };
            seed.UserProfiles.Add(profile);
            await seed.SaveChangesAsync();
            ProcessedImage Delivered(string url) => new()
            {
                UserProfileId = profile.Id, ProcessedImageUrl = url, Style = "linkedin",
                IsGenerated = true, GenerationStatus = "succeeded"
            };
            var a = Delivered("generated/owner/a.png");
            var b = Delivered("generated/owner/b.png");
            seed.ProcessedImages.AddRange(a, b);
            await seed.SaveChangesAsync();
            (mine, theirs) = (a.Id, b.Id);
        }

        await using var racing = new LosesFirstInsertContext(Options(name), name, theirs);

        var outcome = await Service(racing).SelectAsync(Owner, mine);

        outcome.Kind.Should().Be(CareerOutcomeKind.Ok);
        outcome.Value!.SelectedPhotoId.Should().Be(mine);
        await using var check = new ApplicationDbContext(Options(name));
        var rows = await check.CareerPhotoSelections.Where(s => s.OwnerId == Owner).ToListAsync();
        rows.Should().ContainSingle().Which.ProcessedImageId.Should().Be(mine);
    }

    /// <summary>
    /// On its first save, lets another request insert this owner's selection, then fails
    /// the way a unique-index violation is classified (a lost race).
    /// </summary>
    private sealed class LosesFirstInsertContext : ApplicationDbContext
    {
        private readonly string _name;
        private readonly int _winnerImageId;
        private bool _lost;

        public LosesFirstInsertContext(DbContextOptions<ApplicationDbContext> options, string name, int winnerImageId)
            : base(options)
        {
            _name = name;
            _winnerImageId = winnerImageId;
        }

        public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            if (!_lost)
            {
                _lost = true;
                await using var other = new ApplicationDbContext(Options(_name));
                other.CareerPhotoSelections.Add(new CareerPhotoSelection
                {
                    Id = Guid.NewGuid(), OwnerId = Owner, ProcessedImageId = _winnerImageId,
                    SelectedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
                });
                await other.SaveChangesAsync(cancellationToken);
                throw new DbUpdateConcurrencyException("Simulated unique owner index violation.");
            }
            return await base.SaveChangesAsync(cancellationToken);
        }
    }
}
