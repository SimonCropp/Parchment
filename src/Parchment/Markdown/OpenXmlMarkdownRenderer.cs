namespace Parchment.Markdown;

internal sealed class OpenXmlMarkdownRenderer :
    RendererBase
{
    readonly Stack<ContainerState> stack = new();

    public OpenXmlMarkdownRenderer(MainDocumentPart mainPart, int headingOffset = 0)
    {
        MainPart = mainPart;
        HeadingOffset = headingOffset;
        AvailableStyles = StyleSet.Read(mainPart);
        Numbering = new(mainPart);
        stack.Push(new());

        // Block renderers
        ObjectRenderers.Add(new Renderers.HeadingBlockRenderer());
        ObjectRenderers.Add(new Renderers.ParagraphBlockRenderer());
        ObjectRenderers.Add(new Renderers.ListBlockRenderer());
        ObjectRenderers.Add(new Renderers.QuoteBlockRenderer());
        ObjectRenderers.Add(new Renderers.TableRenderer());
        ObjectRenderers.Add(new Renderers.CodeBlockRenderer());
        ObjectRenderers.Add(new Renderers.ThematicBreakRenderer());
        ObjectRenderers.Add(new Renderers.HtmlBlockRenderer());

        // Inline renderers
        ObjectRenderers.Add(new Renderers.LiteralInlineRenderer());
        ObjectRenderers.Add(new Renderers.EmphasisInlineRenderer());
        ObjectRenderers.Add(new Renderers.LinkInlineRenderer());
        ObjectRenderers.Add(new Renderers.AutolinkInlineRenderer());
        ObjectRenderers.Add(new Renderers.CodeInlineRenderer());
        ObjectRenderers.Add(new Renderers.LineBreakInlineRenderer());
        ObjectRenderers.Add(new Renderers.HtmlInlineRenderer());
        ObjectRenderers.Add(new Renderers.SmartyPantInlineRenderer());
    }

    public MainDocumentPart MainPart { get; }
    public int HeadingOffset { get; }
    public StyleSet AvailableStyles { get; }
    public WordNumberingState Numbering { get; }

    internal ContainerState Top => stack.Peek();

    public override object Render(MarkdownObject markdownObject)
    {
        Write(markdownObject);
        return this;
    }

    public IReadOnlyList<OpenXmlElement> Drain() =>
        stack.Peek().Blocks;

    internal void PushContainer() =>
        stack.Push(new());

    internal ContainerState PopContainer() =>
        stack.Pop();

    internal void FlushParagraph(ParagraphProperties? properties = null)
    {
        var top = stack.Peek();
        if (top.CurrentRuns.Count == 0 && properties == null)
        {
            return;
        }

        var paragraph = new Paragraph();
        if (properties != null)
        {
            paragraph.ParagraphProperties = properties;
        }

        foreach (var run in top.CurrentRuns)
        {
            paragraph.Append(run);
        }

        top.Blocks.Add(paragraph);
        top.CurrentRuns.Clear();
    }

    internal void AddRun(OpenXmlElement run) =>
        stack.Peek().CurrentRuns.Add(run);

    internal void AddBlock(OpenXmlElement block)
    {
        FlushParagraph();
        stack.Peek().Blocks.Add(block);
    }
}

internal sealed class ContainerState
{
    public List<OpenXmlElement> Blocks { get; } = [];
    public List<OpenXmlElement> CurrentRuns { get; } = [];
}
