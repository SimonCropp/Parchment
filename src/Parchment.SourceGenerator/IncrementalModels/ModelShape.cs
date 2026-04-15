/// <summary>
/// Flat snapshot of a reachable model type graph. <see cref="Types"/> is ordered by BFS from
/// <see cref="RootTypeFullyQualifiedName"/> so equality is deterministic.
/// </summary>
sealed record ModelShape(
    string RootTypeFullyQualifiedName,
    EquatableArray<TypeEntry> Types);
