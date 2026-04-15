namespace Parchment.Markdown.Renderers;

class HtmlInlineRenderer :
    MarkdownObjectRenderer<OpenXmlMarkdownRenderer, HtmlInline>
{
    protected override void Write(OpenXmlMarkdownRenderer renderer, HtmlInline inline)
    {
        var tag = inline.Tag;
        if (tag.Length == 0)
        {
            return;
        }

        var run = new Run(new Text(tag) { Space = SpaceProcessingModeValues.Preserve });
        renderer.AddRun(run);
    }
}
