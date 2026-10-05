using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace AI.ProfilePhotoMaker.API.Services.Career;

/// <summary>Where the O*NET snapshot comes from, shown wherever matches appear (CC BY 4.0 attribution).</summary>
public sealed record OccupationSource(
    string Name,
    string Release,
    string ReleaseDate,
    string Taxonomy,
    string License,
    string LicenseUrl,
    string Url,
    string Attribution);

public sealed record OccupationTask(int Id, string Text, IReadOnlyList<string> Stems);

/// <summary>An occupation with its text pre-normalised once, so matching never re-tokenises the snapshot.</summary>
public sealed class OccupationRecord
{
    public required string Code { get; init; }
    public required string Title { get; init; }
    public required string Description { get; init; }
    public required IReadOnlyList<OccupationTask> Tasks { get; init; }
    public required IReadOnlyList<string> Skills { get; init; }
    public required IReadOnlyList<string> Technologies { get; init; }
    public required IReadOnlyList<string> Titles { get; init; }

    /// <summary>SOC major group: the first two digits of the code.</summary>
    public string MajorGroup => Code[..2];

    /// <summary>Normalised skill and technology names mapped to their reference kind; skills win over technologies.</summary>
    public required IReadOnlyDictionary<string, (string Kind, string Text)> SkillIndex { get; init; }

    /// <summary>Stem sequences of the title and its reported/alternate titles, for the title bonus.</summary>
    public required IReadOnlyList<string> TitleKeys { get; init; }
}

/// <summary>A verified, indexed snapshot. IDF is computed over every core task of every occupation.</summary>
public sealed class OccupationReferenceData
{
    private readonly Dictionary<string, OccupationRecord> _byCode;

    public OccupationReferenceData(OccupationSource source, IReadOnlyList<OccupationRecord> occupations, IReadOnlyDictionary<string, double> idf)
    {
        Source = source;
        Occupations = occupations;
        Idf = idf;
        _byCode = occupations.ToDictionary(o => o.Code, StringComparer.Ordinal);
    }

    public OccupationSource Source { get; }
    public IReadOnlyList<OccupationRecord> Occupations { get; }
    public IReadOnlyDictionary<string, double> Idf { get; }

    public OccupationRecord? Find(string code) => _byCode.GetValueOrDefault(code);

    /// <summary>A stem the snapshot never uses is as informative as the rarest one.</summary>
    public double IdfOf(string stem) => Idf.TryGetValue(stem, out var value) ? value : MaxIdf;

    public double MaxIdf { get; init; }
}

public interface IOccupationReference
{
    /// <summary>The verified snapshot, or null when it is missing, altered or malformed (fail closed).</summary>
    OccupationReferenceData? Data { get; }
}

/// <summary>
/// Loads the embedded snapshot once, on first use. The bytes must hash to
/// <see cref="ExpectedSha256"/> and every code must be a valid O*NET-SOC code, otherwise
/// the reference is unavailable and nothing is matched against a doubtful source.
/// </summary>
public sealed class EmbeddedOccupationReference : IOccupationReference
{
    /// <summary>SHA-256 of Data/Reference/onet-snapshot.json.gz (O*NET 30.0, ADR 0010).</summary>
    public const string ExpectedSha256 = "f5a07953095e635e76a99ddaf9574bab8b351fec409dc022eb3cda68f19880fa";

