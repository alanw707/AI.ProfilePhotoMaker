using System.Globalization;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;

namespace AI.ProfilePhotoMaker.API.Services.Career;

public enum MarketValueStatus
{
    Available,
    NotAvailable,
    TopCoded,
    NotPublished
}

/// <summary>
/// One published figure. <see cref="Number"/> is set only when the figure is available, or when it is
/// top-coded (then it carries the top code). Never a stand-in zero.
/// </summary>
public readonly record struct MarketValue(double? Number, MarketValueStatus Status)
{
    public static MarketValue NotPublished => new(null, MarketValueStatus.NotPublished);
    public static MarketValue NotAvailable => new(null, MarketValueStatus.NotAvailable);
}

public sealed record MarketArea(string Code, string Title, string Type, string State);

/// <summary>Where an O*NET code lands in a BLS table: the SOC code and whether it is the exact or the broad-group code.</summary>
public sealed record MarketCrosswalkEntry(string Code, string Match);

public sealed record MarketSource(
    string Id,
    string Name,
    string Publisher,
    string ReferencePeriod,
    string PublishedOn,
    string Url,
    string DefinitionsUrl,
    string License,
    string Citation,
    string Definition,
    string Coverage);

public sealed record MarketWageRow(
    MarketValue Employment,
    MarketValue EmploymentPrse,
    MarketValue JobsPer1000,
    MarketValue LocationQuotient,
    MarketValue MeanAnnual,
    MarketValue MeanPrse,
    MarketValue Pct10Annual,
    MarketValue Pct25Annual,
    MarketValue MedianAnnual,
    MarketValue Pct75Annual,
    MarketValue Pct90Annual,
    MarketValue MedianHourly);

public sealed record MarketProjectionRow(
    string Title,
    MarketValue Employment2025Thousands,
    MarketValue Employment2035Thousands,
    MarketValue ChangePercent,
    MarketValue AnnualOpeningsThousands,
    string? TypicalEducation);

/// <summary>The OEWS half of the snapshot: areas, occupations, wage rows and the O*NET crosswalk.</summary>
public sealed class OewsData
{
    // Wage cells are packed into one array per area (rows sorted by SOC code), so 177k rows
    // cost a few MB instead of an object graph. Sentinels stand for BLS statuses; no real
    // figure is negative, so they cannot collide with data.
    internal const double Missing = double.NaN;
    internal const double WageNotAvailable = -1;
    internal const double EmploymentNotAvailable = -2;
    internal const double TopCoded = -3;

    // The 13th packed cell is the pay basis: which wages BLS publishes for the occupation.
    internal const int FieldCount = 13;
    internal const int PayBasisField = 12;
    internal const double BothBases = 0;
    internal const double AnnualOnly = 1;
    internal const double HourlyOnly = 2;

    private readonly Dictionary<string, MarketArea> _areas;
    private readonly Dictionary<string, AreaRows> _rows;
    private readonly Dictionary<string, MarketCrosswalkEntry> _crosswalk;
    private readonly double _topCodeAnnual;
    private readonly double _topCodeHourly;

    internal sealed record AreaRows(string[] Socs, double[] Values);

    internal OewsData(
        MarketSource source, IReadOnlyList<MarketArea> areas, int occupationCount, Dictionary<string, AreaRows> rows,
        Dictionary<string, MarketCrosswalkEntry> crosswalk, double topCodeAnnual, double topCodeHourly)
    {
        Source = source;
        Areas = areas;
        OccupationCount = occupationCount;
        _areas = areas.ToDictionary(a => a.Code, StringComparer.Ordinal);
        _rows = rows;
        _crosswalk = crosswalk;
        _topCodeAnnual = topCodeAnnual;
        _topCodeHourly = topCodeHourly;
    }

    public MarketSource Source { get; }
    public IReadOnlyList<MarketArea> Areas { get; }
    public int OccupationCount { get; }

    public MarketArea? Area(string code) => _areas.GetValueOrDefault(code);

    public MarketCrosswalkEntry? Crosswalk(string onetCode) => _crosswalk.GetValueOrDefault(onetCode);

