namespace Parchment.SourceGenerator;

/// <summary>
/// Validation-time counterpart to <see cref="ShapeBuilder"/>. Resolves liquid member paths
/// against a pre-baked <see cref="ModelShape"/> instead of live symbols.
/// </summary>
static class ShapeResolver
{
    public static string? Resolve(
        ModelShape shape,
        string[] segments,
        IReadOnlyDictionary<string, string> scope)
    {
        if (segments.Length == 0)
        {
            return null;
        }

        string currentFqn;
        int start;
        if (scope.TryGetValue(segments[0], out var scoped))
        {
            currentFqn = scoped;
            start = 1;
        }
        else
        {
            currentFqn = shape.RootTypeFullyQualifiedName;
            start = 0;
        }

        for (var i = start; i < segments.Length; i++)
        {
            var entry = FindType(shape, currentFqn);
            if (entry == null)
            {
                return null;
            }

            string? matched = null;
            foreach (var member in entry.Members)
            {
                if (string.Equals(member.Name, segments[i], StringComparison.OrdinalIgnoreCase))
                {
                    matched = member.TypeFullyQualifiedName;
                    break;
                }
            }

            if (matched == null)
            {
                return null;
            }

            currentFqn = matched;
        }

        return currentFqn;
    }

    public static string? GetElementType(ModelShape shape, string typeFqn) =>
        FindType(shape, typeFqn)?.ElementTypeFullyQualifiedName;

    static TypeEntry? FindType(ModelShape shape, string typeFqn)
    {
        foreach (var entry in shape.Types)
        {
            if (entry.TypeFullyQualifiedName == typeFqn)
            {
                return entry;
            }
        }

        return null;
    }
}
