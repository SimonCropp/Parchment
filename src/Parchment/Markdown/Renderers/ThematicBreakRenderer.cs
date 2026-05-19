class ThematicBreakRenderer :
    MarkdownObjectRenderer<OpenXmlMarkdownRenderer, ThematicBreakBlock>
{
    protected override void Write(OpenXmlMarkdownRenderer renderer, ThematicBreakBlock block)
    {
        var properties = new ParagraphProperties(
            new ParagraphBorders(
                new BottomBorder
                {
                    Val = BorderValues.Single,
                    Size = 6,
                    Space = 1
                }));
        var indent = renderer.CurrentIndent;
        if (indent > 0)
        {
            properties.Append(
                new Indentation
                {
                    Left = indent.ToString(CultureInfo.InvariantCulture)
                });
        }

        var paragraph = new Paragraph(properties);
        renderer.AddBlock(paragraph);
    }
}
