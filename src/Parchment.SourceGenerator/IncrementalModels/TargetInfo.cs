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
