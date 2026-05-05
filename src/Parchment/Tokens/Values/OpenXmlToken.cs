namespace Parchment;

/// <summary>
/// Returned from a <see cref="TokenValue"/>-typed model property when the value can't be
/// expressed as markdown or HTML — the lowest-level structural-replacement hatch. The
/// <paramref name="render"/> delegate emits raw OpenXML elements (paragraphs, tables, anything
/// block-level) at the substitution site, with access to numbering, image parts, and styles
/// via <see cref="IOpenXmlContext"/>. Used for generated tables, charts, and custom-styled
/// lists.
/// </summary>
public class OpenXmlToken(Func<IOpenXmlContext, IEnumerable<OpenXmlElement>> render) :
    TokenValue
{
    internal static readonly OpenXmlToken Empty = new(_ => []);

    public Func<IOpenXmlContext, IEnumerable<OpenXmlElement>> Render { get; } = render;
}