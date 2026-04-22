/// <summary>
/// Registration-time validator for substitution tokens that resolve to a string property
/// annotated as html or markdown. These tokens become structural replacements at render time
/// (paragraph swap), so the same two rules as <see cref="ExcelsiorTokenValidator"/> apply:
/// token alone in its paragraph, and plain member-access expression (no filters / arithmetic
/// / literals).
/// </summary>
static class FormatTokenValidator
{
    public static void Validate(
        IReadOnlyList<ParagraphClassification> classifications,
        FormatMap formats,
        string templateName,
        string partUri)
    {
        if (formats.IsEmpty)
        {
            return;
        }

        foreach (var classification in classifications)
        {
            if (classification.Kind != ParagraphKind.Substitution)
            {
                continue;
            }

            var paragraphText = ParagraphText.Build(classification.Paragraph).InnerText;
            foreach (var token in classification.Substitutions)
            {
                if (token.References.Count == 0)
                {
                    continue;
                }

                var dottedPath = string.Join('.', token.References[0].Segments);
                if (!formats.TryGet(dottedPath, out var entry))
                {
                    continue;
                }

                RequireSoloInParagraph(
                    token,
                    classification.Substitutions.Count,
                    paragraphText,
                    entry.Kind,
                    templateName,
                    partUri);

                RequirePlainIdentifier(token, entry.Kind, templateName, partUri);
            }
        }
    }

    static void RequireSoloInParagraph(
        DocxTokenSite token,
        int siblingTokenCount,
        string paragraphText,
        FormatKind kind,
        string templateName,
        string partUri)
    {
        if (siblingTokenCount == 1 && token.Offset == 0 && token.Length == paragraphText.Length)
        {
            return;
        }

        throw new ParchmentRegistrationException(
            templateName,
            $"""
             [{kind}] token '{token.Source}' must sit alone in its own paragraph.
             Structural replacement swaps the entire host paragraph, so any surrounding text or sibling tokens would be discarded.
             """,
            partUri,
            token.Source);
    }

    static void RequirePlainIdentifier(DocxTokenSite token, FormatKind kind, string templateName, string partUri)
    {
        var statements = ((Fluid.Parser.FluidTemplate) token.Template).Statements;
        if (statements.Count == 0)
        {
            return;
        }

        if (statements[0] is not OutputStatement output)
        {
            return;
        }

        if (output.Expression is MemberExpression)
        {
            return;
        }

        throw new ParchmentRegistrationException(
            templateName,
            $$$"""
               [{{{kind}}}] token '{{{token.Source}}}' must be a plain member-access expression (for example '{{ Body }}' or '{{ Customer.Bio }}').
               Filters, arithmetic, and literal expressions are not supported — the property's formatted rendering is selected by attribute, so filters would not be applied.
               """,
            partUri,
            token.Source);
    }
}
