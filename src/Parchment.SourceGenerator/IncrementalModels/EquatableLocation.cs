readonly record struct EquatableLocation(
    string? FilePath,
    TextSpan TextSpan,
    LinePositionSpan LineSpan)
{
    public static EquatableLocation From(Location location)
    {
        if (location == Location.None)
        {
            return new(null, default, default);
        }

        var mapped = location.GetLineSpan();
        return new(mapped.Path, location.SourceSpan, mapped.Span);
    }

    public Location ToLocation() =>
        FilePath is null
            ? Location.None
            : Location.Create(FilePath, TextSpan, LineSpan);
}
