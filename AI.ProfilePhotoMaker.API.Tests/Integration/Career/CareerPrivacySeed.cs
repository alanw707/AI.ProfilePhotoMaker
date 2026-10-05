using System.Reflection;
using AI.ProfilePhotoMaker.API.Data;
using AI.ProfilePhotoMaker.API.Models.Career;
using AI.ProfilePhotoMaker.API.Services.Career;
using Microsoft.EntityFrameworkCore;

namespace AI.ProfilePhotoMaker.API.Tests.Integration.Career;

/// <summary>
/// Seeds one row for every covered entity type, so a type added to <c>CoveredEntityTypes</c> is
/// automatically exercised by the purge, replay and export tests.
/// </summary>
public static class CareerPrivacySeed
{
    public const string Excerpt = "SECRET SOURCE EXCERPT";

    public static string BlobKey(Guid id) => $"career-private/resumes/{id}";

    /// <summary>Adds (does not save) one row per covered type for the owner, stamped <paramref name="at"/>.</summary>
    public static List<object> SeedAll(ApplicationDbContext db, string owner, DateTime at, InMemoryStorage? storage = null)
    {
        var rows = new List<object>();
        foreach (var type in CareerPrivateDataService.CoveredEntityTypes)
        {
            var row = Activator.CreateInstance(type)!;
            var id = Guid.NewGuid();
            type.GetProperty("Id")?.SetValue(row, id);
            type.GetProperty("OwnerId")!.SetValue(row, owner);
            if (CareerPrivateDataService.TimestampProperty(type) is { } timestamp)
            {
                type.GetProperty(timestamp)!.SetValue(row, at);
            }

            switch (row)
            {
                case ResumeDocument resume:
                    resume.StorageKey = BlobKey(id);
                    resume.FileName = "resume.pdf";
                    storage?.SaveImageToPathAsync(new MemoryStream(new byte[] { 1, 2, 3 }), resume.StorageKey).GetAwaiter().GetResult();
                    break;
                case CareerExport export:
                    export.Content = new byte[] { 9, 9, 9, 9 };
                    export.FileName = "resume.pdf";
                    break;
                case CareerProfileProposalItem item:
                    item.Excerpt = Excerpt;
                    break;
            }
            db.Add(row);
            rows.Add(row);
        }

        // The excerpt purge follows the item to its proposal, so link the two seeded rows.
        var proposal = rows.OfType<CareerProfileProposal>().Single();
        rows.OfType<CareerProfileProposalItem>().Single().ProposalId = proposal.Id;
        return rows;
    }

    public static async Task<int> CountAsync(ApplicationDbContext db, Type type, string owner) =>
        await (Task<int>)typeof(CareerPrivacySeed).GetMethod(nameof(CountOf), BindingFlags.NonPublic | BindingFlags.Static)!
            .MakeGenericMethod(type).Invoke(null, new object[] { db, owner })!;

    private static Task<int> CountOf<T>(ApplicationDbContext db, string owner) where T : class =>
        db.Set<T>().CountAsync(e => EF.Property<string>(e, "OwnerId") == owner);

    /// <summary>Total covered rows the owner still has.</summary>
    public static async Task<int> TotalAsync(ApplicationDbContext db, string owner)
    {
        var total = 0;
        foreach (var type in CareerPrivateDataService.CoveredEntityTypes)
        {
            total += await CountAsync(db, type, owner);
        }
        return total;
    }
}
