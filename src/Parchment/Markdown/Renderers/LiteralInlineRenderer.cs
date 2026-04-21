class LiteralInlineRenderer :
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
            new Text(XmlCharSanitizer.Strip(text))
            {
                Space = SpaceProcessingModeValues.Preserve
            });
        renderer.AddRun(run);
    }
}
