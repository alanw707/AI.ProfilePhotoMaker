using System.Globalization;
using System.Text;
using System.Text.Json;
using AI.ProfilePhotoMaker.API.Data;
using AI.ProfilePhotoMaker.API.Models.Career;
using AI.ProfilePhotoMaker.API.Services.Career.Export;
using AI.ProfilePhotoMaker.API.Services.Storage;
using Microsoft.EntityFrameworkCore;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Processing;

namespace AI.ProfilePhotoMaker.API.Services.Career;

// Shapes for docs/career/api-summaries-exports.md (ADR 0019).

public sealed class CreateExportRequest
{
    public string? Format { get; set; }
    public int? Version { get; set; }
    public bool? IncludePhoto { get; set; }
}

public sealed record CareerExportDto(Guid Id, string Format, int Version, bool IncludesPhoto, string FileName, DateTime ExpiresAt, string DownloadUrl);

public sealed record CareerExportListDto(IReadOnlyList<CareerExportDto> Exports);

public sealed record CareerExportFile(byte[] Content, string ContentType, string FileName);

public static class CareerExportErrorCodes
{
    public const string Expired = "CareerExportExpired";
    public const string NotFound = "CareerExportNotFound";
    public const string PhotoUnavailable = "CareerExportPhotoUnavailable";
}

public interface ICareerExportService
{
    Task<CareerOutcome<CareerExportDto>> CreateAsync(string ownerId, Guid materialId, CreateExportRequest request, CancellationToken ct = default);
    Task<CareerOutcome<CareerExportListDto>> ListAsync(string ownerId, Guid materialId, CancellationToken ct = default);
    Task<CareerOutcome<CareerExportFile>> DownloadAsync(string ownerId, Guid exportId, CancellationToken ct = default);
}

