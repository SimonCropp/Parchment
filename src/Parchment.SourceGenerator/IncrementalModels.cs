namespace Parchment.SourceGenerator;

/// <summary>
/// Primitive-only snapshot of a <c>[ParchmentTemplate]</c>-decorated class. Every field is
/// value-equatable so this can flow through the incremental pipeline without pinning to a
/// specific <see cref="Compilation"/>. In particular, no <see cref="ISymbol"/> fields —
/// symbols change identity between compilations and would invalidate the cache on every edit.
/// </summary>
sealed record TargetInfo(
    string? DeclaringNamespace,
    string DeclaringName,
    string ModelFullyQualifiedName,
    string ModelSimpleName,
    string TemplatePath,
    EquatableLocation Location,
    ModelShape Shape);

/// <summary>
/// Flat snapshot of a reachable model type graph. <see cref="Types"/> is ordered by BFS from
/// <see cref="RootTypeFullyQualifiedName"/> so equality is deterministic.
/// </summary>
sealed record ModelShape(
    string RootTypeFullyQualifiedName,
    EquatableArray<TypeEntry> Types);

sealed record TypeEntry(
    string TypeFullyQualifiedName,
    string? ElementTypeFullyQualifiedName,
    EquatableArray<MemberEntry> Members);

sealed record MemberEntry(
    string Name,
    string TypeFullyQualifiedName);

sealed record DocxData(
    string Path,
    EquatableArray<string> Paragraphs,
    string? ReadError);

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
