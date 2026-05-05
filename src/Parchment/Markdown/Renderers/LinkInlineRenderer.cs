class LinkInlineRenderer :
    MarkdownObjectRenderer<OpenXmlMarkdownRenderer, LinkInline>
{
    protected override void Write(OpenXmlMarkdownRenderer renderer, LinkInline inline)
    {
        var url = inline.Url ?? string.Empty;
        if (string.IsNullOrEmpty(url))
        {
            renderer.WriteChildren(inline);
            return;
        }

        var relId = renderer.MainPart.AddHyperlinkRelationship(new(url, UriKind.RelativeOrAbsolute), true).Id;

        var top = renderer.Top;
        var before = top.CurrentRuns.Count;
        renderer.WriteChildren(inline);
        var produced = top.CurrentRuns.Skip(before).ToList();

        var hyperlink = new Hyperlink
        {
            Id = relId
        };
        foreach (var run in produced)
        {
            if (run is Run runElement)
            {
                runElement.RunProperties ??= new();
                runElement.RunProperties.Append(
                    new RunStyle
                    {
                        Val = "Hyperlink"
                    });
                hyperlink.Append(runElement);
            }
            else
            {
                hyperlink.Append(run);
            }
        }

        top.CurrentRuns.RemoveRange(before, top.CurrentRuns.Count - before);
        renderer.AddRun(hyperlink);
    }
}
