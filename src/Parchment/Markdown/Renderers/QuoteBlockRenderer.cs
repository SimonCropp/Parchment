class QuoteBlockRenderer :
    MarkdownObjectRenderer<OpenXmlMarkdownRenderer, QuoteBlock>
{
    protected override void Write(OpenXmlMarkdownRenderer renderer, QuoteBlock quoteBlock)
    {
        foreach (var child in quoteBlock)
        {
            if (child is LeafBlock leaf && child is not HtmlBlock && child is not CodeBlock && child is not ThematicBreakBlock)
            {
                var properties = new ParagraphProperties
                {
                    ParagraphStyleId = new()
                    {
                        Val = "Quote"
                    }
                };
                renderer.WriteLeafInline(leaf);
                renderer.FlushParagraph(properties);
            }
            else
            {
                renderer.PushIndent(720);
                try
                {
                    renderer.Render(child);
                }
                finally
                {
                    renderer.PopIndent();
                }
            }
        }
    }
}
