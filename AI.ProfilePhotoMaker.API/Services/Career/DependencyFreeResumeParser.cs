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

    /// <summary>More objects than any real resume needs; beyond it the file is treated as hostile.</summary>
    public const int MaxObjects = 5_000;

    /// <summary>Regex time limit; a hit is a parser error, never a hang.</summary>
    public const int MatchTimeoutMs = 250;

    /// <summary>One stream's extracted text never needs to exceed the document limit plus a margin.</summary>
    private const int TextCapChars = ResumeFileInspector.MaxCharacters + 1_024;

    /// <summary>Once total text passes the limit the service rejects the file, so parsing stops.</summary>
    private const int TextStopChars = ResumeFileInspector.MaxCharacters;

    public Task<ResumeParseResult> ParseAsync(byte[] content, ResumeFormat format, CancellationToken ct = default)
    {
        var result = format == ResumeFormat.Pdf ? ParsePdf(content, ct) : ParseDocx(content, ct);
        return Task.FromResult(result);
    }

    /// <summary>Counts <c>/Type /Page</c> objects, not <c>/Type /Pages</c>.</summary>
    public static int CountPdfPages(byte[] content) =>
        PageObjectPattern().Count(Encoding.Latin1.GetString(content));

    // ---- DOCX ------------------------------------------------------------

    private static ResumeParseResult ParseDocx(byte[] content, CancellationToken ct)
    {
        using var archive = new ZipArchive(new MemoryStream(content), ZipArchiveMode.Read);
        var entry = archive.GetEntry("word/document.xml") ?? throw new InvalidDataException("No document.xml.");

        // Declared sizes can lie, so the read itself is bounded.
        using var xml = new MemoryStream();
        using (var stream = entry.Open())
        {
            CopyBounded(stream, xml, ResumeFileInspector.MaxDocumentXmlBytes, ct);
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
        var document = new PdfDocument(content, ct);
        var pageCount = PageObjectPattern().Matches(document.Raw).Count;

        // Preferred: follow each /Type /Page object's /Contents reference so text
        // lands on the right page even when streams are stored out of order.
        var pages = new List<string>();
        var totalChars = 0;
        foreach (var page in document.PageObjectIds())
        {
            ct.ThrowIfCancellationRequested();
            var text = new StringBuilder();
            foreach (Match reference in ReferencePattern().Matches(ContentsValue(document.Dictionary(page))))
            {
                if (int.TryParse(reference.Groups[1].ValueSpan, out var id))
                {
                    var extracted = document.TextOf(id);
                    if (extracted != null)
                    {
                        text.Append(extracted).Append('\n');
                        totalChars += extracted.Length + 1;
                    }
                }

                // Past the limit the service answers 413 anyway; stop doing work.
                if (totalChars > TextStopChars)
                {
                    break;
                }
            }
            pages.Add(text.ToString());
            if (totalChars > TextStopChars)
            {
                break;
            }
        }

        return new ResumeParseResult(Math.Max(pageCount, pages.Count), pages);
    }

    private static string ContentsValue(string dictionary)
    {
        var match = ContentsPattern().Match(dictionary);
        return match.Success ? match.Groups[1].Value : string.Empty;
    }

    /// <summary>Where one PDF object's dictionary and optional stream sit inside the file bytes.</summary>
    private readonly record struct ObjectSpan(int DictStart, int DictEnd, int DataStart, int DataEnd)
    {
        public bool HasStream => DataStart >= 0;
    }

    /// <summary>
    /// One parse of one PDF. Objects are indexed in a single forward pass as offsets
    /// (no per-object copies), decoded bytes share one budget, and each stream is
    /// decoded and extracted at most once however often pages reference it.
    /// </summary>
    private sealed class PdfDocument
    {
        private readonly byte[] _content;
        private readonly CancellationToken _ct;
        private readonly Dictionary<int, ObjectSpan> _objects;
        private readonly Dictionary<int, string?> _textCache = new();
        private long _decodedBytes;

        public PdfDocument(byte[] content, CancellationToken ct)
        {
            _content = content;
            _ct = ct;
            // Latin-1 maps one byte to one char, so char offsets are byte offsets.
            Raw = Encoding.Latin1.GetString(content);
            _objects = IndexObjects();
        }

        public string Raw { get; }

        public IEnumerable<int> PageObjectIds() =>
            _objects.Where(o => PageObjectPattern().IsMatch(Raw.AsSpan(o.Value.DictStart, o.Value.DictEnd - o.Value.DictStart)))
                .Select(o => o.Key)
                .Order();

        public string Dictionary(int id)
        {
            var span = _objects[id];
            return Raw.Substring(span.DictStart, span.DictEnd - span.DictStart);
        }

        /// <summary>Text of a stream object, or null when the object is missing or has no stream.</summary>
        public string? TextOf(int id)
        {
            if (_textCache.TryGetValue(id, out var cached))
            {
                return cached;
            }

            string? text = null;
            if (_objects.TryGetValue(id, out var span) && span.HasStream)
            {
                text = PdfTextExtractor.ExtractText(Decode(span), TextCapChars, _ct);
            }
            _textCache[id] = text;
            return text;
        }

        private string Decode(ObjectSpan span)
        {
            var length = span.DataEnd - span.DataStart;
            var flate = Raw.AsSpan(span.DictStart, span.DictEnd - span.DictStart)
                .IndexOf("/FlateDecode", StringComparison.Ordinal) >= 0;
            if (!flate)
            {
                return Raw.Substring(span.DataStart, length);
            }

            using var output = new MemoryStream();
            using var z = new ZLibStream(new MemoryStream(_content, span.DataStart, length, writable: false), CompressionMode.Decompress);
            _decodedBytes = CopyBounded(z, output, MaxDecompressedBytes - _decodedBytes, _ct) + _decodedBytes;
            return Encoding.Latin1.GetString(output.GetBuffer(), 0, (int)output.Length);
        }

        private Dictionary<int, ObjectSpan> IndexObjects()
        {
            // Pass 1: every "N G obj" header, as offsets only.
            var headers = new List<(int Id, int Start, int BodyStart)>();
            var at = 0;
            while ((at = Raw.IndexOf("obj", at, StringComparison.Ordinal)) >= 0)
            {
                if ((headers.Count & 0xFF) == 0)
                {
                    _ct.ThrowIfCancellationRequested();
                }

                var next = at + 3;
                if (TryReadHeader(at, out var id, out var start) && (next >= Raw.Length || !char.IsLetterOrDigit(Raw[next])))
                {
                    if (headers.Count >= MaxObjects)
                    {
                        throw new InvalidDataException("Too many PDF objects.");
                    }
                    if (id >= 0)
                    {
                        headers.Add((id, start, next));
                    }
                }
                at = next;
            }

            // Pass 2: each body runs to its endobj or the next header, so the scans
            // cover disjoint ranges and the total work stays linear.
            var objects = new Dictionary<int, ObjectSpan>();
            for (var i = 0; i < headers.Count; i++)
            {
                _ct.ThrowIfCancellationRequested();
                var (id, _, bodyStart) = headers[i];
                var limit = i + 1 < headers.Count ? headers[i + 1].Start : Raw.Length;
                var end = Raw.IndexOf("endobj", bodyStart, limit - bodyStart, StringComparison.Ordinal);
                if (end < 0)
                {
                    end = limit;
                }

                var streamAt = Raw.IndexOf("stream", bodyStart, end - bodyStart, StringComparison.Ordinal);
                if (streamAt < 0)
                {
                    objects[id] = new ObjectSpan(bodyStart, end, -1, -1);
                    continue;
                }

                var dataStart = streamAt + "stream".Length;
                if (dataStart < end && Raw[dataStart] == '\r') dataStart++;
                if (dataStart < end && Raw[dataStart] == '\n') dataStart++;
                var dataEnd = Raw.IndexOf("endstream", dataStart, end - dataStart, StringComparison.Ordinal);
                if (dataEnd < 0)
                {
                    dataEnd = end;
                }
                // Strip the end-of-line that precedes "endstream".
                if (dataEnd > dataStart && Raw[dataEnd - 1] == '\n') dataEnd--;
                if (dataEnd > dataStart && Raw[dataEnd - 1] == '\r') dataEnd--;

                objects[id] = new ObjectSpan(bodyStart, streamAt, dataStart, dataEnd);
            }
            return objects;
        }

        /// <summary>
        /// Reads backwards from "obj" for <c>N G</c>. Over-long or out-of-range numbers
        /// give id -1 (skipped) instead of an overflow.
        /// </summary>
        private bool TryReadHeader(int objAt, out int id, out int start)
        {
            id = -1;
            start = objAt;
            var i = objAt - 1;

            var ws = 0;
            while (i >= 0 && IsPdfSpace(Raw[i]) && ws++ < 32) i--;
            if (ws == 0 || ws > 32) return false;

            var genEnd = i;
            while (i >= 0 && char.IsAsciiDigit(Raw[i])) i--;
            if (i == genEnd) return false;

            ws = 0;
            while (i >= 0 && IsPdfSpace(Raw[i]) && ws++ < 32) i--;
            if (ws == 0 || ws > 32) return false;

            var numEnd = i;
            while (i >= 0 && char.IsAsciiDigit(Raw[i])) i--;
            if (i == numEnd) return false;

            start = i + 1;
            var digits = Raw.AsSpan(start, numEnd - i);
            if (!int.TryParse(digits, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out id))
            {
                id = -1;
            }
            return true;
        }

        private static bool IsPdfSpace(char c) => c is ' ' or '\n' or '\r' or '\t' or '\f' or '\0';
    }

    /// <summary>
    /// Copies at most <paramref name="remaining"/> bytes and returns how many were copied.
    /// The cap comes from the caller so one budget can span many streams.
    /// </summary>
    private static long CopyBounded(Stream source, Stream destination, long remaining, CancellationToken ct)
    {
        var buffer = new byte[81920];
        long total = 0;
        int read;
        while ((read = source.Read(buffer, 0, buffer.Length)) > 0)
        {
            ct.ThrowIfCancellationRequested();
            total += read;
            if (total > remaining)
            {
                throw new InvalidDataException("Decompressed content is too large.");
            }
            destination.Write(buffer, 0, read);
        }
        return total;
    }

    [GeneratedRegex(@"/Type\s*/Page(?![A-Za-z])", RegexOptions.None, MatchTimeoutMs)]
    private static partial Regex PageObjectPattern();

    [GeneratedRegex(@"/Contents\s*(\[[^\]]*\]|\d+\s+\d+\s+R)", RegexOptions.None, MatchTimeoutMs)]
    private static partial Regex ContentsPattern();

    [GeneratedRegex(@"(\d+)\s+\d+\s+R", RegexOptions.None, MatchTimeoutMs)]
    private static partial Regex ReferencePattern();
}

