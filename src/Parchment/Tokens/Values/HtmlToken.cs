class HtmlToken(string html) :
    TokenValue
{
    public string Source { get; } = html;
}