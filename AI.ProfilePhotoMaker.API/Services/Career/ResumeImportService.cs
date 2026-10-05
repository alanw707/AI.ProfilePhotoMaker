using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using AI.ProfilePhotoMaker.API.Data;
using AI.ProfilePhotoMaker.API.Models.Career;
using AI.ProfilePhotoMaker.API.Services.Storage;
using Microsoft.EntityFrameworkCore;

namespace AI.ProfilePhotoMaker.API.Services.Career;

public interface IResumeImportService
{
    Task<CareerOutcome<ResumeDocumentDto>> UploadAsync(string ownerId, byte[]? content, string? fileName, bool consent, string? consentVersion, CancellationToken ct = default);
    Task<CareerOutcome<IReadOnlyList<ResumeDocumentDto>>> ListAsync(string ownerId, CancellationToken ct = default);
    Task<CareerOutcome<ResumeDocumentDto>> GetAsync(string ownerId, Guid id, CancellationToken ct = default);
    Task<CareerOutcome<ResumeFileResult>> GetFileAsync(string ownerId, Guid id, CancellationToken ct = default);
    Task<CareerOutcome<bool>> DeleteAsync(string ownerId, Guid id, CancellationToken ct = default);

    /// <summary>Deletes raw files (and their rows) that expired at or before <paramref name="now"/>; returns how many.</summary>
    Task<int> PurgeExpiredResumesAsync(DateTime now, CancellationToken ct = default);
}

/// <summary>
/// Resume upload pipeline (ADR 0007): inspect → store privately → scan → extract →
/// proposal. It runs synchronously in this slice. Logs carry ids and codes only,
/// never document text or file names.
/// </summary>
public sealed class ResumeImportService : IResumeImportService
{
    public const string KeyPrefix = "career-private/resumes/";
    public const int MaxListed = 20;
    public const int ScannerRetryAfterSeconds = 60;
    public const string RetentionConfigKey = "Career:ResumeRetentionDays";
    public const string TimeoutConfigKey = "Career:ExtractionTimeoutSeconds";
    public const string ConsentVersionConfigKey = "Career:ResumeConsentVersion";

    /// <summary>The notice the UI shows next to the consent box; bump it when the wording changes.</summary>
    public const string DefaultConsentVersion = "resume-notice-2026-10-04";
    private const int DefaultRetentionDays = 30;
    private const double DefaultTimeoutSeconds = 15;
    private const int PurgeBatch = 200;

    private readonly ApplicationDbContext _db;
    private readonly IStorageService _storage;
    private readonly IResumeParser _parser;
    private readonly IMalwareScanner? _scanner;
    private readonly IConfiguration _configuration;
    private readonly TimeProvider _clock;
    private readonly ILogger<ResumeImportService> _logger;

    public ResumeImportService(
        ApplicationDbContext db,
        IStorageService storage,
        IResumeParser parser,
        IEnumerable<IMalwareScanner> scanners,
        IConfiguration configuration,
        TimeProvider clock,
        ILogger<ResumeImportService> logger)
    {
        _db = db;
        _storage = storage;
        _parser = parser;
        // No registered scanner (production until one is licensed) means fail closed.
        _scanner = scanners.LastOrDefault();
        _configuration = configuration;
        _clock = clock;
        _logger = logger;
    }

