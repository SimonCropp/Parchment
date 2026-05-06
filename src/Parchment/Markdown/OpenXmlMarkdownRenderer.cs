class OpenXmlMarkdownRenderer :
    RendererBase
{
    readonly Stack<ContainerState> stack = new();

    public OpenXmlMarkdownRenderer(MainDocumentPart mainPart, WordNumberingState numbering, ImagePolicies imagePolicies, int headingOffset = 0)
    {
        MainPart = mainPart;
        HeadingOffset = headingOffset;
        AvailableStyles = StyleSet.Read(mainPart);
        Numbering = numbering;
        ImagePolicies = imagePolicies;
        stack.Push(new());

        // Block renderers
        ObjectRenderers.Add(new HeadingBlockRenderer());
        ObjectRenderers.Add(new ParagraphBlockRenderer());
        ObjectRenderers.Add(new ListBlockRenderer());
        ObjectRenderers.Add(new QuoteBlockRenderer());
        ObjectRenderers.Add(new TableRenderer());
        ObjectRenderers.Add(new CodeBlockRenderer());
        ObjectRenderers.Add(new ThematicBreakRenderer());
        ObjectRenderers.Add(new HtmlBlockRenderer());

        // Inline renderers
        ObjectRenderers.Add(new LiteralInlineRenderer());
        ObjectRenderers.Add(new EmphasisInlineRenderer());
        ObjectRenderers.Add(new LinkInlineRenderer());
        ObjectRenderers.Add(new AutolinkInlineRenderer());
        ObjectRenderers.Add(new CodeInlineRenderer());
        ObjectRenderers.Add(new LineBreakInlineRenderer());
        ObjectRenderers.Add(new HtmlInlineRenderer());
        ObjectRenderers.Add(new SmartyPantInlineRenderer());
    }

    public MainDocumentPart MainPart { get; }
    public int HeadingOffset { get; }
    public StyleSet AvailableStyles { get; }
    public WordNumberingState Numbering { get; }
    public ImagePolicies ImagePolicies { get; }

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