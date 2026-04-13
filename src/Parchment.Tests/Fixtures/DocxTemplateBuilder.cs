namespace Parchment.Tests.Fixtures;

/// <summary>
/// Builds simple docx templates in-memory for test fixtures. Each template contains a sequence of
/// paragraphs whose text may include liquid tokens.
/// </summary>
internal static class DocxTemplateBuilder
{
    public static byte[] Build(params string[] paragraphs)
    {
        using var stream = new MemoryStream();
        using (var doc = WordprocessingDocument.Create(stream, DocumentFormat.OpenXml.WordprocessingDocumentType.Document))
        {
            var mainPart = doc.AddMainDocumentPart();
            mainPart.Document = new Document(new Body());
            var body = mainPart.Document.Body!;

            AddStyles(mainPart);

            foreach (var text in paragraphs)
            {
                body.Append(BuildParagraph(text));
            }

            body.Append(new SectionProperties(
                new PageSize { Width = 12240, Height = 15840 },
                new PageMargin { Top = 1440, Right = 1440, Bottom = 1440, Left = 1440, Header = 720, Footer = 720 }));
        }

        return stream.ToArray();
    }

    static Paragraph BuildParagraph(string text) =>
        new(new Run(new Text(text) { Space = SpaceProcessingModeValues.Preserve }));

    static void AddStyles(MainDocumentPart mainPart)
    {
        var stylesPart = mainPart.AddNewPart<StyleDefinitionsPart>();
        var styles = new Styles();

        styles.Append(BuildStyle("Normal", StyleValues.Paragraph, isDefault: true));
        for (var i = 1; i <= 6; i++)
        {
            styles.Append(BuildStyle($"Heading{i}", StyleValues.Paragraph));
        }

        styles.Append(BuildStyle("ListParagraph", StyleValues.Paragraph));
        styles.Append(BuildStyle("Quote", StyleValues.Paragraph));
        styles.Append(BuildStyle("Code", StyleValues.Paragraph));
        styles.Append(BuildStyle("Hyperlink", StyleValues.Character));

        stylesPart.Styles = styles;
    }

    static Style BuildStyle(string id, StyleValues type, bool isDefault = false)
    {
        var style = new Style
        {
            Type = type,
            StyleId = id
        };
        style.Append(new StyleName { Val = id });
        if (isDefault)
        {
            style.Default = true;
        }

        return style;
    }
}
