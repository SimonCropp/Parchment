namespace Parchment.Tests.Markdown.Renderers;

using global::Markdig.Syntax;
using Parchment.Markdown;

static class RendererHarness
{
    public static OpenXmlMarkdownRenderer BuildRenderer(int headingOffset = 0)
    {
        var stream = new MemoryStream();
        var doc = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document);
        var mainPart = doc.AddMainDocumentPart();
        mainPart.Document = new Document(new Body());
        var stylesPart = mainPart.AddNewPart<StyleDefinitionsPart>();
        stylesPart.Styles = new Styles();
        return new OpenXmlMarkdownRenderer(mainPart, headingOffset);
    }

    public static MarkdownDocument Parse(string markdown) =>
        global::Markdig.Markdown.Parse(markdown, MarkdigPipeline.Pipeline);

    public static T FirstBlock<T>(string markdown) where T : Block =>
        Parse(markdown).Descendants<T>().First();

    public static T FirstInline<T>(string markdown) where T : global::Markdig.Syntax.Inlines.Inline
    {
        var paragraph = FirstBlock<ParagraphBlock>(markdown);
        var child = paragraph.Inline!.FirstChild;
        while (child != null)
        {
            if (child is T match)
            {
                return match;
            }

            child = child.NextSibling;
        }

        throw new InvalidOperationException($"No {typeof(T).Name} found in: {markdown}");
    }
}
