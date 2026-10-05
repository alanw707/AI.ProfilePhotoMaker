using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using AI.ProfilePhotoMaker.API.Services.Career;
using FluentAssertions;
using Xunit;

namespace AI.ProfilePhotoMaker.API.Tests.Services.Career;

/// <summary>The shipped BLS snapshot is verified and each source fails closed on its own (ADR 0011).</summary>
public class MarketReferenceTests
{
    private static byte[] ShippedBytes()
    {
        using var stream = EmbeddedMarketReference.OpenEmbeddedSnapshot();
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        return buffer.ToArray();
    }

    private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    private static byte[] Gzip(string json)
    {
        using var output = new MemoryStream();
        using (var gzip = new GZipStream(output, CompressionLevel.Fastest))
        {
            gzip.Write(Encoding.UTF8.GetBytes(json));
        }
        return output.ToArray();
    }

    /// <summary>A tiny but complete snapshot, so integrity tests need not edit 5 MB of JSON.</summary>
    public static JsonObject TinySnapshot() => JsonNode.Parse("""
        {
          "sources": {
            "oews": {
              "name": "OEWS", "publisher": "BLS", "referencePeriod": "2025-05", "publishedOn": "2026-05-15",
              "url": "https://x", "definitionsUrl": "https://x/d", "license": "Public domain (U.S. government work)",
              "citation": "c", "wageDefinition": "w", "employmentDefinition": "e", "coverage": "cov",
              "topCode": { "annual": 239200, "hourly": 115.0 },
              "fields": ["TOT_EMP","EMP_PRSE","JOBS_1000","LOC_QUOTIENT","A_MEAN","MEAN_PRSE","A_PCT10","A_PCT25","A_MEDIAN","A_PCT75","A_PCT90","H_MEDIAN","ANNUAL","HOURLY"]
            },
            "projections": {
              "name": "EP", "publisher": "BLS", "referencePeriod": "2025-2035", "publishedOn": "2026-08-27",
              "url": "https://x", "definitionsUrl": "https://x/d", "license": "Public domain (U.S. government work)",
              "citation": "c", "coverage": "cov"
            }
          },
          "areas": [ { "code": "99", "title": "U.S.", "type": "national", "state": "US" } ],
          "occupations": { "15-1252": "Software Developers" },
          "wages": { "99": { "15-1252": [100, 1.5, null, null, 10, 1, 1, 2, 3, 4, 5, 6.5, null, null] } },
          "projections": { "15-1252": { "title": "Software developers", "employment2025Thousands": 1, "employment2035Thousands": 2,
            "changePercent": 3.5, "annualOpeningsThousands": 4, "education": "Bachelor's degree" } },
          "crosswalk": { "oews": { "15-1252.00": { "code": "15-1252", "match": "exact" } },
                         "projections": { "15-1252.00": { "code": "15-1252", "match": "exact" } } }
        }
        """)!.AsObject();

    private static EmbeddedMarketReference Load(JsonObject snapshot)
    {
        var bytes = Gzip(snapshot.ToJsonString());
        return new EmbeddedMarketReference(() => new MemoryStream(bytes), Hash(bytes));
    }

    [Fact]
    public void ShippedBytesMatchThePinnedHashAndLoadBothSources()
    {
        Hash(ShippedBytes()).Should().Be("54a8afa5edee78caeabe9682b500887d92e6b60cc304860f0d3b569232201f9d");
        EmbeddedMarketReference.ExpectedSha256.Should().Be("54a8afa5edee78caeabe9682b500887d92e6b60cc304860f0d3b569232201f9d");

        var reference = new EmbeddedMarketReference();

        reference.Oews.Should().NotBeNull();
        reference.Projections.Should().NotBeNull();
        reference.Oews!.Source.ReferencePeriod.Should().Be("2025-05");
        reference.Oews.Source.License.Should().Be("Public domain (U.S. government work)");
        reference.Oews.Areas.Should().HaveCount(445);
        reference.Projections!.Source.ReferencePeriod.Should().Be("2025-2035");
        reference.Projections.OccupationCount.Should().Be(831);
    }

    [Fact]
    public void TamperedBytesMakeBothSourcesUnavailable()
    {
        var bytes = ShippedBytes();
        bytes[bytes.Length / 2] ^= 0xFF;

        var reference = new EmbeddedMarketReference(() => new MemoryStream(bytes));

        reference.Oews.Should().BeNull();
        reference.Projections.Should().BeNull();
    }

    [Fact]
    public void MalformedSnapshotWithAMatchingHashIsUnavailable()
    {
        var bytes = Gzip("{ not json");

        var reference = new EmbeddedMarketReference(() => new MemoryStream(bytes), Hash(bytes));

        reference.Oews.Should().BeNull();
        reference.Projections.Should().BeNull();
    }

