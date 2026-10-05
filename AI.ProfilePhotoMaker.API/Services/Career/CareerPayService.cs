using System.Text.Json;
using AI.ProfilePhotoMaker.API.Data;
using AI.ProfilePhotoMaker.API.Models.Career;
using Microsoft.EntityFrameworkCore;

namespace AI.ProfilePhotoMaker.API.Services.Career;

public interface ICareerPayService
{
    Task<CareerOutcome<CareerPayAnalysisListDto>> ListAsync(string owner, CancellationToken ct);
    Task<CareerOutcome<CareerPayAnalysisDto>> GetAsync(string owner, Guid id, CancellationToken ct);
    Task<CareerOutcome<PayRecomputeDto>> RecomputeAsync(string owner, Guid id, CancellationToken ct);
    PayQualificationDto Qualification();
}

public sealed class CareerPayService : ICareerPayService
{
    private readonly ApplicationDbContext _db;
    private readonly IMarketReference _reference;
    private readonly IPayObservationSource _source;
    public CareerPayService(ApplicationDbContext db, IMarketReference reference, IPayObservationSource source)
    { _db = db; _reference = reference; _source = source; }

    public PayQualificationDto Qualification()
    {
        var gates = PayEvidenceGates.Current();
        return new PayQualificationDto(gates.PersonalizedAllowed, gates.BlockedReasons, gates.Rows);
    }
    private static CareerOutcome<T> Missing<T>() => CareerOutcome<T>.NotFound("CareerPayAnalysisNotFound", "That pay analysis was not found.");
    private Task<CareerPayAnalysis?> Find(string owner, Guid id, CancellationToken ct) =>
        _db.CareerPayAnalyses.AsNoTracking().FirstOrDefaultAsync(a => a.Id == id && a.OwnerId == owner, ct);

    private async Task<List<string>> Stale(CareerPayAnalysis row, CancellationToken ct)
    {
        var reasons = new List<string>();
        var profile = await _db.CareerProfiles.AsNoTracking().Where(p => p.OwnerId == row.OwnerId)
            .Select(p => (int?)p.ActiveVersionNumber).FirstOrDefaultAsync(ct);
        var goalVersion = await _db.CareerGoals.AsNoTracking().Where(g => g.OwnerId == row.OwnerId)
            .Select(g => (int?)g.ActiveVersionNumber).FirstOrDefaultAsync(ct);
        var goal = await _db.CareerGoalVersions.AsNoTracking()
            .FirstOrDefaultAsync(g => g.OwnerId == row.OwnerId && g.VersionNumber == goalVersion, ct);
        if (goalVersion != row.PinnedGoalVersion) reasons.Add("goal_changed");
        if (profile != row.PinnedProfileVersion) reasons.Add("profile_changed");
        if (goal?.OccupationCode != row.OccupationCode) reasons.Add("occupation_changed");
        var location = MarketBriefBuilder.ResolveLocation(goal?.TargetLocation, _reference);
        if (location.Resolution != row.AreaResolution || location.Local?.Code != row.AreaCode) reasons.Add("area_changed");
        return reasons;
    }

    public async Task<CareerOutcome<CareerPayAnalysisListDto>> ListAsync(string owner, CancellationToken ct)
    {
        var rows = await _db.CareerPayAnalyses.AsNoTracking().Where(a => a.OwnerId == owner)
            .OrderByDescending(a => a.CreatedAt).Take(20).ToListAsync(ct);
        var items = new List<CareerPayAnalysisSummaryDto>();
        foreach (var a in rows)
        {
            using var json = JsonDocument.Parse(a.SectionsJson);
            items.Add(new CareerPayAnalysisSummaryDto(a.Id, a.OccupationCode, a.OccupationTitle,
                a.AreaTitle ?? (a.AreaResolution == MarketResolutions.NationalOnly ? "U.S." : null), a.Status,
                json.RootElement[1].GetProperty("status").GetString() == "complete", (await Stale(a, ct)).Count > 0,
                DateTime.SpecifyKind(a.CreatedAt, DateTimeKind.Utc)));
        }
        return CareerOutcome<CareerPayAnalysisListDto>.Ok(new(items));
    }