    private const string ResourceName = "onet-snapshot.json.gz";
    private static readonly Regex CodePattern = new(@"^\d{2}-\d{4}\.\d{2}$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private readonly Lazy<OccupationReferenceData?> _data;

    public EmbeddedOccupationReference() : this(OpenEmbeddedSnapshot)
    {
    }

    /// <summary>Injected stream and hash so tests can cover tampered and malformed snapshots.</summary>
    public EmbeddedOccupationReference(Func<Stream> open, string expectedSha256 = ExpectedSha256, ILogger? logger = null)
    {
        _data = new Lazy<OccupationReferenceData?>(() => Load(open, expectedSha256, logger), LazyThreadSafetyMode.ExecutionAndPublication);
    }

    public OccupationReferenceData? Data => _data.Value;

    public static Stream OpenEmbeddedSnapshot() =>
        typeof(EmbeddedOccupationReference).Assembly.GetManifestResourceStream(ResourceName)
        ?? throw new FileNotFoundException("The occupation snapshot is not embedded.");

    private static OccupationReferenceData? Load(Func<Stream> open, string expectedSha256, ILogger? logger)
    {
        try
        {
            byte[] bytes;
            using (var stream = open())
            using (var buffer = new MemoryStream())
            {
                stream.CopyTo(buffer);
                bytes = buffer.ToArray();
            }

            var actual = Convert.ToHexString(SHA256.HashData(bytes));
            if (!string.Equals(actual, expectedSha256, StringComparison.OrdinalIgnoreCase))
            {
                logger?.LogError("Occupation snapshot hash mismatch; occupation matching is unavailable");
                return null;
            }

            using var gzip = new GZipStream(new MemoryStream(bytes), CompressionMode.Decompress);
            var snapshot = JsonSerializer.Deserialize<SnapshotFile>(gzip, JsonOptions);
            return snapshot == null ? null : Build(snapshot);
        }
        catch (Exception ex) when (ex is JsonException or InvalidDataException or IOException or InvalidOperationException or FormatException)
        {
            // Type only: the failure is about the snapshot, not user data, but keep the log terse.
            logger?.LogError("Occupation snapshot could not be loaded: {ExceptionType}", ex.GetType().Name);
            return null;
        }
    }

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private static OccupationReferenceData? Build(SnapshotFile file)
    {
        var s = file.Source;
        if (s == null || file.Occupations == null || file.Occupations.Count == 0
            || string.IsNullOrWhiteSpace(s.Name) || string.IsNullOrWhiteSpace(s.Release))
        {
            return null;
        }

        var records = new List<OccupationRecord>();
        foreach (var o in file.Occupations)
        {
            if (o.Code == null || !CodePattern.IsMatch(o.Code) || string.IsNullOrWhiteSpace(o.Title))
            {
                return null;
            }
            records.Add(ToRecord(o));
        }
        if (records.Select(r => r.Code).Distinct().Count() != records.Count)
        {
            return null;
        }

        // Document frequency per stem across all tasks: rare stems say more about the work.
        var documentFrequency = new Dictionary<string, int>(StringComparer.Ordinal);
        var taskCount = 0;
        foreach (var task in records.SelectMany(r => r.Tasks))
        {
            taskCount++;
            foreach (var stem in task.Stems.Distinct())
            {
                documentFrequency[stem] = documentFrequency.GetValueOrDefault(stem) + 1;
            }
        }
        var idf = documentFrequency.ToDictionary(kv => kv.Key, kv => Math.Log((taskCount + 1.0) / (kv.Value + 0.5)), StringComparer.Ordinal);

        var source = new OccupationSource(s.Name, s.Release, s.ReleaseDate ?? string.Empty, s.Taxonomy ?? string.Empty,
            s.License ?? string.Empty, s.LicenseUrl ?? string.Empty, s.Url ?? string.Empty, s.Attribution ?? string.Empty);
        return new OccupationReferenceData(source, records, idf) { MaxIdf = Math.Log((taskCount + 1.0) / 0.5) };
    }

    private static OccupationRecord ToRecord(SnapshotOccupation o)
    {
        var skills = o.Skills ?? new List<string>();
        var technologies = o.Technologies ?? new List<string>();
        var index = new Dictionary<string, (string Kind, string Text)>(StringComparer.Ordinal);
        foreach (var name in skills)
        {
            index.TryAdd(OccupationText.NormalizeSkill(name), ("skill", name));
        }
        foreach (var name in technologies)
        {
            index.TryAdd(OccupationText.NormalizeSkill(name), ("technology", name));
        }

        var titles = (o.Titles ?? new List<string>()).Prepend(o.Title!).ToList();
        return new OccupationRecord
        {
            Code = o.Code!,
            Title = o.Title!,
            Description = o.Description ?? string.Empty,
            Tasks = (o.Tasks ?? new List<SnapshotTask>())
                .Select(t => new OccupationTask(t.Id, t.Text ?? string.Empty, OccupationText.Stems(t.Text ?? string.Empty)))
                .ToList(),
            Skills = skills,
            Technologies = technologies,
            Titles = o.Titles ?? new List<string>(),
            SkillIndex = index,
            TitleKeys = titles.Select(t => string.Join(' ', OccupationText.Stems(t))).Where(k => k.Length > 0).Distinct().ToList()
        };
    }

    private sealed class SnapshotFile
    {
        public SnapshotSource? Source { get; set; }
        public List<SnapshotOccupation>? Occupations { get; set; }
    }

    private sealed class SnapshotSource
    {
        public string? Name { get; set; }
        public string? Release { get; set; }
        public string? ReleaseDate { get; set; }
        public string? Taxonomy { get; set; }
        public string? License { get; set; }
        public string? LicenseUrl { get; set; }
        public string? Url { get; set; }
        public string? Attribution { get; set; }
    }

    private sealed class SnapshotOccupation
    {
        public string? Code { get; set; }
        public string? Title { get; set; }
        public string? Description { get; set; }
        public List<SnapshotTask>? Tasks { get; set; }
        public List<string>? Skills { get; set; }
        public List<string>? Technologies { get; set; }
        public List<string>? Titles { get; set; }
    }

    private sealed class SnapshotTask
    {
        public int Id { get; set; }
        public string? Text { get; set; }
    }
}
