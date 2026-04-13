namespace Parchment.Tokens;

internal static class TokenRegex
{
    // Matches `{{ ... }}` (substitution) and `{% ... %}` (block tag), non-greedy.
    // The unified regex returns a single match per token site; the caller classifies by the opening chars.
    public static readonly Regex Tokens = new(
        @"\{\{[^{}]*?\}\}|\{%[^{%]*?%\}",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    // Captures the tag name and expression of a block tag like `{% for item in items %}`.
    public static readonly Regex BlockTag = new(
        @"^\{%\s*(?<tag>\w+)(?:\s+(?<expr>.*?))?\s*%\}$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    // Captures the loop variable and source expression of `for x in y`.
    public static readonly Regex ForExpression = new(
        @"^(?<var>\w+)\s+in\s+(?<source>.+)$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);
}
