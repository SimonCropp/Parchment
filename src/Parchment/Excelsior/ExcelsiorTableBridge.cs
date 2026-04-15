using Excelsior;

/// <summary>
/// Reflection adapter that constructs the closed-generic <c>Excelsior.WordTableBuilder&lt;T&gt;</c>
/// for a model element type known only at runtime, then renders the table against the host
/// <see cref="MainDocumentPart"/>.
/// </summary>
static class ExcelsiorTableBridge
{
    static readonly ConcurrentDictionary<Type, BuilderInvoker> invokerCache = new();

    public static Table BuildTable(Type elementType, object data, MainDocumentPart mainPart)
    {
        var invoker = invokerCache.GetOrAdd(elementType, CreateInvoker);
        return invoker(data, mainPart);
    }

    static BuilderInvoker CreateInvoker(Type elementType)
    {
        var builderType = typeof(WordTableBuilder<>).MakeGenericType(elementType);
        var enumerableType = typeof(IEnumerable<>).MakeGenericType(elementType);
        var ctor = builderType.GetConstructor([enumerableType])
            ?? throw new InvalidOperationException(
                $"Excelsior.WordTableBuilder<{elementType.Name}> has no constructor accepting IEnumerable<{elementType.Name}>.");
        var build = builderType.GetMethod("Build", [typeof(MainDocumentPart)])
            ?? throw new InvalidOperationException(
                $"Excelsior.WordTableBuilder<{elementType.Name}>.Build(MainDocumentPart) is missing.");

        return (data, mainPart) =>
        {
            var builder = ctor.Invoke([data]);
            return (Table) build.Invoke(builder, [mainPart])!;
        };
    }

    delegate Table BuilderInvoker(object data, MainDocumentPart mainPart);
}
