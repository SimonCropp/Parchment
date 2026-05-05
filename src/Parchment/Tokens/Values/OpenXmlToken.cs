class OpenXmlToken(Func<IOpenXmlContext, IEnumerable<OpenXmlElement>> render) :
    TokenValue
{
    public Func<IOpenXmlContext, IEnumerable<OpenXmlElement>> Render { get; } = render;
}