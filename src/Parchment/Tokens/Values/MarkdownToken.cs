class MarkdownToken(string markdown) :
    TokenValue
{
    public string Source { get; } = markdown;
}