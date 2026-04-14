namespace Parchment.Markdown.Renderers;

internal sealed class HtmlBlockRenderer :
    MarkdownObjectRenderer<OpenXmlMarkdownRenderer, HtmlBlock>
{
    protected override void Write(OpenXmlMarkdownRenderer renderer, HtmlBlock block)
    {
        // Comment-only blocks (snippet markers, TODOs, authoring notes) would otherwise round-trip
        // through the HTML converter as empty paragraphs and bleed visible whitespace into the docx.
        if (block.Type == HtmlBlockType.Comment)
        {
            return;
        }

        var html = block.Lines.ToString();
        var settings = new OpenXmlHtml.HtmlConvertSettings
        {
            HeadingLevelOffset = renderer.HeadingOffset
        };
        var elements = OpenXmlHtml.WordHtmlConverter.ToElements(html, renderer.MainPart, settings);
        foreach (var element in elements)
        {
            renderer.AddBlock(element);
        }
    }
}
