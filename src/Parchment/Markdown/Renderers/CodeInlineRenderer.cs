namespace Parchment.Markdown.Renderers;

class CodeInlineRenderer :
    MarkdownObjectRenderer<OpenXmlMarkdownRenderer, CodeInline>
{
    protected override void Write(OpenXmlMarkdownRenderer renderer, CodeInline inline)
    {
        var run = new Run(
            new RunProperties(new RunFonts { Ascii = "Consolas", HighAnsi = "Consolas" }),
            new Text(inline.Content) { Space = SpaceProcessingModeValues.Preserve });
        renderer.AddRun(run);
    }
}
