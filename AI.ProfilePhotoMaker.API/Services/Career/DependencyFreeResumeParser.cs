using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;

namespace AI.ProfilePhotoMaker.API.Services.Career;

/// <summary>
/// Built-in, dependency-free text extraction (ADR 0007). Reads DOCX paragraphs and
/// the literal strings of PDF content streams (plain or Flate). It does not do OCR,
/// custom font encodings or object streams; those documents come back with no text
/// and the user falls back to pasting. A licensed parser is expected to replace it.
/// </summary>
public sealed partial class DependencyFreeResumeParser : IResumeParser
{
    /// <summary>Caps decompressed output so a crafted stream cannot exhaust memory.</summary>
    public const int MaxDecompressedBytes = 20 * 1024 * 1024;

    public Task<ResumeParseResult> ParseAsync(byte[] content, ResumeFormat format, CancellationToken ct = default)
    {
        var result = format == ResumeFormat.Pdf ? ParsePdf(content, ct) : ParseDocx(content, ct);
        return Task.FromResult(result);
    }

    /// <summary>Counts <c>/Type /Page</c> objects, not <c>/Type /Pages</c>.</summary>
    public static int CountPdfPages(byte[] content) =>
        PageObjectPattern().Matches(Encoding.Latin1.GetString(content)).Count;

    // ---- DOCX ------------------------------------------------------------

    private static ResumeParseResult ParseDocx(byte[] content, CancellationToken ct)
    {
        using var archive = new ZipArchive(new MemoryStream(content), ZipArchiveMode.Read);
        var entry = archive.GetEntry("word/document.xml") ?? throw new InvalidDataException("No document.xml.");

        // Declared sizes can lie, so the read itself is bounded.
        using var xml = new MemoryStream();
        using (var stream = entry.Open())
        {
            CopyBounded(stream, xml);
        }
        xml.Position = 0;

        var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null };
        using var reader = XmlReader.Create(xml, settings);

        var pages = new List<StringBuilder> { new() };
        var paragraph = new StringBuilder();
        var bullet = false;
        var renderedBreaks = 0;

        while (reader.Read())
        {
            ct.ThrowIfCancellationRequested();
            if (reader.NodeType == XmlNodeType.Element)
            {
                switch (reader.LocalName)
                {
                    case "p":
                        paragraph.Clear();
                        bullet = false;
                        break;
                    case "numPr":
                        bullet = true;
                        break;
                    case "t":
                        paragraph.Append(reader.ReadElementContentAsString());
                        break;
                    case "tab":
                        paragraph.Append('\t');
                        break;
                    case "br" when reader.GetAttribute("type", reader.NamespaceURI) == "page":
                        pages.Add(new StringBuilder());
                        break;
                    case "lastRenderedPageBreak":
                        renderedBreaks++;
                        break;
                }
            }
            else if (reader.NodeType == XmlNodeType.EndElement && reader.LocalName == "p")
            {
                var text = paragraph.ToString().Trim();
                if (text.Length > 0)
                {
                    pages[^1].Append(bullet ? "• " : string.Empty).Append(text).Append('\n');
                }
                paragraph.Clear();
            }
        }

