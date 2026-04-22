/// <summary>
/// Cache of <see cref="ExcelsiorTableAttribute"/>-marked properties reachable from a model type,
/// keyed by their dotted path from the root (e.g. <c>Customer.Lines</c>). Built once at template
/// registration time so render-time lookup is a single dictionary hit.
/// </summary>
sealed class ExcelsiorTableMap
{
    readonly Dictionary<string, ExcelsiorTableEntry> entries;

    ExcelsiorTableMap(Dictionary<string, ExcelsiorTableEntry> entries) =>
        this.entries = entries;

    public bool IsEmpty => entries.Count == 0;

    public bool TryGet(string dottedPath, [NotNullWhen(true)] out ExcelsiorTableEntry? entry) =>
        entries.TryGetValue(dottedPath, out entry);

    public static ExcelsiorTableMap Build(Type modelType, string templateName)
    {
        var entries = new Dictionary<string, ExcelsiorTableEntry>(StringComparer.OrdinalIgnoreCase);
        var visited = new HashSet<Type> { modelType };
        WalkType(modelType, [], static root => root, entries, visited, templateName);
        return new(entries);
    }

    static void WalkType(
        Type type,
        List<string> pathSegments,
        Func<object, object?> getter,
        Dictionary<string, ExcelsiorTableEntry> entries,
        HashSet<Type> visited,
        string templateName)
    {
        foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (!property.CanRead)
            {
                continue;
            }

            var nextSegments = new List<string>(pathSegments) { property.Name };
            var nextGetter = ChainGetter(getter, property);

            if (property.GetCustomAttribute<ExcelsiorTableAttribute>() != null)
            {
                var elementType = ModelValidator.TryResolveElementType(property.PropertyType);
                if (elementType == null || elementType == typeof(char))
                {
                    throw new ParchmentRegistrationException(
                        templateName,
                        $"[ExcelsiorTable] property '{type.Name}.{property.Name}' must be an IEnumerable<T> (excluding string).");
                }

                var dottedPath = string.Join('.', nextSegments);
                entries[dottedPath] = new(dottedPath, elementType, nextGetter);
                // Don't descend into the element type — collection items become Excelsior columns.
                continue;
            }

            // Descend into POCO properties looking for nested [ExcelsiorTable] members. Skip leaves
            // (primitives, strings, dates, etc.) and collection types (loops handle those). Track
            // visited types per branch so self-referential models don't recurse forever; the same
            // type can still appear at multiple unrelated paths.
            var underlying = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
            if (!ShouldDescend(underlying))
            {
                continue;
            }

            if (!visited.Add(underlying))
            {
                continue;
            }

            WalkType(underlying, nextSegments, nextGetter, entries, visited, templateName);
            visited.Remove(underlying);
        }
    }

    static Func<object, object?> ChainGetter(Func<object, object?> upstream, PropertyInfo property) =>
        root =>
        {
            var parent = upstream(root);
            return parent == null ? null : property.GetValue(parent);
        };

    static bool ShouldDescend(Type type)
    {
        if (type.IsPrimitive || type.IsEnum)
        {
            return false;
        }

        if (type == typeof(string) ||
            type == typeof(decimal) ||
            type == typeof(DateTime) ||
            type == typeof(DateTimeOffset) ||
            type == typeof(Date) ||
            type == typeof(Time) ||
            type == typeof(TimeSpan) ||
            type == typeof(Guid) ||
            type == typeof(Uri))
        {
            return false;
        }

        return !typeof(IEnumerable).IsAssignableFrom(type);
    }
}