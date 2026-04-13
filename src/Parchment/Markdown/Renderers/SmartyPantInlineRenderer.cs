namespace Parchment.Markdown.Renderers;

internal sealed class SmartyPantInlineRenderer :
    MarkdownObjectRenderer<OpenXmlMarkdownRenderer, Markdig.Extensions.SmartyPants.SmartyPant>
{
    protected override void Write(OpenXmlMarkdownRenderer renderer, Markdig.Extensions.SmartyPants.SmartyPant inline)
    {
        var glyph = inline.Type switch
        {
            Markdig.Extensions.SmartyPants.SmartyPantType.LeftQuote => "\u2018",
            Markdig.Extensions.SmartyPants.SmartyPantType.RightQuote => "\u2019",
            Markdig.Extensions.SmartyPants.SmartyPantType.LeftDoubleQuote => "\u201C",
            Markdig.Extensions.SmartyPants.SmartyPantType.RightDoubleQuote => "\u201D",
            Markdig.Extensions.SmartyPants.SmartyPantType.Dash2 => "\u2013",
            Markdig.Extensions.SmartyPants.SmartyPantType.Dash3 => "\u2014",
            Markdig.Extensions.SmartyPants.SmartyPantType.Ellipsis => "\u2026",
            _ => string.Empty
        };

        if (glyph.Length == 0)
        {
            return;
        }

        renderer.AddRun(new Run(new Text(glyph) { Space = SpaceProcessingModeValues.Preserve }));
    }
}
