namespace Parchment;

public abstract class TokenValue
{
    public static implicit operator TokenValue(string text) =>
        new TextToken(text);
}