    public async Task<CareerOutcome<ResumeDocumentDto>> UploadAsync(
        string ownerId, byte[]? content, string? fileName, bool consent, string? consentVersion, CancellationToken ct = default)
    {
        var errors = new Dictionary<string, string>();
        if (!consent)
        {
            errors["consent"] = "Confirm that you agree to us processing this file.";
        }
        else if (!string.Equals(consentVersion, CurrentConsentVersion(), StringComparison.Ordinal))
        {
            // The user agreed to older wording; make them read the current notice.
            errors["consent"] = "The notice changed; read it again.";
        }
        if (content == null || content.Length == 0)
        {
            errors["file"] = "Choose a PDF or DOCX file.";
        }
        if (errors.Count > 0)
        {
            return CareerOutcome<ResumeDocumentDto>.Invalid(errors);
        }

        var bytes = content!;
        var inspection = ResumeFileInspector.Inspect(bytes);
        if (inspection.Rejection is { } rejection)
        {
            _logger.LogInformation("Resume upload rejected before storage: {Kind}", rejection);
            return rejection == CareerOutcomeKind.TooLarge
                ? CareerOutcome<ResumeDocumentDto>.TooLarge(inspection.Detail!)
                : CareerOutcome<ResumeDocumentDto>.Unsupported(inspection.Detail!);
        }

        if (_scanner == null)
        {
            _logger.LogWarning("Resume upload refused: no malware scanner is registered ({Code})", CareerErrorCodes.ScannerUnavailable);
            return CareerOutcome<ResumeDocumentDto>.ScannerUnavailable(ScannerRetryAfterSeconds);
        }

        var format = inspection.Format!.Value;
        var now = Now();
        var id = Guid.NewGuid();
        var document = new ResumeDocument
        {
            Id = id,
            OwnerId = ownerId,
            StorageKey = KeyPrefix + Guid.NewGuid().ToString("N"),
            FileName = SanitiseFileName(fileName, format),
            Format = format == ResumeFormat.Pdf ? "pdf" : "docx",
            SizeBytes = bytes.Length,
            Sha256 = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant(),
            PageCount = inspection.PageCount,
            State = ResumeState.Quarantined,
            ConsentedAt = now,
            ConsentVersion = CurrentConsentVersion(),
            UploadedAt = now,
            ExpiresAt = now.AddDays(RetentionDays()),
            CreatedAt = now
        };

        // Row first, blob second: a crash between them leaves a row the purge job
        // clears by expiry, never an unowned blob nothing can find.
        _db.CareerResumeDocuments.Add(document);
        await _db.SaveChangesAsync(ct);

        try
        {
            using var stream = new MemoryStream(bytes, writable: false);
            await _storage.SaveImageToPathAsync(stream, document.StorageKey);
        }
        catch
        {
            // Without a blob the row would point at nothing.
            _db.CareerResumeDocuments.Remove(document);
            await _db.SaveChangesAsync(CancellationToken.None);
            throw;
        }

        var scan = await ScanAsync(document, bytes, ct);
        if (scan != MalwareScanResult.Clean)
        {
            await DiscardAsync(document);
            return scan == MalwareScanResult.Threat
                ? CareerOutcome<ResumeDocumentDto>.Rejected()
                : CareerOutcome<ResumeDocumentDto>.ScannerUnavailable(ScannerRetryAfterSeconds);
        }

        try
        {
            return await ProcessScannedAsync(document, bytes, format, now, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            _db.ChangeTracker.Clear();
            await DiscardAsync(document);
            throw;
        }
        catch (Exception ex)
        {
            // A parser or extractor bug is the file's failure, not a 500. Exception text
            // can quote document content, so only the type is logged.
            _logger.LogWarning("Resume {ResumeId} failed after scanning ({Code}, {ExceptionType})",
                document.Id, ResumeFailureCodes.ParserError, ex.GetType().Name);
            _db.ChangeTracker.Clear();
            _db.CareerResumeDocuments.Update(document);
            return await FinishAsync(document, ResumeState.Failed, ResumeFailureCodes.ParserError, ct);
        }
    }

    private async Task<CareerOutcome<ResumeDocumentDto>> ProcessScannedAsync(
        ResumeDocument document, byte[] bytes, ResumeFormat format, DateTime now, CancellationToken ct)
    {
        document.State = ResumeState.Extracting;
        var (parsed, failureCode) = await ParseAsync(document, bytes, format, ct);
        if (parsed == null)
        {
            return await FinishAsync(document, ResumeState.Failed, failureCode, ct);
        }

        // Page and character limits are enforced on the parser's own answer too, as
        // the cheap pre-check cannot see inside every document.
        document.PageCount = Math.Max(parsed.PageCount, 1);
        if (parsed.PageCount > ResumeFileInspector.MaxPages)
        {
            await DiscardAsync(document);
            return CareerOutcome<ResumeDocumentDto>.TooLarge($"The document has more than {ResumeFileInspector.MaxPages} pages.");
        }
        if (parsed.Pages.Sum(p => p.Length) > ResumeFileInspector.MaxCharacters)
        {
            await DiscardAsync(document);
            return CareerOutcome<ResumeDocumentDto>.TooLarge("The document has more than 100,000 characters of text.");
        }

        if (parsed.Pages.All(string.IsNullOrWhiteSpace))
        {
            // Scanned or image-only: no OCR is offered, so the user pastes instead.
            return await FinishAsync(document, ResumeState.Unreadable, null, ct);
        }

        var items = ResumeFactExtractor.Extract(parsed.Pages, pasted: false, now);
        var proposal = await ProposalStore.AddAsync(_db, document.OwnerId, ProposalSources.Resume, document.Id, items, now, ct);
        document.ProposalId = proposal.Id;
        return await FinishAsync(document, ResumeState.Ready, null, ct);
    }

    public async Task<CareerOutcome<IReadOnlyList<ResumeDocumentDto>>> ListAsync(string ownerId, CancellationToken ct = default)
    {
        var rows = await _db.CareerResumeDocuments.AsNoTracking()
            .Where(d => d.OwnerId == ownerId)
            .OrderByDescending(d => d.UploadedAt)
            .Take(MaxListed)
            .ToListAsync(ct);
        return CareerOutcome<IReadOnlyList<ResumeDocumentDto>>.Ok(rows.Select(ToDto).ToList());
    }

    public async Task<CareerOutcome<ResumeDocumentDto>> GetAsync(string ownerId, Guid id, CancellationToken ct = default)
    {
        var row = await FindAsync(ownerId, id, ct);
        return row == null ? NotFound<ResumeDocumentDto>() : CareerOutcome<ResumeDocumentDto>.Ok(ToDto(row));
    }

    public async Task<CareerOutcome<ResumeFileResult>> GetFileAsync(string ownerId, Guid id, CancellationToken ct = default)
    {
        var row = await FindAsync(ownerId, id, ct);
        if (row == null)
        {
            return NotFound<ResumeFileResult>();
        }

        // Failed and unreadable uploads have their raw file deleted (ADR 0007).
        var stream = row.State is ResumeState.Failed or ResumeState.Unreadable
            ? null
            : await _storage.GetImageAsync(row.StorageKey);
        if (stream == null)
        {
            return CareerOutcome<ResumeFileResult>.NotFound(CareerErrorCodes.ResumeFileGone, "The original file is no longer stored.");
        }

        var contentType = row.Format == "pdf"
            ? "application/pdf"
            : "application/vnd.openxmlformats-officedocument.wordprocessingml.document";
        return CareerOutcome<ResumeFileResult>.Ok(new ResumeFileResult(stream, contentType, row.FileName));
    }

    public async Task<CareerOutcome<bool>> DeleteAsync(string ownerId, Guid id, CancellationToken ct = default)
    {
        var row = await _db.CareerResumeDocuments.FirstOrDefaultAsync(d => d.Id == id && d.OwnerId == ownerId, ct);
        if (row == null)
        {
            return NotFound<bool>();
        }

        await _storage.DeleteImageAsync(row.StorageKey);
        await RemoveRowsAsync(row, ct);
        _logger.LogInformation("Resume {ResumeId} deleted by owner", id);
        return CareerOutcome<bool>.Ok(true);
    }

    public async Task<int> PurgeExpiredResumesAsync(DateTime now, CancellationToken ct = default)
    {
        var expired = await _db.CareerResumeDocuments
            .Where(d => d.ExpiresAt <= now)
            .OrderBy(d => d.ExpiresAt)
            .Take(PurgeBatch)
            .ToListAsync(ct);

        var purged = 0;
        foreach (var row in expired)
        {
            try
            {
                await _storage.DeleteImageAsync(row.StorageKey);
                await RemoveRowsAsync(row, ct);
                purged++;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Keep the row so the next run retries the raw file. Drop whatever this
                // row left tracked so it cannot fail the saves of the rows after it.
                _db.ChangeTracker.Clear();
                _logger.LogWarning(ex, "Could not purge expired resume {ResumeId}", row.Id);
            }
        }

        if (purged > 0)
        {
            _logger.LogInformation("Purged {Count} expired resumes", purged);
        }
        return purged;
    }

    // ---- Pipeline steps ----------------------------------------------------

    private async Task<MalwareScanResult> ScanAsync(ResumeDocument document, byte[] bytes, CancellationToken ct)
    {
        try
        {
            return await _scanner!.ScanAsync(bytes, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            await DiscardAsync(document);
            throw;
        }
        catch (Exception ex)
        {
            // Any scanner failure is an outage: never treat it as clean.
            _logger.LogWarning(ex, "Malware scan failed for resume {ResumeId} ({Code})", document.Id, CareerErrorCodes.ScannerUnavailable);
            return MalwareScanResult.Unavailable;
        }
    }

    private async Task<(ResumeParseResult? Result, string? FailureCode)> ParseAsync(
        ResumeDocument document, byte[] bytes, ResumeFormat format, CancellationToken ct)
    {
        var timeout = TimeSpan.FromSeconds(_configuration.GetValue<double?>(TimeoutConfigKey) ?? DefaultTimeoutSeconds);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct);
        linked.CancelAfter(timeout);

        try
        {
            // WaitAsync enforces the limit even if a parser ignores its token.
            var result = await Task.Run(() => _parser.ParseAsync(bytes, format, linked.Token), CancellationToken.None)
                .WaitAsync(timeout, ct);
            return (result, null);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // UploadAsync discards the document.
            throw;
        }
        catch (Exception ex) when (ex is TimeoutException or OperationCanceledException)
        {
            _logger.LogWarning("Resume {ResumeId} extraction timed out ({Code})", document.Id, ResumeFailureCodes.ExtractionTimeout);
            return (null, ResumeFailureCodes.ExtractionTimeout);
        }
        catch (Exception ex)
        {
            // Exception text can quote document content, so only its type is logged.
            _logger.LogWarning("Resume {ResumeId} could not be parsed ({Code}, {ExceptionType})",
                document.Id, ResumeFailureCodes.ParserError, ex.GetType().Name);
            return (null, ResumeFailureCodes.ParserError);
        }
    }

    private async Task<CareerOutcome<ResumeDocumentDto>> FinishAsync(
        ResumeDocument document, ResumeState state, string? failureCode, CancellationToken ct)
    {
        document.State = state;
        document.FailureCode = failureCode;

        // Failed and unreadable files have nothing worth keeping: the user gets the
        // paste fallback, so the raw file goes now instead of at expiry.
        if (state is ResumeState.Failed or ResumeState.Unreadable)
        {
            try
            {
                await _storage.DeleteImageAsync(document.StorageKey);
            }
            catch (Exception ex)
            {
                // The row stays, so the purge job retries by expiry.
                _logger.LogError(ex, "Could not delete raw file of {State} resume {ResumeId}", state, document.Id);
            }
        }

        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("Resume {ResumeId} finished as {State} {FailureCode}", document.Id, state, failureCode);
        return CareerOutcome<ResumeDocumentDto>.Created(ToDto(document));
    }

    /// <summary>Removes the raw file and row of an upload that must not be kept.</summary>
    private async Task DiscardAsync(ResumeDocument document)
    {
        try
        {
            await _storage.DeleteImageAsync(document.StorageKey);
        }
        catch (Exception ex)
        {
            // The row stays so the purge job still finds the raw file.
            _logger.LogError(ex, "Could not delete raw file of discarded resume {ResumeId}", document.Id);
            return;
        }

        await RemoveRowsAsync(document, CancellationToken.None);
        _logger.LogInformation("Resume {ResumeId} discarded", document.Id);
    }

    /// <summary>
    /// Removes a document and its pending proposals. Accepted or dismissed proposals
    /// stay as the history behind a profile version, minus the link to the file.
    /// </summary>
    private async Task RemoveRowsAsync(ResumeDocument document, CancellationToken ct)
    {
        var proposals = await _db.CareerProfileProposals
            .Where(p => p.OwnerId == document.OwnerId && p.ResumeDocumentId == document.Id)
            .ToListAsync(ct);

        foreach (var proposal in proposals)
        {
            if (proposal.Status == ProposalStatus.Pending)
            {
                _db.CareerProfileProposalItems.RemoveRange(
                    await _db.CareerProfileProposalItems.Where(i => i.ProposalId == proposal.Id).ToListAsync(ct));
                _db.CareerProfileProposals.Remove(proposal);
            }
            else
            {
                proposal.ResumeDocumentId = null;
            }
        }

        _db.CareerResumeDocuments.Remove(document);
        await _db.SaveChangesAsync(ct);
    }

    // ---- Helpers -------------------------------------------------------------

    private DateTime Now() => _clock.GetUtcNow().UtcDateTime;

    private string CurrentConsentVersion() =>
        _configuration[ConsentVersionConfigKey] is { Length: > 0 } configured ? configured : DefaultConsentVersion;

    private int RetentionDays() => Math.Max(1, _configuration.GetValue<int?>(RetentionConfigKey) ?? DefaultRetentionDays);

    private Task<ResumeDocument?> FindAsync(string ownerId, Guid id, CancellationToken ct) =>
        _db.CareerResumeDocuments.AsNoTracking().FirstOrDefaultAsync(d => d.Id == id && d.OwnerId == ownerId, ct);

    private static CareerOutcome<T> NotFound<T>() =>
        CareerOutcome<T>.NotFound(CareerErrorCodes.ResumeNotFound, "That resume was not found.");

    private const int MaxFileNameLength = 200;

    /// <summary>
    /// Keeps a readable display name: no path, no control or format characters (such
    /// as the right-to-left override U+202E that disguises extensions), at most 200
    /// characters, extension kept, no split surrogate pair.
    /// </summary>
    internal static string SanitiseFileName(string? name, ResumeFormat format)
    {
        var fallback = format == ResumeFormat.Pdf ? "resume.pdf" : "resume.docx";
        var leaf = (name ?? string.Empty).Replace('\\', '/');
        leaf = leaf[(leaf.LastIndexOf('/') + 1)..];

        var builder = new StringBuilder();
        foreach (var c in leaf)
        {
            var category = char.GetUnicodeCategory(c);
            if (category is not (UnicodeCategory.Control or UnicodeCategory.Format)
                && c != '"' && c != '<' && c != '>' && c != '|' && c != ':' && c != '*' && c != '?')
            {
                builder.Append(c);
            }
        }

        var clean = builder.ToString().Trim();
        if (clean.Length > MaxFileNameLength)
        {
            // Cut the stem, not the extension, so the file still looks like what it is.
            var dot = clean.LastIndexOf('.');
            var extension = dot > 0 && clean.Length - dot <= 10 ? clean[dot..] : string.Empty;
            var stemLength = MaxFileNameLength - extension.Length;
            if (char.IsHighSurrogate(clean[stemLength - 1]))
            {
                stemLength--;
            }
            clean = clean[..stemLength] + extension;
        }
        return clean.Length == 0 ? fallback : clean;
    }

    private static ResumeDocumentDto ToDto(ResumeDocument d) => new(
        d.Id, d.FileName, d.Format, d.SizeBytes, d.PageCount, d.State.ToString().ToLowerInvariant(), d.FailureCode, d.ProposalId, d.ConsentVersion,
        DateTime.SpecifyKind(d.UploadedAt, DateTimeKind.Utc), DateTime.SpecifyKind(d.ExpiresAt, DateTimeKind.Utc));
}
