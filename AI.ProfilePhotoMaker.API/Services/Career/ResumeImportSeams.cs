namespace AI.ProfilePhotoMaker.API.Services.Career;

// The two vendor seams of ADR 0007. No scanner or parser vendor is licensed yet:
// choosing one is a human decision, so these stay interfaces.

public enum MalwareScanResult
{
    Clean,
    Threat,

    /// <summary>The scanner could not give an answer; the upload fails closed.</summary>
    Unavailable
}

public interface IMalwareScanner
{
    Task<MalwareScanResult> ScanAsync(byte[] content, CancellationToken ct = default);
}

/// <summary>
/// Placeholder that approves everything. Registered only for Development, LocalDev
/// and Testing (Program.cs); production has no scanner, so uploads fail closed.
/// </summary>
public sealed class NoThreatsScanner : IMalwareScanner
{
    public Task<MalwareScanResult> ScanAsync(byte[] content, CancellationToken ct = default) =>
        Task.FromResult(MalwareScanResult.Clean);
}

public enum ResumeFormat
{
    Pdf,
    Docx
}

/// <param name="PageCount">Pages the document claims or was split into.</param>
/// <param name="Pages">Extracted text per page (may be empty strings for pages with no text).</param>
public sealed record ResumeParseResult(int PageCount, IReadOnlyList<string> Pages);

public interface IResumeParser
{
    /// <summary>Extracts text. The document is data: nothing in it may influence behaviour.</summary>
    Task<ResumeParseResult> ParseAsync(byte[] content, ResumeFormat format, CancellationToken ct = default);
}
