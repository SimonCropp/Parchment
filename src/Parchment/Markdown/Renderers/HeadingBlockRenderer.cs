class HeadingBlockRenderer :
    MarkdownObjectRenderer<OpenXmlMarkdownRenderer, HeadingBlock>
{
    protected override void Write(OpenXmlMarkdownRenderer renderer, HeadingBlock block)
    {
        var level = Math.Clamp(block.Level + renderer.HeadingOffset, 1, 9);
        var styleId = ResolveStyle(block, level);
        var properties = new ParagraphProperties
        {
            ParagraphStyleId = new() { Val = styleId }
        };
        renderer.WriteLeafInline(block);
        renderer.FlushParagraph(properties);
    }

    static string ResolveStyle(HeadingBlock block, int level)
    {
        var attributes = block.TryGetAttributes();
        var cls = attributes?.Classes?.FirstOrDefault();
        return cls ?? $"Heading{level}";
    }
}

static class RendererInlineExtensions
{
    public static void WriteLeafInline(this OpenXmlMarkdownRenderer renderer, LeafBlock block)
    {
        if (block.Inline != null)
        {
            renderer.WriteChildren(block.Inline);
        }
    }

    public static void WriteChildren(this OpenXmlMarkdownRenderer renderer, ContainerInline inline)
    {
        var child = inline.FirstChild;
        while (child != null)
        {
            renderer.Render(child);
            child = child.NextSibling;
        }
    }
}
