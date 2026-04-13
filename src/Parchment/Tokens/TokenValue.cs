namespace Parchment.Tokens;

public abstract class TokenValue
{
    public static TokenValue Text(string value) =>
        new TextToken(value);

    public static TokenValue Markdown(string markdown) =>
        new MarkdownToken(markdown);

    public static TokenValue OpenXml(Func<IOpenXmlContext, IEnumerable<OpenXmlElement>> render) =>
        new OpenXmlToken(render);

    public static implicit operator TokenValue(string text) =>
        Text(text);

    internal sealed class TextToken(string value) :
        TokenValue
    {
        public string Value { get; } = value;
    }

    internal sealed class MarkdownToken(string markdown) :
        TokenValue
    {
        public string Source { get; } = markdown;
    }

    internal sealed class OpenXmlToken(Func<IOpenXmlContext, IEnumerable<OpenXmlElement>> render) :
        TokenValue
    {
        public Func<IOpenXmlContext, IEnumerable<OpenXmlElement>> Render { get; } = render;
    }
}
