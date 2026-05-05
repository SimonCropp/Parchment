namespace Parchment;

public abstract class TokenValue
{
    public static TokenValue Text(string value) =>
        new TextToken(value);

    public static TokenValue Markdown(string markdown) =>
        new MarkdownToken(markdown);

    public static TokenValue Html(string html) =>
        new HtmlToken(html);

    public static TokenValue OpenXml(Func<IOpenXmlContext, IEnumerable<OpenXmlElement>> render) =>
        new OpenXmlToken(render);

    public static TokenValue Mutate(Action<Paragraph, IOpenXmlContext> mutate) =>
        new MutateToken(mutate);

    public static implicit operator TokenValue(string text) =>
        Text(text);
}