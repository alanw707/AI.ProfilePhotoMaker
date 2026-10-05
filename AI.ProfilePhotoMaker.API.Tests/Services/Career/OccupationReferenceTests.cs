using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using AI.ProfilePhotoMaker.API.Services.Career;
using FluentAssertions;
using Xunit;

namespace AI.ProfilePhotoMaker.API.Tests.Services.Career;

/// <summary>The shipped O*NET snapshot is verified, complete and fails closed when it is not (ADR 0010).</summary>
public class OccupationReferenceTests
{
    [Fact]
    public void EmbeddedSnapshotLoadsWithItsSourceAndAllOccupations()
    {
        var data = new EmbeddedOccupationReference().Data;

        data.Should().NotBeNull();
        data!.Occupations.Should().HaveCount(893);
        data.Source.Release.Should().Be("30.0");
        data.Source.Name.Should().Be("O*NET 30.0 Database");
        data.Source.Taxonomy.Should().Be("O*NET-SOC 2019");
        data.Source.License.Should().Be("CC BY 4.0");
        data.Source.Attribution.Should().Contain("O*NET");
        data.Find("15-1252.00")!.Title.Should().Be("Software Developers");
        data.Occupations.Should().OnlyContain(o => System.Text.RegularExpressions.Regex.IsMatch(o.Code, @"^\d{2}-\d{4}\.\d{2}$"));
    }

    [Fact]
    public void ShippedBytesMatchThePinnedHash()
    {
        using var stream = EmbeddedOccupationReference.OpenEmbeddedSnapshot();
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);

        Convert.ToHexString(SHA256.HashData(buffer.ToArray())).ToLowerInvariant()
            .Should().Be(EmbeddedOccupationReference.ExpectedSha256);
    }

    [Fact]
    public void TamperedBytesAreUnavailable()
    {
        using var stream = EmbeddedOccupationReference.OpenEmbeddedSnapshot();
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        var bytes = buffer.ToArray();
        bytes[bytes.Length / 2] ^= 0xFF;

        new EmbeddedOccupationReference(() => new MemoryStream(bytes)).Data.Should().BeNull();
    }

    [Fact]
    public void MalformedSnapshotWithAMatchingHashIsStillUnavailable()
    {
        var bytes = Gzip("{ not json");

        new EmbeddedOccupationReference(() => new MemoryStream(bytes), Hash(bytes)).Data.Should().BeNull();
    }

    private const string GoodSource =
        "\"name\":\"O*NET 30.0 Database\",\"release\":\"30.0\",\"releaseDate\":\"2025-08\",\"taxonomy\":\"O*NET-SOC 2019\"," +
        "\"url\":\"https://www.onetcenter.org/\",\"license\":\"CC BY 4.0\",\"licenseUrl\":\"https://creativecommons.org/licenses/by/4.0/\"," +
        "\"attribution\":\"Includes O*NET data.\"";

    private const string GoodOccupation =
        "{\"code\":\"15-1252.00\",\"title\":\"Software Developers\",\"description\":\"d\"," +
        "\"tasks\":[{\"id\":1,\"text\":\"Modify existing software to correct errors\"}],\"skills\":[],\"technologies\":[],\"titles\":[]}";

    private static EmbeddedOccupationReference Snapshot(string source, string occupation)
    {
        var bytes = Gzip($"{{\"source\":{{{source}}},\"occupations\":[{occupation}]}}");
        return new EmbeddedOccupationReference(() => new MemoryStream(bytes), Hash(bytes));
    }

    [Fact]
    public void AMinimalSnapshotWithValidatedLicenceAndTaxonomyLoads()
    {
        Snapshot(GoodSource, GoodOccupation).Data!.Source.License.Should().Be("CC BY 4.0");
    }

    [Fact]
    public void ABadOccupationCodeMakesTheWholeSnapshotUnavailable()
    {
        Snapshot(GoodSource, GoodOccupation.Replace("15-1252.00", "1-1")).Data.Should().BeNull();
    }

    [Theory]
    [InlineData("\"license\":\"CC BY 4.0\"", "\"license\":\"Proprietary\"")]
    [InlineData("\"license\":\"CC BY 4.0\",", "")]
    [InlineData("\"taxonomy\":\"O*NET-SOC 2019\"", "\"taxonomy\":\"SOC 2010\"")]
    [InlineData("\"attribution\":\"Includes O*NET data.\"", "\"attribution\":\" \"")]
    [InlineData("\"licenseUrl\":\"https://creativecommons.org/licenses/by/4.0/\",", "")]
    [InlineData("\"url\":\"https://www.onetcenter.org/\",", "")]
    public void ASnapshotWithoutAValidatedLicenceOrTaxonomyIsUnavailable(string field, string replacement)
    {
        GoodSource.Should().Contain(field);

        Snapshot(GoodSource.Replace(field, replacement), GoodOccupation).Data.Should().BeNull();
    }

    [Fact]
    public void MissingResourceIsUnavailable()
    {
        new EmbeddedOccupationReference(() => throw new FileNotFoundException()).Data.Should().BeNull();
    }

    private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    private static byte[] Gzip(string text)
    {
        using var output = new MemoryStream();
        using (var gz = new GZipStream(output, CompressionLevel.Fastest))
        {
            gz.Write(Encoding.UTF8.GetBytes(text));
        }
        return output.ToArray();
    }
}
