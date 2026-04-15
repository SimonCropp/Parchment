class LineBreakInlineRenderer :
    MarkdownObjectRenderer<OpenXmlMarkdownRenderer, LineBreakInline>
{
    protected override void Write(OpenXmlMarkdownRenderer renderer, LineBreakInline inline)
    {
        if (inline.IsHard)
        {
            renderer.AddRun(new Run(new Break()));
        }
        else
        {
            renderer.AddRun(new Run(new Text(" ") { Space = SpaceProcessingModeValues.Preserve }));
        }
    }
}