    public async Task<CareerOutcome<CareerPayAnalysisDto>> GetAsync(string owner, Guid id, CancellationToken ct)
    {
        var a = await Find(owner, id, ct);
        if (a == null) return Missing<CareerPayAnalysisDto>();
        var reasons = await Stale(a, ct);
        var published = _reference.Oews?.Crosswalk(a.OccupationCode);
        var qualification = JsonSerializer.Deserialize<PayQualificationDto>(a.QualificationJson, MarketBriefJson.Options)!;
        return CareerOutcome<CareerPayAnalysisDto>.Ok(new(a.Id, a.RunId, a.Status,
            new(a.OccupationCode, a.OccupationTitle, published?.Code, published?.Match),
            new(a.LocationInput ?? "", a.AreaResolution, a.AreaCode == null ? null :
                new MarketAreaDto(a.AreaCode, a.AreaTitle ?? "", a.AreaResolution)),
            new(a.PinnedProfileVersion, a.PinnedGoalVersion, a.OewsRelease, a.OewsSnapshotSha256,
                a.ProjectionsRelease, a.RuleVersion, a.ObservationSourceId), a.InputHash, reasons.Count > 0, reasons,
            ReadSections(a.SectionsJson), qualification.BlockedReasons, qualification,
            JsonSerializer.Deserialize<List<MarketSourceDto>>(a.SourcesJson, MarketBriefJson.Options)!,
            DateTime.SpecifyKind(a.CreatedAt, DateTimeKind.Utc)));
    }

    private static IReadOnlyList<object> ReadSections(string json) =>
        JsonSerializer.Deserialize<List<JsonElement>>(json, MarketBriefJson.Options)!.Cast<object>().ToList();

    public async Task<CareerOutcome<PayRecomputeDto>> RecomputeAsync(string owner, Guid id, CancellationToken ct)
    {
        var a = await Find(owner, id, ct);
        if (a == null) return Missing<PayRecomputeDto>();
        IReadOnlyList<PayObservation> rows;
        var sourceFailed = false;
        try { rows = _source.ObservationsFor(a.OccupationCode, a.AreaCode ?? "99"); }
        catch (Exception) when (!ct.IsCancellationRequested)
        {
            rows = Array.Empty<PayObservation>();
            sourceFailed = true;
        }
        var input = new PayAnalysisInput(a.PinnedProfileVersion, a.PinnedGoalVersion, a.OccupationCode,
            a.OccupationTitle, a.LocationInput, a.RequestedAnnual, a.AreaCode, a.AreaTitle, a.AreaResolution,
            a.OewsRelease, a.OewsSnapshotSha256, a.ProjectionsRelease, a.RuleVersion, _source.SourceId,
            rows, DateTime.SpecifyKind(a.CreatedAt, DateTimeKind.Utc));
        var built = PayAnalysisBuilder.Build(input, _reference, sourceFailed);
        var sections = PayAnalysisBuilder.AsList(built.Sections);
        var json = JsonSerializer.Serialize(sections, MarketBriefJson.Options);
        var differences = new List<string>();
        if (built.InputHash != a.InputHash)
        {
            differences.Add("inputHash");
            using var original = JsonDocument.Parse(a.InputJson);
            using var fresh = JsonDocument.Parse(PayAnalysisBuilder.CanonicalInputJson(input));
            foreach (var field in fresh.RootElement.EnumerateObject())
                if (!original.RootElement.TryGetProperty(field.Name, out var old) || old.GetRawText() != field.Value.GetRawText())
                    differences.Add(field.Name);
        }
        if (a.ObservationCount != rows.Count) differences.Add("observationCount");
        if (a.ObservationSourceId != _source.SourceId) differences.Add("observationSourceId");
        if (json != a.SectionsJson) differences.Add("sections");
        return CareerOutcome<PayRecomputeDto>.Ok(new(differences.Count == 0, built.InputHash, a.InputHash, differences, sections));
    }
}
