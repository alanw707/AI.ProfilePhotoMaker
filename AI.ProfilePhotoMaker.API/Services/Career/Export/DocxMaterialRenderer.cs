using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace AI.ProfilePhotoMaker.API.Services.Career.Export;

/// <summary>DOCX via DocumentFormat.OpenXml: Heading1 for the name, Heading2 for sections, real hyperlink relationships, no images.</summary>
public sealed class DocxMaterialRenderer : IMaterialExportRenderer
{
    public string Format => "docx";
    public string ContentType => "application/vnd.openxmlformats-officedocument.wordprocessingml.document";

    public byte[] Render(MaterialExportDocument document)
    {
        using var stream = new MemoryStream();
        using (var package = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document))
        {
            var main = package.AddMainDocumentPart();
            main.Document = new Document();
            var body = main.Document.AppendChild(new Body());
            AddStyles(main);

            package.PackageProperties.Title = ExportText.Clean(document.Title);

            body.AppendChild(Styled("Heading1", Runs(main, ExportText.Clean(document.Name))));
            if (!string.IsNullOrEmpty(document.ContactLine))
            {
                body.AppendChild(Paragraph(main, document.ContactLine, null));
            }
            foreach (var section in document.Sections)
            {
                if (!string.IsNullOrEmpty(section.Heading))
                {
                    body.AppendChild(Styled("Heading2", Runs(main, section.Heading)));
                }
                foreach (var paragraph in section.Paragraphs)
                {
                    body.AppendChild(Paragraph(main, (paragraph.Bullet ? "\u2022 " : "") + paragraph.Text, paragraph.Bullet ? "ListParagraph" : null));
                }
            }
            body.AppendChild(new SectionProperties(
                new PageSize { Width = 12240, Height = 15840 },
                new PageMargin { Top = 1080, Bottom = 1080, Left = 1080, Right = 1080, Header = 720, Footer = 720 }));
            main.Document.Save();
        }
        return stream.ToArray();
    }

    private static Paragraph Styled(string styleId, IEnumerable<OpenXmlElement> runs)
    {
        var paragraph = new Paragraph(new ParagraphProperties(new ParagraphStyleId { Val = styleId }));
        paragraph.Append(runs);
        return paragraph;
    }

    private static Paragraph Paragraph(MainDocumentPart main, string text, string? styleId)
    {
        var paragraph = new Paragraph();
        if (styleId != null)
        {
            paragraph.Append(new ParagraphProperties(new ParagraphStyleId { Val = styleId }));
        }
        paragraph.Append(Runs(main, ExportText.Clean(text)));
        return paragraph;
    }

    private static IEnumerable<OpenXmlElement> Runs(MainDocumentPart main, string text)
    {
        foreach (var (part, url) in ExportText.Runs(text))
        {
            if (url == null)
            {
                yield return PlainRun(part);
                continue;
            }
            var relationship = main.AddHyperlinkRelationship(new Uri(url), true);
            yield return new Hyperlink(PlainRun(part, "Hyperlink")) { Id = relationship.Id };
        }
    }

    private static Run PlainRun(string text, string? styleId = null)
    {
        var run = new Run();
        if (styleId != null)
        {
            run.Append(new RunProperties(new RunStyle { Val = styleId }));
        }
        run.Append(new Text(text) { Space = SpaceProcessingModeValues.Preserve });
        return run;
    }

    private static void AddStyles(MainDocumentPart main)
    {
        var styles = main.AddNewPart<StyleDefinitionsPart>();
        styles.Styles = new Styles(
            new DocDefaults(new RunPropertiesDefault(new RunPropertiesBaseStyle(
                new RunFonts { Ascii = "Calibri", HighAnsi = "Calibri", EastAsia = "Noto Sans CJK SC", ComplexScript = "Calibri" },
                new FontSize { Val = "22" }))),
            new Style(new StyleName { Val = "Normal" }, new PrimaryStyle()) { Type = StyleValues.Paragraph, StyleId = "Normal", Default = true },
            new Style(
                new StyleName { Val = "heading 1" }, new BasedOn { Val = "Normal" }, new NextParagraphStyle { Val = "Normal" }, new PrimaryStyle(),
                new StyleParagraphProperties(new KeepNext(), new SpacingBetweenLines { Before = "0", After = "80" }, new OutlineLevel { Val = 0 }),
                new StyleRunProperties(new Bold(), new FontSize { Val = "36" }))
            { Type = StyleValues.Paragraph, StyleId = "Heading1" },
            new Style(
                new StyleName { Val = "heading 2" }, new BasedOn { Val = "Normal" }, new NextParagraphStyle { Val = "Normal" }, new PrimaryStyle(),
                new StyleParagraphProperties(new KeepNext(), new SpacingBetweenLines { Before = "240", After = "80" }, new OutlineLevel { Val = 1 }),
                new StyleRunProperties(new Bold(), new FontSize { Val = "26" }))
            { Type = StyleValues.Paragraph, StyleId = "Heading2" },
            new Style(
                new StyleName { Val = "List Paragraph" }, new BasedOn { Val = "Normal" }, new PrimaryStyle(),
                new StyleParagraphProperties(new Indentation { Left = "360", Hanging = "180" }))
            { Type = StyleValues.Paragraph, StyleId = "ListParagraph" },
            new Style(
                new StyleName { Val = "Hyperlink" },
                new StyleRunProperties(new Color { Val = "0563C1" }, new Underline { Val = UnderlineValues.Single }))
            { Type = StyleValues.Character, StyleId = "Hyperlink" });
    }
}
