using System.Globalization;
using PdfSharp.Drawing;
using PdfSharp.Fonts;
using PdfSharp.Pdf;
using PdfSharp.Pdf.Annotations;

namespace AI.ProfilePhotoMaker.API.Services.Career.Export;

/// <summary>
/// PDF via PDFsharp 6 with the embedded DejaVu Sans, so accented and (where the font has glyphs) non-Latin names are
/// real text. Lines are laid out first and a page break only ever falls between lines; headings go into the outline;
/// URLs are link annotations and, when longer than a line, are split across lines.
/// </summary>
public sealed class PdfMaterialRenderer : IMaterialExportRenderer
{
    private const string FontFamily = "DejaVu Sans";
    private const double Margin = 54;
    private const double BodySize = 10.5;
    private const double HeadingSize = 13;
    private const double NameSize = 20;
    private const double PhotoSize = 84;

    private static readonly object FontLock = new();

    public string Format => "pdf";
    public string ContentType => "application/pdf";

    private readonly record struct Piece(string Text, string? Url);

    public byte[] Render(MaterialExportDocument document)
    {
        EnsureFontResolver();
        using var pdf = new PdfDocument();
        pdf.Info.Title = ExportText.Clean(document.Title);
        pdf.Info.Creator = "";
        pdf.Info.Author = "";
        pdf.Options.CompressContentStreams = true;

        var body = new XFont(FontFamily, BodySize);
        var heading = new XFont(FontFamily, HeadingSize);
        var name = new XFont(FontFamily, NameSize);
        var writer = new Writer(pdf, body);
        try
        {
            XImage? photo = null;
            if (document.Photo is { Length: > 0 })
            {
                photo = XImage.FromStream(new MemoryStream(document.Photo));
            }

            var textWidth = writer.PageWidth - 2 * Margin;
            var nameWidth = photo != null ? textWidth - PhotoSize - 12 : textWidth;
            if (photo != null)
            {
                var scale = Math.Min(PhotoSize / photo.PixelWidth, PhotoSize / photo.PixelHeight);
                writer.DrawImage(photo, writer.PageWidth - Margin - photo.PixelWidth * scale, Margin, photo.PixelWidth * scale, photo.PixelHeight * scale);
            }

            writer.Heading(ExportText.Clean(document.Name), name, nameWidth, spaceBefore: 0, outline: true);
            if (!string.IsNullOrEmpty(document.ContactLine))
            {
                writer.Paragraph(document.ContactLine, body, nameWidth, indent: 0, spaceAfter: 4);
            }
            if (photo != null)
            {
                writer.MoveBelow(Margin + PhotoSize + 10);
            }
            foreach (var section in document.Sections)
            {
                if (!string.IsNullOrEmpty(section.Heading))
                {
                    writer.Heading(section.Heading, heading, textWidth, spaceBefore: 12, outline: true);
                }
                foreach (var paragraph in section.Paragraphs)
                {
                    writer.Paragraph(ExportText.Clean(paragraph.Text), body, textWidth, indent: paragraph.Bullet ? 12 : 0, spaceAfter: 3, bullet: paragraph.Bullet);
                }
            }
        }
        finally
        {
            writer.Dispose();
        }

        using var stream = new MemoryStream();
        pdf.Save(stream, false);
        return stream.ToArray();
    }

    private static void EnsureFontResolver()
    {
        lock (FontLock)
        {
            if (GlobalFontSettings.FontResolver == null)
            {
                GlobalFontSettings.FontResolver = new EmbeddedFontResolver();
            }
        }
    }

    /// <summary>Resolves every request to the embedded DejaVu Sans; headings differ by size, so no style is simulated.</summary>
    private sealed class EmbeddedFontResolver : IFontResolver
    {
        private const string FaceName = "DejaVuSans";
        private static readonly Lazy<byte[]> Bytes = new(() =>
        {
            using var resource = typeof(EmbeddedFontResolver).Assembly.GetManifestResourceStream("DejaVuSans.ttf")
                ?? throw new InvalidOperationException("The embedded DejaVu Sans font is missing.");
            using var copy = new MemoryStream();
            resource.CopyTo(copy);
            return copy.ToArray();
        });

        public FontResolverInfo? ResolveTypeface(string familyName, bool bold, bool italic) => new(FaceName);

        public byte[]? GetFont(string faceName) => faceName == FaceName ? Bytes.Value : null;
    }

    /// <summary>Lays out lines top to bottom and starts a new page before a line that would not fit.</summary>
    private sealed class Writer : IDisposable
    {
        private readonly PdfDocument _pdf;
        private readonly XFont _body;
        private PdfPage _page;
        private XGraphics _gfx;
        private double _y = Margin;

        public Writer(PdfDocument pdf, XFont body)
        {
            _pdf = pdf;
            _body = body;
            _page = NewPage();
            _gfx = XGraphics.FromPdfPage(_page);
        }

        public double PageWidth => _page.Width.Point;
        private double PageHeight => _page.Height.Point;

        private PdfPage NewPage()
        {
            var page = _pdf.AddPage();
            page.Size = PdfSharp.PageSize.Letter;
            return page;
        }

        private void Break()
        {
            _gfx.Dispose();
            _page = NewPage();
            _gfx = XGraphics.FromPdfPage(_page);
            _y = Margin;
        }

        public void MoveBelow(double y) => _y = Math.Max(_y, y);

        public void DrawImage(XImage image, double x, double y, double width, double height) => _gfx.DrawImage(image, x, y, width, height);

