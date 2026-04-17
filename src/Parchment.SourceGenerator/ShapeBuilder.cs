/// <summary>
/// Builds a primitive-only <see cref="ModelShape"/> from a live <see cref="INamedTypeSymbol"/>
/// at extract time. Consuming the shape downstream (instead of the symbol) is what makes the
/// incremental pipeline actually cacheable.
/// Known limitation: the shape is only rebuilt when the attributed class's own syntax changes,
/// because <c>ForAttributeWithMetadataName</c> in <see cref="ParchmentTemplateGenerator"/> keys
/// re-extraction on that class's syntax. Edits to a model type declared in a separate file
/// will not re-trigger validation until something in the attributed class is touched.
/// </summary>
static class ShapeBuilder
{
    public const string ExcelsiorTableAttributeFullName = "Parchment.ExcelsiorTableAttribute";

    static readonly SymbolDisplayFormat format = SymbolDisplayFormat.FullyQualifiedFormat;

    public static ModelShape Build(INamedTypeSymbol root, INamedTypeSymbol? excelsiorTableType, Cancel cancel)
    {
        var entries = ImmutableArray.CreateBuilder<TypeEntry>();
        var visited = new HashSet<string>(StringComparer.Ordinal);
        var queue = new Queue<ITypeSymbol>();

        Enqueue(root, visited, queue);
        while (queue.Count > 0)
        {
            cancel.ThrowIfCancellationRequested();
            var type = queue.Dequeue();
            entries.Add(BuildEntry(type, excelsiorTableType, visited, queue));
        }

        return new(Fqn(root), new(entries.ToImmutable()));
    }

    static TypeEntry BuildEntry(ITypeSymbol type, INamedTypeSymbol? excelsiorTableType, HashSet<string> visited, Queue<ITypeSymbol> queue)
    {
        string? elementFqn = null;
        if (type.SpecialType != SpecialType.System_String)
        {
            var element = ModelSymbolResolver.TryGetElementType(type);
            if (element != null)
            {
                elementFqn = Fqn(element);
                Enqueue(element, visited, queue);
            }
        }

        var members = ImmutableArray.CreateBuilder<MemberEntry>();
        if (!IsSystemType(type))
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var current = type;
            while (current != null)
            {
                foreach (var member in current.GetMembers())
                {
                    if (!TryGetMemberType(member, out var memberType, out var memberName))
                    {
                        continue;
                    }

                    if (!seen.Add(memberName))
                    {
                        continue;
                    }

                    var isExcelsior = HasExcelsiorTableAttribute(member, excelsiorTableType);
                    members.Add(new(memberName, Fqn(memberType), isExcelsior));
                    Enqueue(memberType, visited, queue);
                }

                current = current.BaseType;
            }
        }

        return new(Fqn(type), elementFqn, new(members.ToImmutable()));
    }

    static bool HasExcelsiorTableAttribute(ISymbol member, INamedTypeSymbol? excelsiorTableType)
    {
        if (excelsiorTableType is null)
        {
            return false;
        }

        foreach (var attribute in member.GetAttributes())
        {
            if (SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, excelsiorTableType))
            {
                return true;
            }
        }

        return false;
    }

    static bool TryGetMemberType(ISymbol member, out ITypeSymbol type, out string name)
    {
        if (member is IPropertySymbol { DeclaredAccessibility: Accessibility.Public, IsStatic: false } property)
        {
            type = property.Type;
            name = property.Name;
            return true;
        }

        if (member is IFieldSymbol { DeclaredAccessibility: Accessibility.Public, IsStatic: false } field)
        {
            type = field.Type;
            name = field.Name;
            return true;
        }

        type = null!;
        name = null!;
        return false;
    }

    static void Enqueue(ITypeSymbol? type, HashSet<string> visited, Queue<ITypeSymbol> queue)
    {
        if (type == null)
        {
            return;
        }

        var key = Fqn(type);
        if (visited.Add(key))
        {
            queue.Enqueue(type);
        }
    }

    static bool IsSystemType(ITypeSymbol type)
    {
        var ns = type.ContainingNamespace;
        if (ns is null or { IsGlobalNamespace: true })
        {
            return false;
        }

        while (ns.ContainingNamespace is { IsGlobalNamespace: false })
        {
            ns = ns.ContainingNamespace;
        }

        return ns.Name == "System";
    }

    static string Fqn(ITypeSymbol type) =>
        type.ToDisplayString(format);
}