    [Fact]
    public void TinySnapshotLoadsBothSources()
    {
        var reference = Load(TinySnapshot());

        reference.Oews!.Wage("99", "15-1252")!.MedianAnnual.Should().Be(new MarketValue(3, MarketValueStatus.Available));
        reference.Projections!.Row("15-1252")!.ChangePercent.Number.Should().Be(3.5);
    }

    [Theory]
    [InlineData("oews")]
    [InlineData("projections")]
    public void AWrongLicenceMakesOnlyThatSourceUnavailable(string source)
    {
        var snapshot = TinySnapshot();
        snapshot["sources"]![source]!["license"] = "CC BY 4.0";

        var reference = Load(snapshot);

        (reference.Oews == null).Should().Be(source == "oews");
        (reference.Projections == null).Should().Be(source == "projections");
    }

    [Theory]
    [InlineData("oews")]
    [InlineData("projections")]
    public void AMissingSourceLeavesTheOtherWorking(string source)
    {
        var snapshot = TinySnapshot();
        snapshot["sources"]!.AsObject().Remove(source);

        var reference = Load(snapshot);

        (reference.Oews == null).Should().Be(source == "oews");
        (reference.Projections == null).Should().Be(source == "projections");
    }

    [Fact]
    public void AMalformedWageRowMakesOnlyOewsUnavailable()
    {
        var snapshot = TinySnapshot();
        snapshot["wages"]!["99"]!["15-1252"] = new JsonArray(1, 2);

        var reference = Load(snapshot);

        reference.Oews.Should().BeNull();
        reference.Projections.Should().NotBeNull();
    }

    [Fact]
    public void UnknownStatusStringsFailClosed()
    {
        var snapshot = TinySnapshot();
        snapshot["wages"]!["99"]!["15-1252"]![8] = "n/a";

        Load(snapshot).Oews.Should().BeNull();
    }

    [Fact]
    public void StatusesBecomeTypedValuesNeverZero()
    {
        var snapshot = TinySnapshot();
        var row = snapshot["wages"]!["99"]!["15-1252"]!.AsArray();
        row[8] = "*";
        row[10] = "#";
        row[11] = "#";
        row[0] = "**";

        var wage = Load(snapshot).Oews!.Wage("99", "15-1252")!;

        wage.MedianAnnual.Should().Be(MarketValue.NotAvailable);
        wage.Employment.Should().Be(MarketValue.NotAvailable);
        wage.Pct90Annual.Should().Be(new MarketValue(239200, MarketValueStatus.TopCoded));
        wage.MedianHourly.Should().Be(new MarketValue(115.0, MarketValueStatus.TopCoded));
        wage.JobsPer1000.Should().Be(MarketValue.NotAvailable);
    }

    [Fact]
    public void UnknownRowsAndAreasAreNull()
    {
        var oews = Load(TinySnapshot()).Oews!;

        oews.Wage("99", "99-9999").Should().BeNull();
        oews.Wage("00000", "15-1252").Should().BeNull();
        oews.Area("99")!.Title.Should().Be("U.S.");
        oews.Crosswalk("15-1252.00")!.Match.Should().Be("exact");
        oews.Crosswalk("21-1011.00").Should().BeNull();
    }

    [Theory]
    [InlineData("oews")]
    [InlineData("projections")]
    public void SharedCrosswalkLoadsButUnknownMatchFailsOnlyItsSource(string source)
    {
        var snapshot = TinySnapshot();
        snapshot["crosswalk"]![source]!["15-1252.00"]!["match"] = "shared";
        var loaded = Load(snapshot);
        (source == "oews" ? loaded.Oews?.Crosswalk("15-1252.00") : loaded.Projections?.Crosswalk("15-1252.00"))
            .Should().Be(new MarketCrosswalkEntry("15-1252", "shared"));

        snapshot["crosswalk"]![source]!["15-1252.00"]!["match"] = "unknown";
        loaded = Load(snapshot);
        (loaded.Oews == null).Should().Be(source == "oews");
        (loaded.Projections == null).Should().Be(source == "projections");
    }

    [Fact]
    public void CrosswalkTellsExactFromBroadAndUnmapped()
    {
        var reference = new EmbeddedMarketReference();

        reference.Oews!.Crosswalk("15-1252.00").Should().Be(new MarketCrosswalkEntry("15-1252", "exact"));
        reference.Oews.Crosswalk("13-1021.00").Should().Be(new MarketCrosswalkEntry("13-1020", "broad"));
        reference.Oews.Crosswalk("15-1299.08").Should().Be(new MarketCrosswalkEntry("15-1299", "shared"));
        reference.Oews.Crosswalk("21-1011.00").Should().BeNull();
        reference.Projections!.Crosswalk("21-1011.00").Should().BeNull();
    }
}
