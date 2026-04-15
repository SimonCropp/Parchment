using Parchment.Markdown;

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

        var before = renderer.Top.CurrentRuns.Count;
        renderer.WriteChildren(inline);
        var produced = renderer.Top.CurrentRuns.Skip(before).ToList();

        var hyperlink = new Hyperlink { Id = relId };
        foreach (var run in produced)
        {
            if (run is Run runElement)
            {
                runElement.RunProperties ??= new();
                runElement.RunProperties.Append(new RunStyle { Val = "Hyperlink" });
                hyperlink.Append(runElement);
            }
            else
            {
                hyperlink.Append(run);
            }
        }

        renderer.Top.CurrentRuns.RemoveRange(before, renderer.Top.CurrentRuns.Count - before);
        renderer.AddRun(hyperlink);
    }
}