    /// <summary>The wage row for a published SOC code in an area, or null when BLS publishes none ("not published").</summary>
    public MarketWageRow? Wage(string areaCode, string soc)
    {
        if (!_rows.TryGetValue(areaCode, out var area))
        {
            return null;
        }
        var index = Array.BinarySearch(area.Socs, soc, StringComparer.Ordinal);
        if (index < 0)
        {
            return null;
        }

        var v = new ArraySegment<double>(area.Values, index * FieldCount, FieldCount);
        // Wages BLS does not publish for this occupation are "not published", not "too few responses".
        var annualOnly = v[PayBasisField] == AnnualOnly;
        var hourlyOnly = v[PayBasisField] == HourlyOnly;
        MarketValue Annual(int field) => hourlyOnly ? MarketValue.NotPublished : Cell(v[field], field);
        return new MarketWageRow(
            Employment: Cell(v[0], 0),
            EmploymentPrse: Cell(v[1], 1),
            JobsPer1000: Cell(v[2], 2),
            LocationQuotient: Cell(v[3], 3),
            MeanAnnual: Annual(4),
            MeanPrse: Cell(v[5], 5),
            Pct10Annual: Annual(6),
            Pct25Annual: Annual(7),
            MedianAnnual: Annual(8),
            Pct75Annual: Annual(9),
            Pct90Annual: Annual(10),
            MedianHourly: annualOnly ? MarketValue.NotPublished : Cell(v[11], 11));
    }

    private MarketValue Cell(double raw, int field)
    {
        if (double.IsNaN(raw) || raw == WageNotAvailable || raw == EmploymentNotAvailable)
        {
            return MarketValue.NotAvailable;
        }
        if (raw == TopCoded)
        {
            // "#" means at or above the top code: carry the published ceiling, as a ceiling.
            return new MarketValue(field == 11 ? _topCodeHourly : _topCodeAnnual, MarketValueStatus.TopCoded);
        }
        return new MarketValue(raw, MarketValueStatus.Available);
    }
}

/// <summary>The Employment Projections half of the snapshot: national rows by SOC code and the crosswalk.</summary>
public sealed class ProjectionsData
{
    private readonly Dictionary<string, MarketProjectionRow> _rows;
    private readonly Dictionary<string, MarketCrosswalkEntry> _crosswalk;

    internal ProjectionsData(MarketSource source, Dictionary<string, MarketProjectionRow> rows, Dictionary<string, MarketCrosswalkEntry> crosswalk)
    {
        Source = source;
        _rows = rows;
        _crosswalk = crosswalk;
    }

    public MarketSource Source { get; }
    public int OccupationCount => _rows.Count;

    public MarketCrosswalkEntry? Crosswalk(string onetCode) => _crosswalk.GetValueOrDefault(onetCode);

    public MarketProjectionRow? Row(string soc) => _rows.GetValueOrDefault(soc);
}

/// <summary>
/// The verified BLS snapshot, one half per source. Either half can be unavailable (null) while
/// the other still works, so a brief can say exactly what it could not look up.
/// </summary>
public interface IMarketReference
{
    OewsData? Oews { get; }
    ProjectionsData? Projections { get; }
}

/// <summary>
/// Loads the embedded snapshot once, on first use. The bytes must hash to <see cref="ExpectedSha256"/>;
/// then each source is validated on its own (public-domain licence, required fields, well-formed
/// values) and fails closed alone. The JSON is parsed into compact structures and dropped.
/// </summary>
public sealed class EmbeddedMarketReference : IMarketReference
{
    /// <summary>SHA-256 of Data/Reference/bls-snapshot.json.gz (OEWS May 2025 + projections 2025-35, ADR 0011).</summary>
    public const string ExpectedSha256 = "542f51c0c76b59648728162d7f31185319601147143b1e913dfe576cac36f418";

    public const string RequiredLicense = "Public domain (U.S. government work)";

    private const string ResourceName = "bls-snapshot.json.gz";

    // OEWS columns kept, in the order OewsData packs them.
    private static readonly string[] WageFields =
    {
        "TOT_EMP", "EMP_PRSE", "JOBS_1000", "LOC_QUOTIENT", "A_MEAN", "MEAN_PRSE",
        "A_PCT10", "A_PCT25", "A_MEDIAN", "A_PCT75", "A_PCT90", "H_MEDIAN"
    };

    private const string ProjectionsDefinition =
        "Projected national employment in 2035, the change from 2025, and average annual occupational openings from growth and replacement needs, in thousands of jobs.";

    private readonly Lazy<(OewsData? Oews, ProjectionsData? Projections)> _data;

    public EmbeddedMarketReference() : this(OpenEmbeddedSnapshot)
    {
    }

    /// <summary>Injected stream and hash so tests can cover tampered and partial snapshots.</summary>
    public EmbeddedMarketReference(Func<Stream> open, string expectedSha256 = ExpectedSha256, ILogger? logger = null)
    {
        _data = new Lazy<(OewsData?, ProjectionsData?)>(() => Load(open, expectedSha256, logger), LazyThreadSafetyMode.ExecutionAndPublication);
    }

