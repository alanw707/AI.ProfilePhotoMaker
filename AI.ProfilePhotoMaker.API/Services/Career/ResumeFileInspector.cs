using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;

namespace AI.ProfilePhotoMaker.API.Services.Career;

/// <summary>Outcome of the cheap checks that run before anything is stored or parsed.</summary>
public sealed record ResumeInspection(
    ResumeFormat? Format,
    int PageCount,
    CareerOutcomeKind? Rejection,
    string? Detail)
{
    public static ResumeInspection Accept(ResumeFormat format, int pages) => new(format, pages, null, null);
    public static ResumeInspection TooLarge(string detail) => new(null, 0, CareerOutcomeKind.TooLarge, detail);
    public static ResumeInspection Unsupported(string detail) => new(null, 0, CareerOutcomeKind.Unsupported, detail);
}

/// <summary>
/// Decides what a file really is from its bytes (never its name) and applies the
/// ADR 0007 limits before any parsing: size, page count, encrypted PDFs, legacy or
/// encrypted Office (OLE) files, and unsafe ZIP archives.
/// </summary>
public static partial class ResumeFileInspector
{
    public const long MaxBytes = 10L * 1024 * 1024;
    public const int MaxPages = 25;
    public const int MaxCharacters = 100_000;
    public const int MaxZipEntries = 200;
    public const long MaxZipExpandedBytes = 50L * 1024 * 1024;
    public const int MaxZipRatio = 100;

    /// <summary>Ratio checks ignore small entries, where a high ratio is normal and harmless.</summary>
    private const long RatioCheckFloorBytes = 1024 * 1024;

    public static ResumeInspection Inspect(byte[] bytes)
    {
        if (bytes.Length > MaxBytes)
        {
            return ResumeInspection.TooLarge("The file is over 10 MB.");
        }

        if (StartsWith(bytes, 0xD0, 0xCF, 0x11, 0xE0))
        {
            return ResumeInspection.Unsupported("Legacy .doc and password-protected Office files are not supported. Save it as PDF or DOCX.");
        }

        if (StartsWith(bytes, (byte)'%', (byte)'P', (byte)'D', (byte)'F', (byte)'-'))
        {
            return InspectPdf(bytes);
        }

        if (StartsWith(bytes, (byte)'P', (byte)'K', 0x03, 0x04))
        {
            return InspectDocx(bytes);
        }

        return ResumeInspection.Unsupported("This is not a PDF or DOCX file.");
    }

    private static ResumeInspection InspectPdf(byte[] bytes)
    {
        if (EncryptPattern().IsMatch(Encoding.Latin1.GetString(bytes)))
        {
            return ResumeInspection.Unsupported("Password-protected PDFs are not supported. Remove the password and upload again.");
        }

        var pages = DependencyFreeResumeParser.CountPdfPages(bytes);
        return pages > MaxPages
            ? ResumeInspection.TooLarge($"The document has more than {MaxPages} pages.")
            : ResumeInspection.Accept(ResumeFormat.Pdf, Math.Max(pages, 1));
    }

    private static ResumeInspection InspectDocx(byte[] bytes)
    {
        try
        {
            using var archive = new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read);
            if (archive.Entries.Count > MaxZipEntries)
            {
                return ResumeInspection.Unsupported("The archive has too many parts to be a normal document.");
            }

            long declared = 0;
            foreach (var entry in archive.Entries)
            {
                declared += entry.Length;
                var ratio = entry.CompressedLength > 0 ? entry.Length / entry.CompressedLength : entry.Length;
                if (declared > MaxZipExpandedBytes || (entry.Length >= RatioCheckFloorBytes && ratio > MaxZipRatio))
                {
                    return ResumeInspection.Unsupported("The archive expands to an unsafe size.");
                }
            }

            return archive.GetEntry("word/document.xml") == null
                ? ResumeInspection.Unsupported("This is not a Word document.")
                : ResumeInspection.Accept(ResumeFormat.Docx, 1);
        }
        catch (InvalidDataException)
        {
            return ResumeInspection.Unsupported("This is not a valid DOCX file.");
        }
    }

    private static bool StartsWith(byte[] bytes, params byte[] prefix) =>
        bytes.Length >= prefix.Length && bytes.AsSpan(0, prefix.Length).SequenceEqual(prefix);

    [GeneratedRegex(@"/Encrypt\b")]
    private static partial Regex EncryptPattern();
}
