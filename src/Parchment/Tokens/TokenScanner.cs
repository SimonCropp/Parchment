namespace Parchment.Tokens;

/// <summary>
/// Scans every paragraph of a given part for Fluid tokens, classifies each paragraph, and injects
/// anchor bookmarks into token-bearing paragraphs so they can be located by name at render time.
/// </summary>
internal static class TokenScanner
{
    public static List<ParagraphClassification> Scan(OpenXmlCompositeElement partRoot, string templateName, string partUri)
    {
        var results = new List<ParagraphClassification>();
        foreach (var paragraph in partRoot.Descendants<Paragraph>().ToList())
        {
            var classification = ClassifyParagraph(paragraph, templateName, partUri);
            if (classification.Kind == ParagraphKind.Static)
            {
                continue;
            }

            var anchor = Anchors.EnsureOn(paragraph);
            results.Add(classification with { AnchorName = anchor });
        }

        return results;
    }

    static ParagraphClassification ClassifyParagraph(Paragraph paragraph, string templateName, string partUri)
    {
        var text = ParagraphText.Build(paragraph);
        var innerText = text.InnerText;
        var matches = TokenRegex.Tokens.Matches(innerText);
        if (matches.Count == 0)
        {
            return new(paragraph, string.Empty, ParagraphKind.Static, [], null);
        }

        var substitutions = new List<DocxTokenSite>();
        var blocks = new List<BlockMarker>();

        foreach (Match match in matches)
        {
            var source = match.Value;
            if (source.StartsWith("{{", StringComparison.Ordinal))
            {
                substitutions.Add(ParseSubstitution(source, match.Index, templateName, partUri));
            }
            else
            {
                blocks.Add(ParseBlockTag(source, templateName, partUri));
            }
        }

        if (blocks.Count > 1)
        {
            throw new ParchmentRegistrationException(
                templateName,
                "A paragraph contains more than one block tag. Place each block tag in its own paragraph.",
                partUri,
                string.Join(" ", blocks.Select(x => x.Source)));
        }

        if (blocks.Count == 1)
        {
            var nonBlockText = TokenRegex.Tokens.Replace(innerText, match => match.Value.StartsWith("{%", StringComparison.Ordinal) ? "" : match.Value);
            if (!string.IsNullOrWhiteSpace(nonBlockText) && substitutions.Count == 0)
            {
                // Block tag paragraph also contains static text - that's fine.
            }

            if (substitutions.Count > 0)
            {
                throw new ParchmentRegistrationException(
                    templateName,
                    "A paragraph mixes block tags with substitution tokens. Block tags must sit in their own paragraphs.",
                    partUri,
                    blocks[0].Source);
            }

            var nonBlockContent = TokenRegex.Tokens.Replace(innerText, "").Trim();
            if (nonBlockContent.Length > 0)
            {
                throw new ParchmentRegistrationException(
                    templateName,
                    "A paragraph contains a block tag alongside non-token text. Block tags must sit in their own paragraphs.",
                    partUri,
                    blocks[0].Source);
            }

            return new(paragraph, string.Empty, ParagraphKind.Block, [], blocks[0]);
        }

        return new(paragraph, string.Empty, ParagraphKind.Substitution, substitutions, null);
    }

    static DocxTokenSite ParseSubstitution(string source, int offset, string templateName, string partUri)
    {
        if (!SharedFluid.Parser.TryParse(source, out var template, out var error))
        {
            throw new ParchmentRegistrationException(
                templateName,
                $"Failed to parse liquid token: {error}",
                partUri,
                source);
        }

        var refs = IdentifierVisitor.Collect(template);
        return new(offset, source.Length, source, template, refs);
    }

    static BlockMarker ParseBlockTag(string source, string templateName, string partUri)
    {
        var tagMatch = TokenRegex.BlockTag.Match(source);
        if (!tagMatch.Success)
        {
            throw new ParchmentRegistrationException(
                templateName,
                $"Malformed block tag: {source}",
                partUri,
                source);
        }

        var tag = tagMatch.Groups["tag"].Value;
        var expression = tagMatch.Groups["expr"].Success ? tagMatch.Groups["expr"].Value.Trim() : null;

        switch (tag)
        {
            case "for":
                return BuildForTag(source, expression, templateName, partUri);
            case "endfor":
                return new(BlockTagKind.EndFor, source, null, null, null, null, []);
            case "if":
                return BuildIfTag(source, expression, templateName, partUri);
            case "elsif":
            case "elseif":
                return BuildElsifTag(source, expression, templateName, partUri);
            case "else":
                return new(BlockTagKind.Else, source, null, null, null, null, []);
            case "endif":
                return new(BlockTagKind.EndIf, source, null, null, null, null, []);
            default:
                throw new ParchmentRegistrationException(
                    templateName,
                    $"Unsupported block tag '{tag}'. Supported: for, endfor, if, elsif, else, endif",
                    partUri,
                    source);
        }
    }

    static BlockMarker BuildForTag(string source, string? expression, string templateName, string partUri)
    {
        if (expression == null)
        {
            throw new ParchmentRegistrationException(
                templateName,
                "{% for %} tag is missing its loop expression",
                partUri,
                source);
        }

        var match = TokenRegex.ForExpression.Match(expression);
        if (!match.Success)
        {
            throw new ParchmentRegistrationException(
                templateName,
                $"Malformed {{% for %}} expression: {expression}",
                partUri,
                source);
        }

        var loopVar = match.Groups["var"].Value;
        var sourceExpr = match.Groups["source"].Value.Trim();

        var sourceLiquid = $"{{{{ {sourceExpr} }}}}";
        if (!SharedFluid.Parser.TryParse(sourceLiquid, out var template, out var error))
        {
            throw new ParchmentRegistrationException(
                templateName,
                $"Failed to parse for-loop source expression: {error}",
                partUri,
                source);
        }

        var refs = IdentifierVisitor.Collect(template);
        return new(BlockTagKind.For, source, expression, null, loopVar, template, refs);
    }

    static BlockMarker BuildIfTag(string source, string? expression, string templateName, string partUri)
    {
        if (expression == null)
        {
            throw new ParchmentRegistrationException(
                templateName,
                "{% if %} tag is missing its condition",
                partUri,
                source);
        }

        var conditionLiquid = $"{{% if {expression} %}}true{{% else %}}false{{% endif %}}";
        if (!SharedFluid.Parser.TryParse(conditionLiquid, out var template, out var error))
        {
            throw new ParchmentRegistrationException(
                templateName,
                $"Failed to parse if condition: {error}",
                partUri,
                source);
        }

        var refs = IdentifierVisitor.Collect(template);
        return new(BlockTagKind.If, source, expression, template, null, null, refs);
    }

    static BlockMarker BuildElsifTag(string source, string? expression, string templateName, string partUri)
    {
        if (expression == null)
        {
            throw new ParchmentRegistrationException(
                templateName,
                "{% elsif %} tag is missing its condition",
                partUri,
                source);
        }

        var conditionLiquid = $"{{% if {expression} %}}true{{% else %}}false{{% endif %}}";
        if (!SharedFluid.Parser.TryParse(conditionLiquid, out var template, out var error))
        {
            throw new ParchmentRegistrationException(
                templateName,
                $"Failed to parse elsif condition: {error}",
                partUri,
                source);
        }

        var refs = IdentifierVisitor.Collect(template);
        return new(BlockTagKind.ElsIf, source, expression, template, null, null, refs);
    }
}
