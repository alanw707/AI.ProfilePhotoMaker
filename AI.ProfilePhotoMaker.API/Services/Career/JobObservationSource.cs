using System.Globalization;
using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace AI.ProfilePhotoMaker.API.Services.Career;

public enum JobRemoteStatus { Unknown, Eligible, Ineligible }

public sealed record RawJobLocation(string? City, string? State);

/// <summary>A posting in the provider-neutral shape. Values are the provider's, unchanged (ADR 0015).</summary>
public sealed record RawJobObservation(
    string ProviderId,
    string? Title,
    string? Organization,
    IReadOnlyList<RawJobLocation> Locations,
    decimal? PayMin,
    decimal? PayMax,
    string? PayUnit,
    string? PayBasis,
    DateOnly? PostedOn,
    DateOnly? ClosesOn,
    JobRemoteStatus Remote,
    string? RemoteNote,
    string? Series,
    string? Grade,
    string? SourceUrl);

/// <summary>Static facts about a source, shown as coverage and used to validate its links.</summary>
public sealed record JobSourceInfo(
    string SourceId, string Name, string Coverage, string Attribution, string SourceUrl, IReadOnlyList<string> AllowedHosts);

/// <summary>Raised by an adapter for any provider outage: non-success status, timeout or malformed payload.</summary>
public sealed class JobSourceUnavailableException : Exception
{
    public JobSourceUnavailableException(string message, Exception? inner = null) : base(message, inner)
    {
    }
}

/// <summary>The seam for live job postings (ADR 0015). Nothing a source returns is stored.</summary>
public interface IJobObservationSource
{
    string SourceId { get; }
    bool IsConfigured { get; }
    JobSourceInfo Info { get; }

    /// <summary>One bounded page of postings. Throws <see cref="JobSourceUnavailableException"/> on a provider failure.</summary>
    Task<IReadOnlyList<RawJobObservation>> FetchAsync(JobObservationQuery query, CancellationToken ct);
}

public static class UsaJobsInfo
{
    public const string SourceId = "usajobs";

    public static JobSourceInfo Info { get; } = new(
        SourceId,
        "USAJOBS",
        "U.S. federal agencies only; not the private-sector market.",
        "Job postings from USAJOBS (U.S. Office of Personnel Management). Displayed values are unchanged.",
        "https://www.usajobs.gov/",
        new[] { "www.usajobs.gov" });
}

/// <summary>The default: no key, so an honest unavailable state rather than a failure.</summary>
public sealed class NoJobObservationSource : IJobObservationSource
{
    public string SourceId => UsaJobsInfo.SourceId;
    public bool IsConfigured => false;
    public JobSourceInfo Info => UsaJobsInfo.Info;

    public Task<IReadOnlyList<RawJobObservation>> FetchAsync(JobObservationQuery query, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<RawJobObservation>>(Array.Empty<RawJobObservation>());
}

public sealed class UsaJobsOptions
{
    public const string SectionName = "USAJobs";

    public string? ApiKey { get; set; }

    /// <summary>USAJOBS requires the registered email as the User-Agent.</summary>
    public string? Email { get; set; }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(ApiKey) && !string.IsNullOrWhiteSpace(Email);
}

/// <summary>
/// USAJOBS Search API adapter. One page only (Page=1, ResultsPerPage at most 25): no bulk harvesting, per
/// the provider terms in ADR 0015. A per-attempt timeout and one retry on a transient failure.
/// </summary>
public sealed class UsaJobsObservationSource : IJobObservationSource
{
    public const int MaxResultsPerPage = 25;
    public static readonly Uri SearchUri = new("https://data.usajobs.gov/api/search");
    public static readonly TimeSpan DefaultAttemptTimeout = TimeSpan.FromSeconds(5);

    private readonly HttpClient _http;
    private readonly UsaJobsOptions _options;
    private readonly TimeSpan _attemptTimeout;

    public UsaJobsObservationSource(HttpClient http, IOptions<UsaJobsOptions> options, TimeSpan? attemptTimeout = null)
    {
        _http = http;
        _options = options.Value;
        _attemptTimeout = attemptTimeout ?? DefaultAttemptTimeout;
    }

    public string SourceId => UsaJobsInfo.SourceId;
    public bool IsConfigured => _options.IsConfigured;
    public JobSourceInfo Info => UsaJobsInfo.Info;

    public async Task<IReadOnlyList<RawJobObservation>> FetchAsync(JobObservationQuery query, CancellationToken ct)
    {
        if (!IsConfigured)
        {
            throw new JobSourceUnavailableException("USAJOBS is not configured.");
        }

        var uri = new Uri(SearchUri, BuildQueryString(query));
        string? body = null;
        for (var attempt = 1; attempt <= 2 && body == null; attempt++)
        {
            try
            {
                body = await GetOnceAsync(uri, ct);
            }
            catch (TransientFailure ex) when (attempt == 1)
            {
                _ = ex; // one retry only
            }
            catch (TransientFailure ex)
            {
                throw new JobSourceUnavailableException("USAJOBS did not answer.", ex);
            }
        }
        return Parse(body!);
    }

    private sealed class TransientFailure : Exception
    {
        public TransientFailure(string message, Exception? inner = null) : base(message, inner)
        {
        }
    }

