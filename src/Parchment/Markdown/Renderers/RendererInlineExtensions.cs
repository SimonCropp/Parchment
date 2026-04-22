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