/// <summary>
/// Renders an accepted material version to PDF or DOCX (ADR 0019). Free: it is not a run and never touches the
/// allowance. Rows are private to the owner, expire after 24 hours and are removed when read after that.
/// </summary>
public sealed class CareerExportService : ICareerExportService
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromHours(24);
    private const int MaxActivePerOwner = 50;
    private const int MaxListed = 20;
    private const int PhotoPixels = 360;
    private const string PrivateSegment = "generated-private";

    private readonly ApplicationDbContext _db;
    private readonly IStorageService _storage;
    private readonly TimeProvider _clock;
    private readonly IReadOnlyDictionary<string, IMaterialExportRenderer> _renderers;

    public CareerExportService(ApplicationDbContext db, IStorageService storage, TimeProvider clock, IEnumerable<IMaterialExportRenderer> renderers)
    {
        _db = db;
        _storage = storage;
        _clock = clock;
        _renderers = renderers.ToDictionary(r => r.Format, StringComparer.Ordinal);
    }

    private DateTime Now() => _clock.GetUtcNow().UtcDateTime;

    private static DateTime Utc(DateTime value) => DateTime.SpecifyKind(value, DateTimeKind.Utc);

    private static CareerOutcome<T> Field<T>(string field, string message) =>
        CareerOutcome<T>.Invalid(new Dictionary<string, string> { [field] = message });

    private static CareerOutcome<T> MaterialMissing<T>() =>
        CareerOutcome<T>.NotFound(ResumeErrorCodes.MaterialNotFound, "That document was not found.");

    public static CareerExportDto ToDto(CareerExport row) =>
        new(row.Id, row.Format, row.Version, row.IncludesPhoto, row.FileName, Utc(row.ExpiresAt), $"/api/career/exports/{row.Id}");

    public async Task<CareerOutcome<CareerExportDto>> CreateAsync(string ownerId, Guid materialId, CreateExportRequest request, CancellationToken ct = default)
    {
        var startedAt = System.Diagnostics.Stopwatch.GetTimestamp();
        var material = await _db.CareerMaterials.AsNoTracking().FirstOrDefaultAsync(m => m.Id == materialId && m.OwnerId == ownerId, ct);
        if (material == null)
        {
            return MaterialMissing<CareerExportDto>();
        }
        var format = request.Format?.Trim().ToLowerInvariant();
        if (format == null || !_renderers.TryGetValue(format, out var renderer))
        {
            return Field<CareerExportDto>("format", "Choose pdf or docx.");
        }
        var number = request.Version ?? material.CurrentVersion;
        if (number < 1 || number > material.CurrentVersion)
        {
            return Field<CareerExportDto>("version", "Choose one of this document's saved versions.");
        }
        var version = await _db.CareerMaterialVersions.AsNoTracking()
            .FirstOrDefaultAsync(v => v.MaterialId == materialId && v.OwnerId == ownerId && v.Number == number, ct);
        if (version == null)
        {
            return Field<CareerExportDto>("version", "Choose one of this document's saved versions.");
        }

        var includePhoto = request.IncludePhoto == true;
        byte[]? photo = null;
        if (includePhoto)
        {
            photo = format == "pdf" ? await LoadPhotoAsync(ownerId, ct) : null;
            if (photo == null)
            {
                return CareerOutcome<CareerExportDto>.Invalid(new Dictionary<string, string>
                {
                    ["includePhoto"] = format == "pdf" ? "Choose a career photo first." : "Only a PDF can include a photo."
                }) with { ErrorCode = CareerExportErrorCodes.PhotoUnavailable };
            }
        }

        var now = Now();
        // Expired rows are removed whenever the owner exports, so they never pile up.
        _db.CareerExports.RemoveRange(await _db.CareerExports.Where(e => e.OwnerId == ownerId && e.ExpiresAt <= now).ToListAsync(ct));
        // Retention: the newest MaxActivePerOwner unexpired exports are kept; the new one takes a slot.
        var excess = await _db.CareerExports.Where(e => e.OwnerId == ownerId && e.ExpiresAt > now)
            .OrderByDescending(e => e.CreatedAt).ThenByDescending(e => e.Id)
            .Select(e => e.Id).Skip(MaxActivePerOwner - 1).ToListAsync(ct);
        if (excess.Count > 0)
        {
            _db.CareerExports.RemoveRange(await _db.CareerExports.Where(e => excess.Contains(e.Id)).ToListAsync(ct));
        }

        var document = await BuildDocumentAsync(material, version, photo, ct);
        byte[] bytes;
        try
        {
            bytes = renderer.Render(document);
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException or NotSupportedException or IOException)
        {
            return CareerOutcome<CareerExportDto>.Busy("CareerExportFailed", "We could not create that file. Try again.", 5);
        }
        if (bytes.Length > CareerExport.MaxBytes)
        {
            return Field<CareerExportDto>("version", "This document is too large to export.");
        }

        var row = new CareerExport
        {
            Id = Guid.NewGuid(),
            OwnerId = ownerId,
            MaterialId = materialId,
            Version = number,
            Format = format,
            IncludesPhoto = includePhoto,
            FileName = FileNameFor(material, number, format, includePhoto),
            Content = bytes,
            CreatedAt = now,
            ExpiresAt = now + Lifetime
        };
        _db.CareerExports.Add(row);
        _db.CareerUsageEvents.Add(CareerUsage.Event(
            ownerId, null, CareerUsageActions.Export, CareerUsageOutcomes.Ok, now,
            (int)System.Diagnostics.Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds));
        await _db.SaveChangesAsync(ct);
        return CareerOutcome<CareerExportDto>.Created(ToDto(row));
    }

    public async Task<CareerOutcome<CareerExportListDto>> ListAsync(string ownerId, Guid materialId, CancellationToken ct = default)
    {
        if (!await _db.CareerMaterials.AsNoTracking().AnyAsync(m => m.Id == materialId && m.OwnerId == ownerId, ct))
        {
            return MaterialMissing<CareerExportListDto>();
        }
        var now = Now();
        var rows = await _db.CareerExports.AsNoTracking()
            .Where(e => e.OwnerId == ownerId && e.MaterialId == materialId && e.ExpiresAt > now)
            .OrderByDescending(e => e.CreatedAt).Take(MaxListed)
            .Select(e => new CareerExport
            {
                Id = e.Id, Format = e.Format, Version = e.Version, IncludesPhoto = e.IncludesPhoto, FileName = e.FileName, ExpiresAt = e.ExpiresAt
            }).ToListAsync(ct);
        return CareerOutcome<CareerExportListDto>.Ok(new CareerExportListDto(rows.Select(ToDto).ToList()));
    }

    public async Task<CareerOutcome<CareerExportFile>> DownloadAsync(string ownerId, Guid exportId, CancellationToken ct = default)
    {
        var row = await _db.CareerExports.FirstOrDefaultAsync(e => e.Id == exportId && e.OwnerId == ownerId, ct);
        if (row == null)
        {
            return CareerOutcome<CareerExportFile>.NotFound(CareerExportErrorCodes.NotFound, "That export was not found.");
        }
        // Gone once it expires, or once the document it came from is gone; the bytes go with it.
        var sourceGone = !await _db.CareerMaterials.AnyAsync(m => m.Id == row.MaterialId && m.OwnerId == ownerId, ct);
        if (row.ExpiresAt <= Now() || sourceGone)
        {
            _db.CareerExports.Remove(row);
            await _db.SaveChangesAsync(ct);
            return CareerOutcome<CareerExportFile>.Gone(CareerExportErrorCodes.Expired, "This export has expired. Create a new one.");
        }
        var contentType = _renderers.TryGetValue(row.Format, out var renderer) ? renderer.ContentType : "application/octet-stream";
        return CareerOutcome<CareerExportFile>.Ok(new CareerExportFile(row.Content, contentType, row.FileName));
    }

    // ---- Document --------------------------------------------------------------

    private static string SectionHeading(string kind, string key) => key switch
    {
        ResumeSectionKeys.Summary => "Summary",
        ResumeSectionKeys.ExperienceHighlights => "Experience highlights",
        ResumeSectionKeys.Skills => "Skills",
        ResumeSectionKeys.Short => "Short bio",
        ResumeSectionKeys.Long => "Professional summary",
        _ => key
    };

    private async Task<MaterialExportDocument> BuildDocumentAsync(CareerMaterial material, CareerMaterialVersion version, byte[]? photo, CancellationToken ct)
    {
        var contact = JsonSerializer.Deserialize<ResumeContact>(version.ContactJson, ResumeJson.Options) ?? new ResumeContact();
        var sections = JsonSerializer.Deserialize<List<ResumeSection>>(version.SectionsJson, ResumeJson.Options) ?? new();
        var user = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == material.OwnerId, ct);
        var profile = contact.Location
            ? await _db.CareerProfileVersions.AsNoTracking().FirstOrDefaultAsync(v => v.OwnerId == material.OwnerId && v.VersionNumber == version.PinnedProfileVersion, ct)
            : null;

        var fullName = string.Join(" ", new[] { user?.FirstName, user?.LastName }.Where(p => !string.IsNullOrWhiteSpace(p)).Select(p => p!.Trim()));
        // The name is the document's title line; when the user turned it off, the document's own title stands in.
        var name = contact.Name && fullName.Length > 0 ? fullName : material.Title;

        var contactParts = new List<string>();
        if (contact.Email && !string.IsNullOrWhiteSpace(user?.Email))
        {
            contactParts.Add(user!.Email!);
        }
        if (contact.Phone && !string.IsNullOrWhiteSpace(user?.PhoneNumber))
        {
            contactParts.Add(user!.PhoneNumber!);
        }
        if (contact.Location && !string.IsNullOrWhiteSpace(profile?.Location))
        {
            contactParts.Add(profile!.Location!);
        }

        var exportSections = new List<ExportSection>();
        foreach (var key in ResumeSectionKeys.For(material.Kind))
        {
            var lines = sections.FirstOrDefault(s => s.Key == key)?.Lines.Select(l => ExportText.Clean(l.Text)).Where(t => t.Length > 0).ToList() ?? new();
            if (lines.Count == 0)
            {
                continue;
            }
            exportSections.Add(key switch
            {
                ResumeSectionKeys.Headline => new ExportSection(null, lines.Select(t => new ExportParagraph(t)).ToList()),
                ResumeSectionKeys.ExperienceHighlights => new ExportSection(SectionHeading(material.Kind, key), lines.Select(t => new ExportParagraph(t, Bullet: true)).ToList()),
                ResumeSectionKeys.Skills => new ExportSection(SectionHeading(material.Kind, key), new[] { new ExportParagraph(string.Join(", ", lines)) }),
                _ => new ExportSection(SectionHeading(material.Kind, key), lines.Select(t => new ExportParagraph(t)).ToList())
            });
        }
        return new MaterialExportDocument(
            material.Title, ExportText.Clean(name), contactParts.Count > 0 ? ExportText.Clean(string.Join(" \u00B7 ", contactParts)) : null, exportSections, photo);
    }

    private static string FileNameFor(CareerMaterial material, int version, string format, bool photo)
    {
        var slug = new StringBuilder();
        foreach (var c in material.Title.Normalize(NormalizationForm.FormD))
        {
            if (c < 128 && char.IsLetterOrDigit(c))
            {
                slug.Append(c);
            }
            else if (slug.Length > 0 && slug[^1] != '-' && (c is ' ' or '-' or '_'))
            {
                slug.Append('-');
            }
            if (slug.Length >= 60)
            {
                break;
            }
        }
        var stem = slug.ToString().Trim('-');
        if (stem.Length == 0)
        {
            stem = material.Kind;
        }
        return $"{stem}-v{version.ToString(CultureInfo.InvariantCulture)}{(photo ? "-with-photo" : "")}.{format}";
    }

    // ---- Photo -----------------------------------------------------------------

    /// <summary>The owner's chosen career photo as a small JPEG, or null when it is missing, a preview, private or unreadable.</summary>
    private async Task<byte[]?> LoadPhotoAsync(string ownerId, CancellationToken ct)
    {
        var selection = await _db.CareerPhotoSelections.AsNoTracking().FirstOrDefaultAsync(s => s.OwnerId == ownerId, ct);
        if (selection == null)
        {
            return null;
        }
        var image = await _db.ProcessedImages.AsNoTracking()
            .Where(i => i.Id == selection.ProcessedImageId && i.UserProfile.UserId == ownerId && i.IsGenerated && !i.IsOriginalUpload
                && i.GenerationStatus != null && i.GenerationStatus.ToLower() == "succeeded" && i.ProcessedImageUrl != ""
                && (i.RawImageStoragePath == null || i.RawImageStoragePath == "")
                && !i.ProcessedImageUrl.ToLower().Contains(PrivateSegment))
            .Select(i => i.ProcessedImageUrl).FirstOrDefaultAsync(ct);
        if (image == null)
        {
            return null;
        }
        try
        {
            await using var source = await _storage.GetImageAsync(image);
            if (source == null)
            {
                return null;
            }
            using var loaded = await Image.LoadAsync(source, ct);
            loaded.Mutate(x => x.AutoOrient().Resize(new ResizeOptions { Size = new Size(PhotoPixels, PhotoPixels), Mode = ResizeMode.Max }));
            using var output = new MemoryStream();
            await loaded.SaveAsJpegAsync(output, new JpegEncoder { Quality = 85 }, ct);
            return output.ToArray();
        }
        catch (Exception ex) when (ex is UnknownImageFormatException or InvalidImageContentException or IOException or NotSupportedException or HttpRequestException)
        {
            return null;
        }
    }
}
