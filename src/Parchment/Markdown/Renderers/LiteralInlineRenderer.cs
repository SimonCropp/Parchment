class LiteralInlineRenderer :
    MarkdownObjectRenderer<OpenXmlMarkdownRenderer, LiteralInline>
{
    protected override void Write(OpenXmlMarkdownRenderer renderer, LiteralInline inline)
    {
        var content = inline.Content.AsSpan();
        if (content.Length == 0)
        {
            return;
        }

        var run = new Run(
            new Text(XmlCharSanitizer.Strip(content).ToString())
            {
                Space = SpaceProcessingModeValues.Preserve
            });
        renderer.AddRun(run);
    }
}
