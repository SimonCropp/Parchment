namespace Parchment;

/// <summary>
/// The polymorphic carrier for substitution values that need richer behaviour than plain text.
/// Type a model property as <see cref="TokenValue"/> when its value may be plain text on one
/// render and rich content (markdown, HTML, raw OpenXML, or in-place mutation) on another —
/// the token subclass picked at assignment time decides how the host paragraph is treated:
/// <list type="bullet">
/// <item><description><see cref="MarkdownToken"/> — render markdown source as Word blocks.</description></item>
/// <item><description><see cref="HtmlToken"/> — convert HTML source to Word elements.</description></item>
/// <item><description><see cref="OpenXmlToken"/> — emit raw OpenXML elements via a callback.</description></item>
/// <item><description><see cref="MutateToken"/> — mutate the host paragraph in place.</description></item>
/// </list>
/// A plain string assigned to a <see cref="TokenValue"/>-typed property flows through the
/// implicit conversion and behaves like any other text substitution.
/// </summary>
public abstract class TokenValue
{
    /// <summary>
    /// Allows a string to be assigned directly to a <see cref="TokenValue"/>-typed property
    /// (or returned from a method) without explicit construction. The result is treated as
    /// plain text substitution, identical to declaring the property as <c>string</c>.
    /// </summary>
    public static implicit operator TokenValue(string text) =>
        new TextToken(text);
}