/// <summary>Reads text-showing operators from a decoded PDF content stream.</summary>
internal static class PdfTextExtractor
{
    public static string ExtractText(string stream, int maxChars, CancellationToken ct)
    {
        var output = new StringBuilder();
        var line = new StringBuilder();
        var strings = new List<string>();
        var numbers = new List<double>();
        var inArray = false;
        var pending = 0; // chars held in strings not yet shown
        var i = 0;
        var steps = 0;

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
            if ((++steps & 0x3FFF) == 0)
            {
                ct.ThrowIfCancellationRequested();
            }
            // Enough text for the document limit: stop instead of buffering more.
            if (output.Length + line.Length + pending >= maxChars)
            {
                break;
            }

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
                strings.Add(ReadLiteral(stream, ref i, maxChars));
                pending += strings[^1].Length;
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
                strings.Add(ReadHex(stream, ref i, maxChars));
                pending += strings[^1].Length;
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
                        pending++;
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
                pending = 0;
                numbers.Clear();
                inArray = false;
            }
        }

        FlushLine();
        return output.Length > maxChars ? output.ToString(0, maxChars) : output.ToString();
    }

    private static bool IsDelimiter(char c) => char.IsWhiteSpace(c) || "()<>[]{}/%".Contains(c);

    private static string ReadLiteral(string s, ref int i, int maxChars)
    {
        var result = new StringBuilder();
        var depth = 0;
        i++; // opening paren
        while (i < s.Length)
        {
            if (result.Length >= maxChars)
            {
                // Nothing more is wanted; skip the rest of the stream.
                i = s.Length;
                break;
            }

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

    private static string ReadHex(string s, ref int i, int maxChars)
    {
        var hex = new StringBuilder();
        i++; // '<'
        while (i < s.Length && s[i] != '>' && hex.Length < maxChars * 2)
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