    public OewsData? Oews => _data.Value.Oews;
    public ProjectionsData? Projections => _data.Value.Projections;

    public static Stream OpenEmbeddedSnapshot() =>
        typeof(EmbeddedMarketReference).Assembly.GetManifestResourceStream(ResourceName)
        ?? throw new FileNotFoundException("The market snapshot is not embedded.");

    private static (OewsData?, ProjectionsData?) Load(Func<Stream> open, string expectedSha256, ILogger? logger)
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
                logger?.LogError("Market snapshot hash mismatch; market briefs are unavailable");
                return (null, null);
            }

            using var gzip = new GZipStream(new MemoryStream(bytes), CompressionMode.Decompress);
            using var document = JsonDocument.Parse(gzip);
            var root = document.RootElement;
            return (
                Guard("OEWS", logger, () => BuildOews(root)),
                Guard("projections", logger, () => BuildProjections(root)));
        }
        catch (Exception ex) when (ex is JsonException or InvalidDataException or IOException or InvalidOperationException or FormatException)
        {
            logger?.LogError("Market snapshot could not be loaded: {ExceptionType}", ex.GetType().Name);
            return (null, null);
        }
    }

    /// <summary>A source that fails validation is dropped on its own; the other keeps working.</summary>
    private static T? Guard<T>(string name, ILogger? logger, Func<T?> build) where T : class
    {
        try
        {
            return build();
        }
        catch (Exception ex) when (ex is InvalidOperationException or KeyNotFoundException or FormatException or JsonException or InvalidCastException)
        {
            logger?.LogError("Market snapshot {Source} data could not be loaded: {ExceptionType}", name, ex.GetType().Name);
            return null;
        }
    }

    // ---- Shared ------------------------------------------------------------------

    private static string Text(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(value.GetString())
            ? value.GetString()!
            : throw new InvalidOperationException($"Missing text '{name}'.");

    /// <summary>The source block, or null when it is absent or not public domain.</summary>
    private static MarketSource? ReadSource(JsonElement root, string id, string definition)
    {
        if (!root.TryGetProperty("sources", out var sources) || !sources.TryGetProperty(id, out var s) || s.ValueKind != JsonValueKind.Object)
        {
            return null;
        }
        // Only public-domain data may back a brief; anything else is treated as not there.
        if (!s.TryGetProperty("license", out var license) || license.GetString() != RequiredLicense)
        {
            return null;
        }
        var publishedOn = Text(s, "publishedOn");
        if (!DateOnly.TryParseExact(publishedOn, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
        {
            throw new FormatException("publishedOn is not a date.");
        }
        return new MarketSource(
            id, Text(s, "name"), Text(s, "publisher"), Text(s, "referencePeriod"), publishedOn, Text(s, "url"),
            Text(s, "definitionsUrl"), RequiredLicense, Text(s, "citation"), definition, Text(s, "coverage"));
    }

    private static Dictionary<string, MarketCrosswalkEntry> ReadCrosswalk(JsonElement root, string id)
    {
        var result = new Dictionary<string, MarketCrosswalkEntry>(StringComparer.Ordinal);
        foreach (var entry in root.GetProperty("crosswalk").GetProperty(id).EnumerateObject())
        {
            var match = Text(entry.Value, "match");
            if (match is not ("exact" or "broad"))
            {
                throw new FormatException("Unknown crosswalk match.");
            }
            result[entry.Name] = new MarketCrosswalkEntry(Text(entry.Value, "code"), match);
        }
        return result;
    }

    // ---- OEWS --------------------------------------------------------------------

    private static OewsData? BuildOews(JsonElement root)
    {
        var sourceBlock = root.TryGetProperty("sources", out var sources) && sources.TryGetProperty("oews", out var oews) ? oews : default;
        var definition = sourceBlock.ValueKind == JsonValueKind.Object
            ? $"{Text(sourceBlock, "wageDefinition")} {Text(sourceBlock, "employmentDefinition")}"
            : string.Empty;
        var source = ReadSource(root, "oews", definition);
        if (source == null)
        {
            return null;
        }

        var topCode = sourceBlock.GetProperty("topCode");
        var topAnnual = topCode.GetProperty("annual").GetDouble();
        var topHourly = topCode.GetProperty("hourly").GetDouble();

        // Columns are read by name, so a reordered snapshot still maps correctly.
        var fields = sourceBlock.GetProperty("fields").EnumerateArray().Select(f => f.GetString()!).ToList();
        var columns = WageFields.Select(name => fields.IndexOf(name)).ToArray();
        var annualColumn = fields.IndexOf("ANNUAL");
        var hourlyColumn = fields.IndexOf("HOURLY");
        if (columns.Any(c => c < 0) || annualColumn < 0 || hourlyColumn < 0)
        {
            throw new InvalidOperationException("A wage column is missing.");
        }

        var areas = new List<MarketArea>();
        foreach (var a in root.GetProperty("areas").EnumerateArray())
        {
            var type = Text(a, "type");
            if (type is not ("national" or "state" or "metro"))
            {
                throw new FormatException("Unknown area type.");
            }
            areas.Add(new MarketArea(Text(a, "code"), Text(a, "title"), type, Text(a, "state")));
        }
        if (areas.Select(a => a.Code).Distinct().Count() != areas.Count || areas.All(a => a.Type != "national"))
        {
            throw new InvalidOperationException("Areas are duplicated or the nation is missing.");
        }

        var occupations = root.GetProperty("occupations").EnumerateObject().ToDictionary(o => o.Name, o => o.Name, StringComparer.Ordinal);
        var rows = new Dictionary<string, OewsData.AreaRows>(StringComparer.Ordinal);
        foreach (var area in root.GetProperty("wages").EnumerateObject())
        {
            var socs = new List<string>();
            var values = new List<double>();
            foreach (var row in area.Value.EnumerateObject().OrderBy(r => r.Name, StringComparer.Ordinal))
            {
                if (row.Value.GetArrayLength() != fields.Count || !occupations.TryGetValue(row.Name, out var soc))
                {
                    throw new FormatException("A wage row does not match the columns or occupations.");
                }
                socs.Add(soc);
                for (var f = 0; f < WageFields.Length; f++)
                {
                    values.Add(PackCell(row.Value[columns[f]]));
                }
                values.Add(row.Value[annualColumn].ValueKind == JsonValueKind.True ? OewsData.AnnualOnly
                    : row.Value[hourlyColumn].ValueKind == JsonValueKind.True ? OewsData.HourlyOnly
                    : OewsData.BothBases);
            }
            rows[area.Name] = new OewsData.AreaRows(socs.ToArray(), values.ToArray());
        }
        if (!rows.ContainsKey(areas.First(a => a.Type == "national").Code))
        {
            throw new InvalidOperationException("National wages are missing.");
        }

        return new OewsData(source, areas, occupations.Count, rows, ReadCrosswalk(root, "oews"), topAnnual, topHourly);
    }

    /// <summary>A number, null, or a BLS status string, packed with the sentinels of <see cref="OewsData"/>.</summary>
    private static double PackCell(JsonElement cell) => cell.ValueKind switch
    {
        JsonValueKind.Number => cell.GetDouble(),
        JsonValueKind.Null or JsonValueKind.True => OewsData.Missing,
        JsonValueKind.String => cell.GetString() switch
        {
            "*" => OewsData.WageNotAvailable,
            "**" => OewsData.EmploymentNotAvailable,
            "#" => OewsData.TopCoded,
            _ => throw new FormatException("Unknown status value.")
        },
        _ => throw new FormatException("Unexpected cell.")
    };

    // ---- Projections -------------------------------------------------------------

    private static ProjectionsData? BuildProjections(JsonElement root)
    {
        var source = ReadSource(root, "projections", ProjectionsDefinition);
        if (source == null)
        {
            return null;
        }

        var rows = new Dictionary<string, MarketProjectionRow>(StringComparer.Ordinal);
        foreach (var p in root.GetProperty("projections").EnumerateObject())
        {
            var v = p.Value;
            rows[p.Name] = new MarketProjectionRow(
                Text(v, "title"),
                ProjectionValue(v, "employment2025Thousands"),
                ProjectionValue(v, "employment2035Thousands"),
                ProjectionValue(v, "changePercent"),
                ProjectionValue(v, "annualOpeningsThousands"),
                v.TryGetProperty("education", out var education) && education.ValueKind == JsonValueKind.String ? education.GetString() : null);
        }
        if (rows.Count == 0)
        {
            throw new InvalidOperationException("No projections.");
        }
        return new ProjectionsData(source, rows, ReadCrosswalk(root, "projections"));
    }

    private static MarketValue ProjectionValue(JsonElement row, string name)
    {
        if (!row.TryGetProperty(name, out var value) || value.ValueKind == JsonValueKind.Null)
        {
            return MarketValue.NotAvailable;
        }
        return value.ValueKind == JsonValueKind.Number
            ? new MarketValue(value.GetDouble(), MarketValueStatus.Available)
            : throw new FormatException("Projection value is not a number.");
    }
}
