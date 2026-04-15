class ThematicBreakRenderer :
    MarkdownObjectRenderer<OpenXmlMarkdownRenderer, ThematicBreakBlock>
{
    protected override void Write(OpenXmlMarkdownRenderer renderer, ThematicBreakBlock block)
    {
        var paragraph = new Paragraph(
            new ParagraphProperties(
                new ParagraphBorders(
                    new BottomBorder
                    {
                        Val = BorderValues.Single,
                        Size = 6,
                        Space = 1
                    })));
        renderer.AddBlock(paragraph);
    }
}
