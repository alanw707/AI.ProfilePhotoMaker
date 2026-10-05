using System.Text.RegularExpressions;

namespace AI.ProfilePhotoMaker.API.Services.Career.Export;

/// <summary>
/// What a renderer draws, already in reading order (ADR 0019): the name, the chosen contact line, then sections.
/// A section with no heading is a plain lead paragraph (the resume headline).
/// </summary>
public sealed record MaterialExportDocument(
    string Title,
    string Name,
    string? ContactLine,
    IReadOnlyList<ExportSection> Sections,
    byte[]? Photo = null,
    string Language = "en");

public sealed record ExportSection(string? Heading, IReadOnlyList<ExportParagraph> Paragraphs);

public sealed record ExportParagraph(string Text, bool Bullet = false);

/// <summary>Draws a <see cref="MaterialExportDocument"/> in one file format. The seam keeps the library choice replaceable.</summary>
public interface IMaterialExportRenderer
{
    /// <summary>pdf | docx.</summary>
    string Format { get; }
    string ContentType { get; }
    byte[] Render(MaterialExportDocument document);
}

/// <summary>Text hygiene shared by the renderers: no control characters, and URLs found in text become links.</summary>
public static class ExportText
{
    private static readonly Regex UrlPattern = new(@"https?://[^\s<>""]+", RegexOptions.Compiled | RegexOptions.IgnoreCase, TimeSpan.FromSeconds(1));
    private const string TrailingPunctuation = ".,;:!?)]}'\u2019";

    public static string Clean(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return "";
        }
        var chars = new List<char>(text.Length);
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (char.IsHighSurrogate(c) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1]))
            {
                chars.Add(c);
                chars.Add(text[++i]);
            }
            else if (char.IsSurrogate(c) || c == '\uFFFE' || c == '\uFFFF')
            {
                continue;
            }
            else if (c is '\r' or '\n' or '\t')
            {
                chars.Add(' ');
            }
            else if (!char.IsControl(c))
            {
                chars.Add(c);
            }
        }
        return new string(chars.ToArray()).Trim();
    }

    /// <summary>Splits text into plain and link runs. Only absolute http(s) URLs become links.</summary>
    public static IReadOnlyList<(string Text, string? Url)> Runs(string text)
    {
        var runs = new List<(string, string?)>();
        var position = 0;
        foreach (Match match in UrlPattern.Matches(text))
        {
            var url = match.Value.TrimEnd(TrailingPunctuation.ToCharArray());
            if (url.Length <= "https://".Length || !Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https"))
            {
                continue;
            }
            if (match.Index > position)
            {
                runs.Add((text[position..match.Index], null));
            }
            runs.Add((url, url));
            position = match.Index + url.Length;
        }
        if (position < text.Length)
        {
            runs.Add((text[position..], null));
        }
        return runs;
    }
}
