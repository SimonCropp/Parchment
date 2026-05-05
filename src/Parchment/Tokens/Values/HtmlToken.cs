namespace Parchment;

/// <summary>
/// Returned from a <see cref="TokenValue"/>-typed model property to render HTML source at the
/// substitution site. The host paragraph is replaced with the converted Word elements (via
/// OpenXmlHtml). Use when the value is supplied as HTML text — common for content authored in
/// a CMS or WYSIWYG editor.
/// </summary>
public class HtmlToken(string html) :
    TokenValue
{
    public string Source { get; } = html;
}