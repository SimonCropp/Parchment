namespace Parchment.Markdown.Renderers;

internal sealed class EmphasisInlineRenderer :
    MarkdownObjectRenderer<OpenXmlMarkdownRenderer, EmphasisInline>
{
    protected override void Write(OpenXmlMarkdownRenderer renderer, EmphasisInline inline)
    {
        var before = renderer.Top.CurrentRuns.Count;
        renderer.WriteChildren(inline);

        for (var i = before; i < renderer.Top.CurrentRuns.Count; i++)
        {
            if (renderer.Top.CurrentRuns[i] is Run run)
            {
                ApplyStyle(run, inline.DelimiterChar, inline.DelimiterCount);
            }
        }
    }

    static void ApplyStyle(Run run, char delimiter, int count)
    {
        run.RunProperties ??= new();
        switch (delimiter)
        {
            case '*':
            case '_':
                if (count >= 2)
                {
                    run.RunProperties.Append(new Bold());
                }
                else
                {
                    run.RunProperties.Append(new Italic());
                }

                break;
            case '~':
                if (count >= 2)
                {
                    run.RunProperties.Append(new Strike());
                }
                else
                {
                    run.RunProperties.Append(new VerticalTextAlignment { Val = VerticalPositionValues.Subscript });
                }

                break;
            case '^':
                run.RunProperties.Append(new VerticalTextAlignment { Val = VerticalPositionValues.Superscript });
                break;
            case '+':
                run.RunProperties.Append(new Underline { Val = UnderlineValues.Single });
                break;
            case '=':
                run.RunProperties.Append(new Highlight { Val = HighlightColorValues.Yellow });
                break;
        }
    }
}
