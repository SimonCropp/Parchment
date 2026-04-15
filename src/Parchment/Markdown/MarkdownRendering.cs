/// <summary>
/// Entry point for rendering a markdown string into a list of OpenXML elements suitable for
/// splicing into a Word document body, header, footer, or other content host.
/// </summary>
static class MarkdownRendering
{
    public static IReadOnlyList<OpenXmlElement> Render(string markdown, MainDocumentPart mainPart, int headingOffset)
    {
        var document = Markdown.Parse(markdown, MarkdigPipeline.Pipeline);
        var renderer = new OpenXmlMarkdownRenderer(mainPart, headingOffset);
        renderer.Render(document);
        return renderer.Drain();
    }
}
