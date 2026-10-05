using System.Security.Cryptography;
using System.Text;
using AI.ProfilePhotoMaker.API.Data;
using AI.ProfilePhotoMaker.API.Models.Career;
using Microsoft.EntityFrameworkCore;

namespace AI.ProfilePhotoMaker.API.Services.Career;

public sealed record CareerActionUsageDto(string Action, int Count, int Failures, decimal CostUsd, int LatencyP50Ms, int LatencyP95Ms);
public sealed record CareerUserDistributionDto(int ActiveUsers, int EventsP50, int EventsP95, int EventsMax, decimal CostUsdP50, decimal CostUsdP95, decimal CostUsdMax);
public sealed record CareerTopUserDto(string UserHash, int Events, decimal CostUsd);

public sealed record CareerUsageReportDto(
    DateTime From, DateTime To, int TotalEvents, int Failures, double FailureRate, decimal TotalCostUsd,
    IReadOnlyList<CareerActionUsageDto> Actions, CareerUserDistributionDto PerActiveUser, IReadOnlyList<CareerTopUserDto> TopUsers);

public interface ICareerUsageReportService
{
    Task<CareerUsageReportDto> BuildAsync(DateTime from, DateTime to, CancellationToken ct = default);
}

/// <summary>Operator report over the usage ledger (ADR 0022). Owners appear only as a short hash.</summary>
public sealed class CareerUsageReportService : ICareerUsageReportService
{
    private readonly ApplicationDbContext _db;

    public CareerUsageReportService(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<CareerUsageReportDto> BuildAsync(DateTime from, DateTime to, CancellationToken ct = default)
    {
        var rows = await _db.CareerUsageEvents.AsNoTracking()
            .Where(e => e.CreatedAt >= from && e.CreatedAt < to)
            .Select(e => new { e.OwnerId, e.Action, e.CostCents, e.LatencyMs, e.Outcome })
            .ToListAsync(ct);

        var actions = rows.GroupBy(r => r.Action).OrderBy(g => g.Key).Select(g =>
        {
            var latencies = g.Select(r => r.LatencyMs).Order().ToList();
            return new CareerActionUsageDto(
                g.Key, g.Count(), g.Count(r => r.Outcome != CareerUsageOutcomes.Ok), Usd(g.Sum(r => r.CostCents)),
                Percentile(latencies, 50), Percentile(latencies, 95));
        }).ToList();

        var users = rows.GroupBy(r => r.OwnerId).Select(g => (Owner: g.Key, Events: g.Count(), Cents: g.Sum(r => r.CostCents))).ToList();
        var events = users.Select(u => u.Events).Order().ToList();
        var cents = users.Select(u => u.Cents).Order().ToList();
        var distribution = new CareerUserDistributionDto(
            users.Count, Percentile(events, 50), Percentile(events, 95), events.LastOrDefault(),
            Usd(Percentile(cents, 50)), Usd(Percentile(cents, 95)), Usd(cents.LastOrDefault()));

        var failures = rows.Count(r => r.Outcome != CareerUsageOutcomes.Ok);
        return new CareerUsageReportDto(
            from, to, rows.Count, failures, rows.Count == 0 ? 0 : (double)failures / rows.Count, Usd(rows.Sum(r => r.CostCents)),
            actions, distribution,
            users.OrderByDescending(u => u.Cents).ThenByDescending(u => u.Events).Take(10)
                .Select(u => new CareerTopUserDto(Hash(u.Owner), u.Events, Usd(u.Cents))).ToList());
    }

    private static decimal Usd(int cents) => cents / 100m;

    /// <summary>Nearest-rank percentile of an ascending list; 0 when empty.</summary>
    internal static int Percentile(IReadOnlyList<int> sorted, int percentile) =>
        sorted.Count == 0 ? 0 : sorted[Math.Max(0, (int)Math.Ceiling(percentile / 100.0 * sorted.Count) - 1)];

    private static string Hash(string owner) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(owner)))[..12].ToLowerInvariant();
}
