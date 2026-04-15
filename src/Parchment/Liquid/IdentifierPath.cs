namespace Parchment.Liquid;

sealed record IdentifierPath(IReadOnlyList<string> Segments)
{
    public string Root => Segments[0];

    public override string ToString() =>
        string.Join('.', Segments);
}
