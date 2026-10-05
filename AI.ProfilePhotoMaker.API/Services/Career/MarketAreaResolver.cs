namespace AI.ProfilePhotoMaker.API.Services.Career;

public static class MarketResolutions
{
    public const string Metro = "metro";
    public const string State = "state";
    public const string NationalOnly = "national_only";
    public const string Unresolved = "unresolved";
}

/// <summary>
/// Turns the goal's free-text location into a BLS area (ADR 0011): "City, ST" to the metro that
/// lists that city and state (else the state), a state code or name to the state, "remote" or blank
/// to national only. Anything else is unresolved; nothing is guessed.
/// </summary>
public static class MarketAreaResolver
{
    public static MarketLocationDto Resolve(string? input, IReadOnlyList<MarketArea> areas)
    {
        var text = (input ?? string.Empty).Trim();
        if (text.Length == 0 || text.Equals("remote", StringComparison.OrdinalIgnoreCase))
        {
            return new MarketLocationDto(text, MarketResolutions.NationalOnly, null);
        }

        var states = areas.Where(a => a.Type == "state").ToList();
        var comma = text.LastIndexOf(',');
        if (comma < 0)
        {
            // No city part: the whole text must be a state code or name.
            return FindState(text, states) is { } only ? Local(text, MarketResolutions.State, only) : Unresolved(text);
        }

        var city = text[..comma].Trim();
        var state = FindState(text[(comma + 1)..].Trim(), states);
        if (city.Length == 0 || state == null)
        {
            return Unresolved(text);
        }

        var metro = FindMetro(city, state.State, areas);
        // A real state with no metro for that city still has state figures.
        return metro != null ? Local(text, MarketResolutions.Metro, metro) : Local(text, MarketResolutions.State, state);
    }

    private static MarketArea? FindState(string text, IReadOnlyList<MarketArea> states) =>
        states.FirstOrDefault(s => s.State.Equals(text, StringComparison.OrdinalIgnoreCase))
        ?? states.FirstOrDefault(s => s.Title.Equals(text, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Metro titles read "City-City-City, ST-ST". The input city must be one of the listed cities (or the
    /// whole city part, for names like Winston-Salem) and the state one of the listed states. A city
    /// listed first wins over one listed later, then the lower code, so the choice is stable.
    /// </summary>
    private static MarketArea? FindMetro(string city, string stateCode, IReadOnlyList<MarketArea> areas)
    {
        MarketArea? best = null;
        var bestRank = int.MaxValue;
        foreach (var metro in areas.Where(a => a.Type == "metro"))
        {
            var comma = metro.Title.LastIndexOf(',');
            if (comma < 0)
            {
                continue;
            }
            var metroStates = metro.Title[(comma + 1)..].Split('-', StringSplitOptions.TrimEntries);
            if (!metroStates.Contains(stateCode, StringComparer.OrdinalIgnoreCase))
            {
                continue;
            }

            var cityPart = metro.Title[..comma];
            var cities = cityPart.Split('-', StringSplitOptions.TrimEntries);
            var rank = cityPart.Equals(city, StringComparison.OrdinalIgnoreCase)
                ? 0
                : Array.FindIndex(cities, c => c.Equals(city, StringComparison.OrdinalIgnoreCase)) is var i and >= 0 ? i + 1 : -1;
            if (rank >= 0 && (rank < bestRank || (rank == bestRank && string.CompareOrdinal(metro.Code, best!.Code) < 0)))
            {
                best = metro;
                bestRank = rank;
            }
        }
        return best;
    }

    private static MarketLocationDto Local(string input, string resolution, MarketArea area) =>
        new(input, resolution, new MarketAreaDto(area.Code, area.Title, area.Type));

    private static MarketLocationDto Unresolved(string input) => new(input, MarketResolutions.Unresolved, null);
}
