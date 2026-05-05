namespace Parchment;

/// <summary>
/// Returned from a <see cref="TokenValue"/>-typed model property to render markdown source at
/// the substitution site. The host paragraph is replaced with the rendered markdown blocks
/// (paragraphs, lists, tables, etc.). Use when the value is supplied as markdown text and a
/// plain <c>string</c> property would only stringify it.
/// </summary>
public class MarkdownToken(string markdown) :
    TokenValue
{
    public string Source { get; } = markdown;
}