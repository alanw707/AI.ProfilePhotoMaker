using System.IO.Compression;
using System.Text.Json;
using AI.ProfilePhotoMaker.API.Services.Career;

namespace AI.ProfilePhotoMaker.API.Tests.Services.Career;

/// <summary>A reference with chosen halves, so tests can make one source unavailable.</summary>
public sealed class FakeMarketReference : IMarketReference
{
    public FakeMarketReference(OewsData? oews, ProjectionsData? projections)
    {
        Oews = oews;
        Projections = projections;
    }

    public OewsData? Oews { get; }
    public ProjectionsData? Projections { get; }

    private static readonly EmbeddedMarketReference Real = new();

    public static FakeMarketReference WithoutProjections() => new(Real.Oews, null);
    public static FakeMarketReference WithoutOews() => new(null, Real.Projections);
    public static FakeMarketReference Neither() => new(null, null);
}

/// <summary>
/// The shipped snapshot read straight from its JSON, independent of the loader, so fixture tests
/// compare a brief with what the file really says.
/// </summary>
public static class RawSnapshot
{
    private static readonly Lazy<JsonDocument> Document = new(() =>
    {
        using var stream = EmbeddedMarketReference.OpenEmbeddedSnapshot();
        using var gzip = new GZipStream(stream, CompressionMode.Decompress);
        return JsonDocument.Parse(gzip);
    });

    public static JsonElement Root => Document.Value.RootElement;

    private static readonly string[] Fields = Root.GetProperty("sources").GetProperty("oews").GetProperty("fields")
        .EnumerateArray().Select(f => f.GetString()!).ToArray();

    /// <summary>The raw cell of a wage row (a number, null or a status string), or null when the row does not exist.</summary>
    public static JsonElement? Cell(string areaCode, string soc, string field)
    {
        if (!Root.GetProperty("wages").TryGetProperty(areaCode, out var area) || !area.TryGetProperty(soc, out var row))
        {
            return null;
        }
        return row[Array.IndexOf(Fields, field)];
    }

    public static JsonElement Projection(string soc) => Root.GetProperty("projections").GetProperty(soc);

    public static bool HasWageRow(string areaCode, string soc) =>
        Root.GetProperty("wages").GetProperty(areaCode).TryGetProperty(soc, out _);
}
