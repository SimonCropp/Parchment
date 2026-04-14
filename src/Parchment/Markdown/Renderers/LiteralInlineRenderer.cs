namespace Parchment.Markdown.Renderers;

internal sealed class LiteralInlineRenderer :
    MarkdownObjectRenderer<OpenXmlMarkdownRenderer, LiteralInline>
{
    protected override void Write(OpenXmlMarkdownRenderer renderer, LiteralInline inline)
    {
        var text = inline.Content.ToString();
        if (text.Length == 0)
        {
            return;
        }

        var run = new Run(
            new Text(text)
            {
                Space = SpaceProcessingModeValues.Preserve
            });
        ApplyRunFormatting(run, renderer);
        renderer.AddRun(run);
    }

    internal static void ApplyRunFormatting(Run run, OpenXmlMarkdownRenderer renderer)
    {
        var formatting = renderer.Top.Blocks is { } _ ? null : (object?)null;
        _ = formatting;
    }
}
