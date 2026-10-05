using AI.ProfilePhotoMaker.API.Services.Career;
using FluentAssertions;
using Xunit;

namespace AI.ProfilePhotoMaker.API.Tests.Services.Career;

/// <summary>Free-text goal locations resolve against the real BLS areas, or are honestly unresolved (ADR 0011).</summary>
public class MarketAreaResolverTests
{
    private static readonly IReadOnlyList<MarketArea> Areas = new EmbeddedMarketReference().Oews!.Areas;

    [Theory]
    [InlineData("Denver, CO", "metro", "19740", "Denver-Aurora-Centennial, CO")]
    [InlineData("denver,co", "metro", "19740", "Denver-Aurora-Centennial, CO")]
    [InlineData("Aurora, CO", "metro", "19740", "Denver-Aurora-Centennial, CO")]
    [InlineData("New York, NY", "metro", "35620", "New York-Newark-Jersey City, NY-NJ")]
    [InlineData("Newark, NJ", "metro", "35620", "New York-Newark-Jersey City, NY-NJ")]
    [InlineData("Kansas City, MO", "metro", "28140", "Kansas City, MO-KS")]
    [InlineData("Kansas City, KS", "metro", "28140", "Kansas City, MO-KS")]
    [InlineData("Portland, OR", "metro", "38900", "Portland-Vancouver-Hillsboro, OR-WA")]
    [InlineData("Portland, Maine", "metro", "38860", "Portland-South Portland, ME")]
    [InlineData("Winston-Salem, NC", "metro", "49180", "Winston-Salem, NC")]
    [InlineData("Washington, DC", "metro", "47900", "Washington-Arlington-Alexandria, DC-VA-MD-WV")]
    // Common spellings that differ from the BLS title (review R2).
    [InlineData("Washington, D.C.", "metro", "47900", "Washington-Arlington-Alexandria, DC-VA-MD-WV")]
    [InlineData("New York City, NY", "metro", "35620", "New York-Newark-Jersey City, NY-NJ")]
    [InlineData("Louisville, KY", "metro", "31140", "Louisville/Jefferson County, KY-IN")]
    [InlineData("Saint Louis, MO", "metro", "41180", "St. Louis, MO-IL")]
    [InlineData("St Louis, MO", "metro", "41180", "St. Louis, MO-IL")]
    [InlineData("Saint Paul, MN", "metro", "33460", "Minneapolis-St. Paul-Bloomington, MN-WI")]
    [InlineData("Nashville, TN", "metro", "34980", "Nashville-Davidson--Murfreesboro--Franklin, TN")]
    [InlineData("Murfreesboro, TN", "metro", "34980", "Nashville-Davidson--Murfreesboro--Franklin, TN")]
    [InlineData("Honolulu, HI", "metro", "46520", "Urban Honolulu, HI")]
    [InlineData("Boise, ID", "metro", "14260", "Boise City, ID")]
    public void CityAndStateResolveToTheMetroThatListsThem(string input, string resolution, string code, string title)
    {
        var location = MarketAreaResolver.Resolve(input, Areas);

        location.Resolution.Should().Be(resolution);
        location.Local.Should().Be(new MarketAreaDto(code, title, "metro"));
        location.Input.Should().Be(input);
    }

    [Theory]
    [InlineData("Colorado")]
    [InlineData("CO")]
    [InlineData("colorado")]
    public void AStateCodeOrNameResolvesToTheState(string input)
    {
        var location = MarketAreaResolver.Resolve(input, Areas);

        location.Resolution.Should().Be("state");
        location.Local.Should().Be(new MarketAreaDto("08", "Colorado", "state"));
    }

    [Fact]
    public void ACityOutsideEveryMetroFallsBackToItsState()
    {
        var location = MarketAreaResolver.Resolve("Nowhereville, CO", Areas);

        location.Resolution.Should().Be("state");
        location.Local!.Code.Should().Be("08");
    }

    [Fact]
    public void ACityInTheWrongStateDoesNotMatchAnotherStatesMetro()
    {
        // Denver is listed for CO only; "Denver, TX" is a real state with no such metro.
        var location = MarketAreaResolver.Resolve("Denver, TX", Areas);

        location.Resolution.Should().Be("state");
        location.Local!.Title.Should().Be("Texas");
    }

    [Theory]
    [InlineData("Remote")]
    [InlineData("  remote ")]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("   ")]
    public void RemoteOrBlankUsesTheNationOnly(string? input)
    {
        var location = MarketAreaResolver.Resolve(input, Areas);

        location.Resolution.Should().Be("national_only");
        location.Local.Should().BeNull();
    }

    [Theory]
    [InlineData("Atlantis, ZZ")]
    [InlineData("Atlantis")]
    [InlineData(", CO")]
    [InlineData("Denver, ")]
    public void UnrecognisedLocationsAreUnresolved(string input)
    {
        var location = MarketAreaResolver.Resolve(input, Areas);

        location.Resolution.Should().Be("unresolved");
        location.Local.Should().BeNull();
    }
}
