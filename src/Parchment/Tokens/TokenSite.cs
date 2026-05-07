readonly struct TokenSite(int offset, int length, TokenSiteKind kind)
{
    public int Offset { get; } = offset;
    public int Length { get; } = length;
    public TokenSiteKind Kind { get; } = kind;
}
