/// <summary>
/// Snapshot of the styles defined in a docx's <see cref="StyleDefinitionsPart"/>, keyed by styleId.
/// </summary>
class StyleSet
{
    readonly Dictionary<string, StyleType> styles;

    StyleSet(Dictionary<string, StyleType> styles) =>
        this.styles = styles;

    public bool TryGet(string id, out StyleType type) =>
        styles.TryGetValue(id, out type);

    public bool Contains(string id) =>
        styles.ContainsKey(id);

    static StyleType MapStyleType(StyleValues? value)
    {
        if (value == null)
        {
            return StyleType.Paragraph;
        }

        if (value.Value == StyleValues.Paragraph)
        {
            return StyleType.Paragraph;
        }

        if (value.Value == StyleValues.Character)
        {
            return StyleType.Character;
        }

        if (value.Value == StyleValues.Table)
        {
            return StyleType.Table;
        }

        if (value.Value == StyleValues.Numbering)
        {
            return StyleType.Numbering;
        }

        return StyleType.Paragraph;
    }

    public static StyleSet Read(MainDocumentPart mainPart)
    {
        var result = new Dictionary<string, StyleType>(StringComparer.OrdinalIgnoreCase);
        var part = mainPart.StyleDefinitionsPart;
        if (part?.Styles == null)
        {
            return new(result);
        }

        foreach (var style in part.Styles.Elements<Style>())
        {
            var id = style.StyleId?.Value;
            if (id == null)
            {
                continue;
            }

            result[id] = MapStyleType(style.Type?.Value);
        }

        return new(result);
    }
}
