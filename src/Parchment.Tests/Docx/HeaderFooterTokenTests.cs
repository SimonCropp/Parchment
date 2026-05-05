public class HeaderFooterTokenTests
{
    public class Doc
    {
        public required string Title { get; init; }
        public required string Author { get; init; }
        public required string FootnoteText { get; init; }
        public required string EndnoteText { get; init; }
    }

    [Test]
    public async Task TokensInHeaderAndFooterAreSubstituted()
    {
        using var template = BuildDocxWithHeaderFooter(
            bodyText: "Body: {{ Title }}",
            headerText: "Header: {{ Title }}",
            footerText: "Footer by {{ Author }}");

        var store = new TemplateStore();
        store.RegisterDocxTemplate<Doc>("hf", template);

        var model = new Doc
        {
            Title = "Annual Report",
            Author = "Ada Lovelace",
            FootnoteText = "fn",
            EndnoteText = "en"
        };

        using var stream = new MemoryStream();
        await store.Render("hf", model, stream);
        stream.Position = 0;

        using var doc = WordprocessingDocument.Open(stream, false);
        var bodyText = string.Concat(doc.MainDocumentPart!.Document!.Body!.Descendants<Text>().Select(_ => _.Text));
        var headerText = string.Concat(doc.MainDocumentPart.HeaderParts.Single().Header!.Descendants<Text>().Select(_ => _.Text));
        var footerText = string.Concat(doc.MainDocumentPart.FooterParts.Single().Footer!.Descendants<Text>().Select(_ => _.Text));

        await Assert.That(bodyText).IsEqualTo("Body: Annual Report");
        await Assert.That(headerText).IsEqualTo("Header: Annual Report");
        await Assert.That(footerText).IsEqualTo("Footer by Ada Lovelace");
    }

    [Test]
    public async Task TokensInFootnotesAndEndnotesAreSubstituted()
    {
        using var template = BuildDocxWithFootnotesEndnotes(
            bodyText: "{{ Title }}",
            footnoteText: "Footnote: {{ FootnoteText }}",
            endnoteText: "Endnote: {{ EndnoteText }}");

        var store = new TemplateStore();
        store.RegisterDocxTemplate<Doc>("fn-en", template);

        var model = new Doc
        {
            Title = "Doc",
            Author = "A",
            FootnoteText = "see ref 4",
            EndnoteText = "appendix B"
        };

        using var stream = new MemoryStream();
        await store.Render("fn-en", model, stream);
        stream.Position = 0;

        using var doc = WordprocessingDocument.Open(stream, false);
        var footnoteText = string.Concat(
            doc.MainDocumentPart!.FootnotesPart!.Footnotes!.Descendants<Text>().Select(_ => _.Text));
        var endnoteText = string.Concat(
            doc.MainDocumentPart.EndnotesPart!.Endnotes!.Descendants<Text>().Select(_ => _.Text));

        await Assert.That(footnoteText).Contains("Footnote: see ref 4");
        await Assert.That(endnoteText).Contains("Endnote: appendix B");
    }

    [Test]
    public async Task MissingMemberInHeaderIsRejectedAtRegistration()
    {
        // Reference validator must walk header parts too — a typo in a header token should fail
        // registration in the same way it would in the body.
        using var template = BuildDocxWithHeaderFooter(
            bodyText: "{{ Title }}",
            headerText: "{{ DoesNotExist }}",
            footerText: "ok");

        var store = new TemplateStore();
        await Assert.That(
                () => store.RegisterDocxTemplate<Doc>("bad-header", template))
            .Throws<ParchmentRegistrationException>();
    }

    static MemoryStream BuildDocxWithHeaderFooter(string bodyText, string headerText, string footerText)
    {
        var stream = new MemoryStream();
        using (var doc = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document))
        {
            var mainPart = doc.AddMainDocumentPart();
            mainPart.Document = new(new Body());
            var body = mainPart.Document.Body!;

            var headerPart = mainPart.AddNewPart<HeaderPart>("rIdHeader");
            headerPart.Header = new(BuildParagraph(headerText));

            var footerPart = mainPart.AddNewPart<FooterPart>("rIdFooter");
            footerPart.Footer = new(BuildParagraph(footerText));

            body.Append(BuildParagraph(bodyText));
            body.Append(
                new SectionProperties(
                    new HeaderReference
                    {
                        Type = HeaderFooterValues.Default,
                        Id = "rIdHeader"
                    },
                    new FooterReference
                    {
                        Type = HeaderFooterValues.Default,
                        Id = "rIdFooter"
                    },
                    new PageSize { Width = 6500, Height = 8000 }));
        }

        stream.Position = 0;
        return stream;
    }

    static MemoryStream BuildDocxWithFootnotesEndnotes(string bodyText, string footnoteText, string endnoteText)
    {
        var stream = new MemoryStream();
        using (var doc = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document))
        {
            var mainPart = doc.AddMainDocumentPart();
            mainPart.Document = new(new Body());
            var body = mainPart.Document.Body!;

            var footnotesPart = mainPart.AddNewPart<FootnotesPart>("rIdFootnotes");
            footnotesPart.Footnotes = new(
                new Footnote(BuildParagraph(footnoteText)) { Id = 1 });

            var endnotesPart = mainPart.AddNewPart<EndnotesPart>("rIdEndnotes");
            endnotesPart.Endnotes = new(
                new Endnote(BuildParagraph(endnoteText)) { Id = 1 });

            body.Append(BuildParagraph(bodyText));
            body.Append(new SectionProperties(new PageSize { Width = 6500, Height = 8000 }));
        }

        stream.Position = 0;
        return stream;
    }

    static Paragraph BuildParagraph(string text) =>
        new(new Run(new Text(text) { Space = SpaceProcessingModeValues.Preserve }));
}