    private async Task<string> GetOnceAsync(Uri uri, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, uri);
        request.Headers.Host = "data.usajobs.gov";
        request.Headers.TryAddWithoutValidation("User-Agent", _options.Email!.Trim());
        request.Headers.TryAddWithoutValidation("Authorization-Key", _options.ApiKey!.Trim());

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(_attemptTimeout);
        try
        {
            using var response = await _http.SendAsync(request, timeout.Token);
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadAsStringAsync(timeout.Token);
            }
            if (response.StatusCode == HttpStatusCode.TooManyRequests || (int)response.StatusCode >= 500)
            {
                throw new TransientFailure($"USAJOBS answered {(int)response.StatusCode}.");
            }
            throw new JobSourceUnavailableException($"USAJOBS answered {(int)response.StatusCode}.");
        }
        catch (HttpRequestException ex)
        {
            throw new TransientFailure("USAJOBS could not be reached.", ex);
        }
        catch (OperationCanceledException ex) when (!ct.IsCancellationRequested)
        {
            throw new TransientFailure("USAJOBS timed out.", ex);
        }
    }

    private static string BuildQueryString(JobObservationQuery query)
    {
        var parts = new List<string>
        {
            // Never followed: one page, a fixed cap.
            $"ResultsPerPage={MaxResultsPerPage}",
            "Page=1"
        };
        if (!string.IsNullOrWhiteSpace(query.OccupationTitle))
        {
            parts.Add("Keyword=" + Uri.EscapeDataString(query.OccupationTitle.Trim()));
        }
        if (!string.IsNullOrWhiteSpace(query.AreaTitle) && query.AreaResolution is MarketResolutions.Metro or MarketResolutions.State)
        {
            parts.Add("LocationName=" + Uri.EscapeDataString(query.AreaTitle.Trim()));
        }
        return "?" + string.Join('&', parts);
    }

    /// <summary>Maps the documented search payload; anything unexpected is an outage, not a partial list.</summary>
    internal static IReadOnlyList<RawJobObservation> Parse(string body)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            var items = doc.RootElement.GetProperty("SearchResult").GetProperty("SearchResultItems");
            var result = new List<RawJobObservation>();
            foreach (var item in items.EnumerateArray().Take(MaxResultsPerPage))
            {
                result.Add(MapItem(item));
            }
            return result;
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException or FormatException)
        {
            throw new JobSourceUnavailableException("USAJOBS returned a payload this app does not understand.", ex);
        }
    }

    private static RawJobObservation MapItem(JsonElement item)
    {
        var id = Str(item, "MatchedObjectId") ?? throw new FormatException("Missing MatchedObjectId.");
        var d = item.GetProperty("MatchedObjectDescriptor");

        var locations = new List<RawJobLocation>();
        if (d.TryGetProperty("PositionLocation", out var locs) && locs.ValueKind == JsonValueKind.Array)
        {
            foreach (var l in locs.EnumerateArray())
            {
                locations.Add(new RawJobLocation(Str(l, "CityName") ?? Str(l, "City"), Str(l, "CountrySubDivisionCode") ?? Str(l, "State")));
            }
        }

        decimal? min = null, max = null;
        string? unit = null, basis = null;
        if (d.TryGetProperty("PositionRemuneration", out var pay) && pay.ValueKind == JsonValueKind.Array && pay.GetArrayLength() > 0)
        {
            var p = pay[0];
            min = Dec(Str(p, "MinimumRange"));
            max = Dec(Str(p, "MaximumRange"));
            (unit, basis) = Str(p, "RateIntervalCode")?.ToUpperInvariant() switch
            {
                "PA" => ("usd_per_year", "annual"),
                "PH" => ("usd_per_hour", "hourly"),
                _ => ((string?)null, (string?)null)
            };
        }

        var remote = JobRemoteStatus.Unknown;
        string? remoteNote = "The posting does not state a remote restriction.";
        JsonElement details = default;
        var hasDetails = d.TryGetProperty("UserArea", out var area) && area.ValueKind == JsonValueKind.Object
            && area.TryGetProperty("Details", out details) && details.ValueKind == JsonValueKind.Object;
        if (hasDetails && string.Equals(Str(details, "RemoteIndicator"), "true", StringComparison.OrdinalIgnoreCase))
        {
            remote = JobRemoteStatus.Eligible;
            remoteNote = "The posting states that remote work is available.";
        }

        string? series = null;
        if (d.TryGetProperty("JobCategory", out var cats) && cats.ValueKind == JsonValueKind.Array && cats.GetArrayLength() > 0)
        {
            series = Str(cats[0], "Code");
        }

        var grade = Str(d, "PositionGrade");
        if (grade == null && d.TryGetProperty("JobGrade", out var grades) && grades.ValueKind == JsonValueKind.Array && grades.GetArrayLength() > 0)
        {
            var code = Str(grades[0], "Code");
            var high = hasDetails ? Str(details, "HighGrade") : null;
            grade = code == null ? null : high == null ? code : $"{code}-{high}";
        }

        return new RawJobObservation(
            id, Str(d, "PositionTitle"), Str(d, "OrganizationName"), locations, min, max, unit, basis,
            Date(Str(d, "PublicationStartDate")), Date(Str(d, "ApplicationCloseDate")), remote, remoteNote,
            series, grade, Str(d, "PositionURI"));
    }

    private static string? Str(JsonElement e, string name) =>
        e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out var v)
            ? v.ValueKind switch
            {
                JsonValueKind.String => v.GetString(),
                JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False => v.ToString(),
                _ => null
            }
            : null;

    private static decimal? Dec(string? s) =>
        decimal.TryParse(s, NumberStyles.Number, CultureInfo.InvariantCulture, out var v) ? v : null;

    private static DateOnly? Date(string? s) =>
        DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var v) ? DateOnly.FromDateTime(v) : null;
}
