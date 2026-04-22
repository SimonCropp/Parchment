class QuoteBlockRenderer :
    MarkdownObjectRenderer<OpenXmlMarkdownRenderer, QuoteBlock>
{
    protected override void Write(OpenXmlMarkdownRenderer renderer, QuoteBlock quoteBlock)
    {
        foreach (var child in quoteBlock)
        {
            if (child is LeafBlock leaf)
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
                renderer.Render(child);
            }
        }
    }
}