        var texts = pages.Select(p => p.ToString()).ToList();
        return new ResumeParseResult(Math.Max(texts.Count, renderedBreaks + 1), texts);
    }

    // ---- PDF -------------------------------------------------------------

    private static ResumeParseResult ParsePdf(byte[] content, CancellationToken ct)
    {
        var raw = Encoding.Latin1.GetString(content);
        var objects = ReadObjects(raw);
        var pageCount = PageObjectPattern().Matches(raw).Count;

        // Preferred: follow each /Type /Page object's /Contents reference so text
        // lands on the right page even when streams are stored out of order.
        var pages = new List<string>();
        foreach (var (_, body) in objects.Where(o => PageObjectPattern().IsMatch(o.Value.Dictionary)).OrderBy(o => o.Key))
        {
            ct.ThrowIfCancellationRequested();
            var text = new StringBuilder();
            foreach (Match reference in ReferencePattern().Matches(ContentsValue(body.Dictionary)))
            {
                if (objects.TryGetValue(int.Parse(reference.Groups[1].Value), out var stream) && stream.Stream != null)
                {
                    text.Append(PdfTextExtractor.ExtractText(DecodeStream(stream)));
                    text.Append('\n');
                }
            }
            pages.Add(text.ToString());
        }

        return new ResumeParseResult(Math.Max(pageCount, pages.Count), pages);
    }

    private static string ContentsValue(string dictionary)
    {
        var match = ContentsPattern().Match(dictionary);
        return match.Success ? match.Groups[1].Value : string.Empty;
    }

    private sealed record PdfObject(string Dictionary, byte[]? Stream);

    private static Dictionary<int, PdfObject> ReadObjects(string raw)
    {
        var objects = new Dictionary<int, PdfObject>();
        foreach (Match header in ObjectHeaderPattern().Matches(raw))
        {
            var start = header.Index + header.Length;
            var end = raw.IndexOf("endobj", start, StringComparison.Ordinal);
            if (end < 0)
            {
                end = raw.Length;
            }

            var streamAt = raw.IndexOf("stream", start, end - start, StringComparison.Ordinal);
            if (streamAt < 0)
            {
                objects[int.Parse(header.Groups[1].Value)] = new PdfObject(raw[start..end], null);
                continue;
            }

            var dictionary = raw[start..streamAt];
            var dataStart = streamAt + "stream".Length;
            if (dataStart < raw.Length && raw[dataStart] == '\r') dataStart++;
            if (dataStart < raw.Length && raw[dataStart] == '\n') dataStart++;
            var dataEnd = raw.IndexOf("endstream", dataStart, StringComparison.Ordinal);
            if (dataEnd < 0 || dataEnd > end)
            {
                dataEnd = Math.Min(end, raw.Length);
            }
            // Strip the end-of-line that precedes "endstream".
            if (dataEnd > dataStart && raw[dataEnd - 1] == '\n') dataEnd--;
            if (dataEnd > dataStart && raw[dataEnd - 1] == '\r') dataEnd--;

            objects[int.Parse(header.Groups[1].Value)] = new PdfObject(
                dictionary, Encoding.Latin1.GetBytes(raw[dataStart..dataEnd]));
        }
        return objects;
    }

    private static string DecodeStream(PdfObject obj)
    {
        var data = obj.Stream!;
        if (!obj.Dictionary.Contains("/FlateDecode", StringComparison.Ordinal))
        {
            return Encoding.Latin1.GetString(data);
        }

        using var output = new MemoryStream();
        using var z = new ZLibStream(new MemoryStream(data), CompressionMode.Decompress);
        CopyBounded(z, output);
        return Encoding.Latin1.GetString(output.ToArray());
    }

    private static void CopyBounded(Stream source, Stream destination)
    {
        var buffer = new byte[81920];
        long total = 0;
        int read;
        while ((read = source.Read(buffer, 0, buffer.Length)) > 0)
        {
            total += read;
            if (total > MaxDecompressedBytes)
            {
                throw new InvalidDataException("Decompressed content is too large.");
            }
            destination.Write(buffer, 0, read);
        }
    }

    [GeneratedRegex(@"/Type\s*/Page(?![A-Za-z])")]
    private static partial Regex PageObjectPattern();

    [GeneratedRegex(@"(\d+)\s+\d+\s+obj\b")]
    private static partial Regex ObjectHeaderPattern();

    [GeneratedRegex(@"/Contents\s*(\[[^\]]*\]|\d+\s+\d+\s+R)")]
    private static partial Regex ContentsPattern();

    [GeneratedRegex(@"(\d+)\s+\d+\s+R")]
    private static partial Regex ReferencePattern();
}

