using System.Diagnostics.CodeAnalysis;

/// <summary>
/// Cache of <see cref="ExcelsiorTableAttribute"/>-marked top-level properties on a model type,
/// resolved at template registration time so render-time lookup is just a dictionary hit.
/// </summary>
sealed class ExcelsiorTableMap
{
    readonly Dictionary<string, ExcelsiorTableEntry> entries;

    ExcelsiorTableMap(Dictionary<string, ExcelsiorTableEntry> entries) =>
        this.entries = entries;

    public bool IsEmpty => entries.Count == 0;

    public bool TryGet(string propertyName, [NotNullWhen(true)] out ExcelsiorTableEntry? entry) =>
        entries.TryGetValue(propertyName, out entry);

    public static ExcelsiorTableMap Build(Type modelType, string templateName)
    {
        var entries = new Dictionary<string, ExcelsiorTableEntry>(StringComparer.OrdinalIgnoreCase);
        foreach (var property in modelType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (property.GetCustomAttribute<ExcelsiorTableAttribute>() == null)
            {
                continue;
            }

            if (!property.CanRead)
            {
                throw new ParchmentRegistrationException(
                    templateName,
                    $"[ExcelsiorTable] property '{modelType.Name}.{property.Name}' is not readable.");
            }

            var elementType = ModelValidator.TryResolveElementType(property.PropertyType);
            if (elementType == null || elementType == typeof(char))
            {
                throw new ParchmentRegistrationException(
                    templateName,
                    $"[ExcelsiorTable] property '{modelType.Name}.{property.Name}' must be an IEnumerable<T> (excluding string).");
            }

            entries[property.Name] = new(property.Name, elementType, property.GetValue!);
        }

        return new(entries);
    }
}

sealed record ExcelsiorTableEntry(string PropertyName, Type ElementType, Func<object, object?> Getter);
