namespace Parchment.Markdown.Renderers;

internal sealed class HtmlBlockRenderer :
    MarkdownObjectRenderer<OpenXmlMarkdownRenderer, HtmlBlock>
{
    protected override void Write(OpenXmlMarkdownRenderer renderer, HtmlBlock block)
    {
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