        private static double LineHeight(XFont font) => font.Size * 1.4;

        public void Heading(string text, XFont font, double width, double spaceBefore, bool outline)
        {
            var lines = Layout(text, font, width, 0);
            // A heading never ends a page: keep it with the first line of what follows.
            var needed = spaceBefore + lines.Count * LineHeight(font) + LineHeight(_body);
            if (_y + needed > PageHeight - Margin && _y > Margin)
            {
                Break();
            }
            else
            {
                _y += _y > Margin ? spaceBefore : 0;
            }
            var first = true;
            foreach (var line in lines)
            {
                DrawLine(line, font, Margin, outlineText: first && outline ? text : null);
                first = false;
            }
            _y += 2;
        }

        public void Paragraph(string text, XFont font, double width, double indent, double spaceAfter, bool bullet = false)
        {
            var content = bullet ? "\u2022 " + text : text;
            var lines = Layout(content, font, width - indent, 0);
            foreach (var line in lines)
            {
                DrawLine(line, font, Margin + indent, null);
            }
            _y += spaceAfter;
        }

        private void DrawLine(List<Piece> line, XFont font, double x, string? outlineText)
        {
            var height = LineHeight(font);
            if (_y + height > PageHeight - Margin && _y > Margin)
            {
                Break();
            }
            if (outlineText != null)
            {
                _pdf.Outlines.Add(outlineText, _page, true);
            }
            var left = x;
            foreach (var piece in line)
            {
                var width = _gfx.MeasureString(piece.Text, font, XStringFormats.TopLeft).Width;
                var brush = piece.Url == null ? XBrushes.Black : XBrushes.MediumBlue;
                _gfx.DrawString(piece.Text, font, brush, new XPoint(left, _y), XStringFormats.TopLeft);
                if (piece.Url != null)
                {
                    _gfx.DrawLine(new XPen(XColors.MediumBlue, 0.5), left, _y + font.Size * 1.15, left + width, _y + font.Size * 1.15);
                    var rect = _gfx.Transformer.WorldToDefaultPage(new XRect(left, _y, width, height));
                    _page.AddWebLink(new PdfRectangle(rect), piece.Url);
                }
                left += width;
            }
            _y += height;
        }

        /// <summary>Greedy word wrap. Plain words wrap at spaces; a word (usually a URL) wider than a line is cut by character.</summary>
        private List<List<Piece>> Layout(string text, XFont font, double width, double unused)
        {
            var lines = new List<List<Piece>>();
            var current = new List<Piece>();
            var used = 0.0;
            var spaceWidth = _gfx.MeasureString(" ", font, XStringFormats.TopLeft).Width;
            if (spaceWidth <= 0)
            {
                spaceWidth = font.Size * 0.3;
            }

            void Flush()
            {
                if (current.Count > 0)
                {
                    lines.Add(current);
                }
                current = new List<Piece>();
                used = 0;
            }

            void Place(string word, string? url, bool spaceBefore)
            {
                var wordWidth = _gfx.MeasureString(word, font, XStringFormats.TopLeft).Width;
                var gap = spaceBefore && current.Count > 0 ? spaceWidth : 0;
                if (used + gap + wordWidth <= width)
                {
                    // The gap is part of the text so the line reads with its spaces when extracted.
                    if (gap > 0 && url != null)
                    {
                        current.Add(new Piece(" ", null));
                        current.Add(new Piece(word, url));
                    }
                    else
                    {
                        current.Add(new Piece((gap > 0 ? " " : "") + word, url));
                    }
                    used += gap + wordWidth;
                    return;
                }
                if (current.Count > 0)
                {
                    Flush();
                }
                if (wordWidth <= width)
                {
                    current.Add(new Piece(word, url));
                    used = wordWidth;
                    return;
                }
                // Longer than a whole line: cut at the last character that fits, never inside a surrogate pair or combining mark.
                var elements = new List<string>();
                var enumerator = StringInfo.GetTextElementEnumerator(word);
                while (enumerator.MoveNext())
                {
                    elements.Add((string)enumerator.Current);
                }
                var chunk = "";
                foreach (var element in elements)
                {
                    var next = chunk + element;
                    if (chunk.Length > 0 && _gfx.MeasureString(next, font, XStringFormats.TopLeft).Width > width)
                    {
                        current.Add(new Piece(chunk, url));
                        Flush();
                        chunk = element;
                    }
                    else
                    {
                        chunk = next;
                    }
                }
                if (chunk.Length > 0)
                {
                    current.Add(new Piece(chunk, url));
                    used = _gfx.MeasureString(chunk, font, XStringFormats.TopLeft).Width;
                }
            }

            var spaceBeforeNext = false;
            foreach (var (run, url) in ExportText.Runs(text))
            {
                if (url != null)
                {
                    Place(run, url, spaceBeforeNext || (current.Count > 0 && char.IsWhiteSpace(run[0])));
                    spaceBeforeNext = false;
                    continue;
                }
                var leadingSpace = run.Length > 0 && char.IsWhiteSpace(run[0]);
                var words = run.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                for (var i = 0; i < words.Length; i++)
                {
                    Place(words[i], null, i > 0 || leadingSpace || spaceBeforeNext);
                }
                spaceBeforeNext = run.Length > 0 && char.IsWhiteSpace(run[^1]);
            }
            Flush();
            if (lines.Count == 0)
            {
                lines.Add(new List<Piece>());
            }
            return lines;
        }

        public void Dispose() => _gfx.Dispose();
    }
}