/// <summary>Reads text-showing operators from a decoded PDF content stream.</summary>
internal static class PdfTextExtractor
{
    public static string ExtractText(string stream)
    {
        var output = new StringBuilder();
        var line = new StringBuilder();
        var strings = new List<string>();
        var numbers = new List<double>();
        var inArray = false;
        var i = 0;

        void FlushLine()
        {
            if (line.Length > 0)
            {
                output.Append(line.ToString().TrimEnd()).Append('\n');
                line.Clear();
            }
        }

        while (i < stream.Length)
        {
            var c = stream[i];
            if (char.IsWhiteSpace(c))
            {
                i++;
            }
            else if (c == '%')
            {
                while (i < stream.Length && stream[i] != '\n' && stream[i] != '\r') i++;
            }
            else if (c == '(')
            {
                strings.Add(ReadLiteral(stream, ref i));
            }
            else if (c == '<' && i + 1 < stream.Length && stream[i + 1] == '<')
            {
                i += 2;
            }
            else if (c == '>' && i + 1 < stream.Length && stream[i + 1] == '>')
            {
                i += 2;
            }
            else if (c == '<')
            {
                strings.Add(ReadHex(stream, ref i));
            }
            else if (c == '[')
            {
                inArray = true;
                i++;
            }
            else if (c == ']')
            {
                inArray = false;
                i++;
            }
            else if (c == '/')
            {
                i++;
                while (i < stream.Length && !IsDelimiter(stream[i])) i++;
            }
            else
            {
                var start = i;
                while (i < stream.Length && !IsDelimiter(stream[i])) i++;
                if (i == start)
                {
                    i++;
                    continue;
                }

                var token = stream[start..i];
                if (double.TryParse(token, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var number))
                {
                    numbers.Add(number);
                    // A large negative kerning inside TJ is a word gap.
                    if (inArray && number < -200 && strings.Count > 0)
                    {
                        strings[^1] += " ";
                    }
                    continue;
                }

                switch (token)
                {
                    case "Tj":
                    case "TJ":
                        line.Append(string.Concat(strings));
                        break;
                    case "'":
                    case "\"":
                        FlushLine();
                        line.Append(string.Concat(strings));
                        break;
                    case "Td":
                    case "TD":
                        // Only a vertical move starts a new line; a horizontal one continues it.
                        if (numbers.Count >= 2 && numbers[^1] != 0)
                        {
                            FlushLine();
                        }
                        else if (line.Length > 0)
                        {
                            line.Append(' ');
                        }
                        break;
                    case "Tm":
                    case "T*":
                    case "ET":
                    case "BT":
                        FlushLine();
                        break;
                }
                strings.Clear();
                numbers.Clear();
                inArray = false;
            }
        }

        FlushLine();
        return output.ToString();
    }

    private static bool IsDelimiter(char c) => char.IsWhiteSpace(c) || "()<>[]{}/%".Contains(c);

    private static string ReadLiteral(string s, ref int i)
    {
        var result = new StringBuilder();
        var depth = 0;
        i++; // opening paren
        while (i < s.Length)
        {
            var c = s[i++];
            if (c == '\\' && i < s.Length)
            {
                var next = s[i++];
                switch (next)
                {
                    case 'n': result.Append('\n'); break;
                    case 'r': result.Append('\r'); break;
                    case 't': result.Append('\t'); break;
                    case 'b': result.Append('\b'); break;
                    case 'f': result.Append('\f'); break;
                    case '\r': if (i < s.Length && s[i] == '\n') i++; break;
                    case '\n': break;
                    case >= '0' and <= '7':
                        var octal = next - '0';
                        for (var n = 0; n < 2 && i < s.Length && s[i] is >= '0' and <= '7'; n++)
                        {
                            octal = octal * 8 + (s[i++] - '0');
                        }
                        result.Append((char)(octal & 0xFF));
                        break;
                    default: result.Append(next); break;
                }
            }
            else if (c == '(')
            {
                depth++;
                result.Append(c);
            }
            else if (c == ')')
            {
                if (depth == 0)
                {
                    break;
                }
                depth--;
                result.Append(c);
            }
            else
            {
                result.Append(c);
            }
        }
        return result.ToString();
    }

    private static string ReadHex(string s, ref int i)
    {
        var hex = new StringBuilder();
        i++; // '<'
        while (i < s.Length && s[i] != '>')
        {
            if (Uri.IsHexDigit(s[i])) hex.Append(s[i]);
            i++;
        }
        i++; // '>'
        if (hex.Length % 2 == 1) hex.Append('0');

        var result = new StringBuilder();
        for (var n = 0; n + 1 < hex.Length; n += 2)
        {
            var value = Convert.ToInt32(hex.ToString(n, 2), 16);
            // Control bytes in a hex string are font glyph ids, not text.
            if (value >= 32) result.Append((char)value);
        }
        return result.ToString();
    }
}
