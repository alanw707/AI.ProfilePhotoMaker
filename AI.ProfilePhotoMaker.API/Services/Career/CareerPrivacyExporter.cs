using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using AI.ProfilePhotoMaker.API.Data;
using AI.ProfilePhotoMaker.API.Models.Career;
using Microsoft.EntityFrameworkCore;

namespace AI.ProfilePhotoMaker.API.Services.Career;

public interface ICareerPrivacyExporter
{
    /// <summary>The owner's career data as <c>career-export/v1</c> JSON bytes.</summary>
    Task<byte[]> ExportAsync(string ownerId, CancellationToken ct = default);
}

/// <summary>
/// Builds the <c>career-export/v1</c> document (ADR 0020): one section per entry of
/// <see cref="CareerPrivateDataService.CoveredEntityTypes"/>, owner-scoped, every stored column kept
/// (fact ids, pinned versions and sources are provenance). Binary columns are listed by size only and
/// the opaque storage key is left out. Deletion requests and tombstones are audit records, not covered
/// entities, so they are exported separately under <c>auditRecords</c>.
/// </summary>
public sealed class CareerPrivacyExporter : ICareerPrivacyExporter
{
    public const string Format = "career-export/v1";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private static readonly MethodInfo SectionMethod =
        typeof(CareerPrivacyExporter).GetMethod(nameof(SectionAsync), BindingFlags.NonPublic | BindingFlags.Instance)!;

    private readonly ApplicationDbContext _db;
    private readonly TimeProvider _clock;

    public CareerPrivacyExporter(ApplicationDbContext db, TimeProvider clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task<byte[]> ExportAsync(string ownerId, CancellationToken ct = default)
    {
        var sections = new SortedDictionary<string, List<Dictionary<string, object?>>>(StringComparer.Ordinal);
        foreach (var type in CareerPrivateDataService.CoveredEntityTypes)
        {
            sections[type.Name] = await SectionOf(type, ownerId, ct);
        }

        var audit = new SortedDictionary<string, List<Dictionary<string, object?>>>(StringComparer.Ordinal);
        foreach (var type in new[] { typeof(CareerDeletionRequest), typeof(CareerTombstone) })
        {
            audit[type.Name] = await SectionOf(type, ownerId, ct);
        }

        var document = new
        {
            format = Format,
            generatedAt = _clock.GetUtcNow().UtcDateTime,
            ownerId,
            sections,
            auditRecords = audit,
            notes = new[]
            {
                "No conversation history is stored: the assistant keeps run steps, not chat transcripts.",
                "Uploaded resume files and generated export files are listed by metadata only; their bytes are not embedded."
            }
        };
        return JsonSerializer.SerializeToUtf8Bytes(document, Json);
    }

    private Task<List<Dictionary<string, object?>>> SectionOf(Type type, string ownerId, CancellationToken ct) =>
        (Task<List<Dictionary<string, object?>>>)SectionMethod.MakeGenericMethod(type).Invoke(this, new object[] { ownerId, ct })!;

    private async Task<List<Dictionary<string, object?>>> SectionAsync<T>(string ownerId, CancellationToken ct) where T : class
    {
        var entityType = _db.Model.FindEntityType(typeof(T))!;
        var properties = entityType.GetProperties()
            .Where(p => p.PropertyInfo != null && p.Name != nameof(ResumeDocument.StorageKey))
            .ToList();

        var rows = await _db.Set<T>().AsNoTracking()
            .Where(e => EF.Property<string>(e, "OwnerId") == ownerId)
            .ToListAsync(ct);

        return rows.Select(row =>
        {
            var item = new Dictionary<string, object?>();
            foreach (var property in properties)
            {
                var value = property.PropertyInfo!.GetValue(row);
                var name = JsonNamingPolicy.CamelCase.ConvertName(property.Name);
                if (value is byte[] bytes)
                {
                    item[name + "Bytes"] = bytes.Length;
                }
                else
                {
                    item[name] = value;
                }
            }
            return item;
        }).ToList();
    }
}
