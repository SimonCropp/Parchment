namespace Parchment.Tokens;

internal static class TokenRegex
{
    // Captures the tag name and expression of a block tag like `{% for item in items %}`.
    public static readonly Regex BlockTag = new(
        @"^\{%\s*(?<tag>\w+)(?:\s+(?<expr>.*?))?\s*%\}$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);
}
