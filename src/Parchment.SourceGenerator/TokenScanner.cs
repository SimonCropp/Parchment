namespace Parchment.SourceGenerator;

public static class TokenScanner
{
    static readonly Regex TokenRegex = new(
        @"\{\{[^{}]*?\}\}|\{%[^{%]*?%\}",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    static readonly Regex BlockTagRegex = new(
        @"^\{%\s*(?<tag>\w+)(?:\s+(?<expr>.*?))?\s*%\}$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    static readonly Regex ForExpressionRegex = new(
        @"^(?<var>\w+)\s+in\s+(?<source>.+)$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    static readonly Regex IdentifierChain = new(
        @"[A-Za-z_][A-Za-z_0-9]*(?:\.[A-Za-z_][A-Za-z_0-9]*)*",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static List<Token> Scan(IReadOnlyList<string> paragraphs)
    {
        var result = new List<Token>();
        foreach (var paragraph in paragraphs)
        {
            var matches = TokenRegex.Matches(paragraph);
            if (matches.Count == 0)
            {
                continue;
            }

            var nonTokenText = TokenRegex.Replace(paragraph, "").Trim();
            var hasOtherContent = nonTokenText.Length > 0;

            foreach (Match match in matches)
            {
                var source = match.Value;
                if (source.StartsWith("{{", StringComparison.Ordinal))
                {
                    var inner = source.Substring(2, source.Length - 4).Trim();
                    var pipeIndex = inner.IndexOf('|');
                    var expression = pipeIndex >= 0 ? inner.Substring(0, pipeIndex).Trim() : inner;
                    var references = ExtractReferences(expression);
                    result.Add(new(TokenKind.Substitution, source, references, LoopVariable: null, LoopSource: null, paragraph, hasOtherContent));
                }
                else
                {
                    result.Add(ParseBlockTag(source, paragraph, hasOtherContent));
                }
            }
        }

        return result;
    }

    static Token ParseBlockTag(string source, string paragraph, bool hasOtherContent)
    {
        var match = BlockTagRegex.Match(source);
        if (!match.Success)
        {
            return new(TokenKind.UnknownBlock, source, [], null, null, paragraph, hasOtherContent);
        }

        var tag = match.Groups["tag"].Value;
        var expression = match.Groups["expr"].Success ? match.Groups["expr"].Value.Trim() : null;

        switch (tag)
        {
            case "for":
                if (expression == null)
                {
                    return new(TokenKind.UnknownBlock, source, [], null, null, paragraph, hasOtherContent);
                }

                var forMatch = ForExpressionRegex.Match(expression);
                if (!forMatch.Success)
                {
                    return new(TokenKind.UnknownBlock, source, [], null, null, paragraph, hasOtherContent);
                }

                var loopVar = forMatch.Groups["var"].Value;
                var sourceExpr = forMatch.Groups["source"].Value.Trim();
                var sourceReferences = ExtractReferences(sourceExpr);
                return new(TokenKind.ForOpen, source, sourceReferences, loopVar, sourceExpr, paragraph, hasOtherContent);

            case "endfor":
                return new(TokenKind.ForClose, source, [], null, null, paragraph, hasOtherContent);

            case "if":
                if (expression == null)
                {
                    return new(TokenKind.UnknownBlock, source, [], null, null, paragraph, hasOtherContent);
                }

                return new(TokenKind.IfOpen, source, ExtractReferences(expression), null, null, paragraph, hasOtherContent);

            case "elsif":
            case "elseif":
                return new(
                    TokenKind.ElsIf,
                    source,
                    expression != null ? ExtractReferences(expression) : Array.Empty<string[]>(),
                    null,
                    null,
                    paragraph,
                    hasOtherContent);

            case "else":
                return new(TokenKind.Else, source, [], null, null, paragraph, hasOtherContent);

            case "endif":
                return new(TokenKind.IfClose, source, [], null, null, paragraph, hasOtherContent);

            default:
                return new(TokenKind.UnknownBlock, source, [], null, null, paragraph, hasOtherContent);
        }
    }

    static IReadOnlyList<string[]> ExtractReferences(string expression)
    {
        var result = new List<string[]>();
        foreach (Match match in IdentifierChain.Matches(expression))
        {
            if (IsLiteral(match.Value))
            {
                continue;
            }

            var segments = match.Value.Split('.');
            result.Add(segments);
        }

        return result;
    }

    static bool IsLiteral(string value)
    {
        if (value is
            "true" or
            "false" or
            "nil" or
            "null" or
            "empty" or
            "blank" or
            "and" or
            "or" or
            "contains" or
            "in" or
            "not")
        {
            return true;
        }

        return char.IsDigit(value[0]);
    }
}

public sealed record Token(
    TokenKind Kind,
    string Source,
    IReadOnlyList<string[]> References,
    string? LoopVariable,
    string? LoopSource,
    string Paragraph,
    bool HasOtherContent);
