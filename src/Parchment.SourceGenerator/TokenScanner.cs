namespace Parchment.SourceGenerator;

public static class TokenScanner
{
    static readonly Regex TokenRegex = new(
        @"\{\{[^{}]*?\}\}|\{%[^{%]*?%\}",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    static readonly Regex BlockTagRegex = new(
        @"^\{%\s*(?<tag>\w+)(?:\s+(?<expr>.*?))?\s*%\}$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    static readonly FluidParser parser = new();

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
                    result.Add(ParseSubstitution(source, paragraph, hasOtherContent));
                }
                else
                {
                    result.Add(ParseBlockTag(source, paragraph, hasOtherContent));
                }
            }
        }

        return result;
    }

    static Token ParseSubstitution(string source, string paragraph, bool hasOtherContent)
    {
        if (!parser.TryParse(source, out var template, out _))
        {
            return new(TokenKind.Substitution, source, [], null, null, paragraph, hasOtherContent);
        }

        var references = IdentifierVisitor.Collect(template);
        return new(TokenKind.Substitution, source, references, null, null, paragraph, hasOtherContent);
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
                return BuildForTag(source, expression, paragraph, hasOtherContent);

            case "endfor":
                return new(TokenKind.ForClose, source, [], null, null, paragraph, hasOtherContent);

            case "if":
                return BuildConditional(TokenKind.IfOpen, source, expression, paragraph, hasOtherContent);

            case "elsif":
            case "elseif":
                return BuildConditional(TokenKind.ElsIf, source, expression, paragraph, hasOtherContent);

            case "else":
                return new(TokenKind.Else, source, [], null, null, paragraph, hasOtherContent);

            case "endif":
                return new(TokenKind.IfClose, source, [], null, null, paragraph, hasOtherContent);

            default:
                return new(TokenKind.UnknownBlock, source, [], null, null, paragraph, hasOtherContent);
        }
    }

    static Token BuildForTag(string source, string? expression, string paragraph, bool hasOtherContent)
    {
        if (expression == null)
        {
            return new(TokenKind.UnknownBlock, source, [], null, null, paragraph, hasOtherContent);
        }

        // Wrap the tag so Fluid yields a real ForStatement. We can then read the loop variable
        // (Identifier) and source expression directly off the AST instead of regex-parsing them.
        var liquid = $"{{% for {expression} %}}{{% endfor %}}";
        if (!parser.TryParse(liquid, out var template, out _))
        {
            return new(TokenKind.UnknownBlock, source, [], null, null, paragraph, hasOtherContent);
        }

        var forStatement = ((FluidTemplate)template).Statements
            .OfType<ForStatement>()
            .FirstOrDefault();
        if (forStatement == null)
        {
            return new(TokenKind.UnknownBlock, source, [], null, null, paragraph, hasOtherContent);
        }

        var references = IdentifierVisitor.Collect(template);
        return new(TokenKind.ForOpen, source, references, forStatement.Identifier, expression, paragraph, hasOtherContent);
    }

    static Token BuildConditional(TokenKind kind, string source, string? expression, string paragraph, bool hasOtherContent)
    {
        if (expression == null)
        {
            return new(TokenKind.UnknownBlock, source, [], null, null, paragraph, hasOtherContent);
        }

        var liquid = $"{{% if {expression} %}}{{% endif %}}";
        if (!parser.TryParse(liquid, out var template, out _))
        {
            return new(TokenKind.UnknownBlock, source, [], null, null, paragraph, hasOtherContent);
        }

        var ifStatement = ((FluidTemplate)template).Statements
            .OfType<IfStatement>()
            .FirstOrDefault();
        if (ifStatement == null)
        {
            return new(TokenKind.UnknownBlock, source, [], null, null, paragraph, hasOtherContent);
        }

        var references = IdentifierVisitor.Collect(template);
        return new(kind, source, references, null, null, paragraph, hasOtherContent);
    }
}

sealed class IdentifierVisitor :
    AstVisitor
{
    readonly List<string[]> paths = [];

    public static IReadOnlyList<string[]> Collect(IFluidTemplate template)
    {
        var visitor = new IdentifierVisitor();
        visitor.VisitTemplate(template);
        return visitor.paths;
    }

    protected override Expression VisitMemberExpression(MemberExpression memberExpression)
    {
        var segments = new List<string>(memberExpression.Segments.Count);
        foreach (var segment in memberExpression.Segments)
        {
            if (segment is IdentifierSegment identifier)
            {
                segments.Add(identifier.Identifier);
            }
            else
            {
                break;
            }
        }

        if (segments.Count > 0)
        {
            paths.Add(segments.ToArray());
        }

        return base.VisitMemberExpression(memberExpression);
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
