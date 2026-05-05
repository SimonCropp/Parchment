namespace Parchment;

/// <summary>
/// Returned from a <see cref="TokenValue"/>-typed model property to mutate the host paragraph
/// in place rather than replace it. The token text is cleared before the callback runs, so the
/// paragraph's properties (style, alignment, indentation) survive. Use when the goal is to add
/// runs with custom formatting, inject bookmarks, or tweak paragraph properties — anything
/// that needs to keep the source paragraph as the carrier.
/// </summary>
public class MutateToken(Action<Paragraph, IOpenXmlContext> mutate) :
    TokenValue
{
    public Action<Paragraph, IOpenXmlContext> Apply { get; } = mutate;
}