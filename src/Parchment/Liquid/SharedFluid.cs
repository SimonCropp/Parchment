namespace Parchment.Liquid;

/// <summary>
/// Static singletons for Fluid. Fluid's parser, options, and filters are thread-safe and expensive
/// to construct; one instance per process is the documented recommendation.
/// </summary>
static class SharedFluid
{
    public static FluidParser Parser { get; } = new();

    public static TemplateOptions Options { get; } = BuildOptions();

    static readonly ConcurrentDictionary<Type, bool> registeredTypes = new();

    static readonly MethodInfo registerGenericMethod = typeof(MemberAccessStrategyExtensions)
        .GetMethods(BindingFlags.Public | BindingFlags.Static)
        .First(_ => _ is { Name: "Register", IsGenericMethodDefinition: true }
                    && _.GetGenericArguments().Length == 1
                    && _.GetParameters().Length == 1);

    static TemplateOptions BuildOptions()
    {
        var options = new TemplateOptions
        {
            MaxSteps = 10_000,
            MaxRecursion = 100
        };
        Filters.Register(options.Filters);
        return options;
    }

    public static void RegisterModel(Type modelType) =>
        RegisterTypeGraph(modelType);

    static void RegisterTypeGraph(Type? type)
    {
        if (type == null || !ShouldRegister(type))
        {
            return;
        }

        if (!registeredTypes.TryAdd(type, true))
        {
            return;
        }

        var generic = registerGenericMethod.MakeGenericMethod(type);
        generic.Invoke(null, [Options.MemberAccessStrategy]);

        foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (property.GetIndexParameters().Length > 0)
            {
                continue;
            }

            RegisterTypeGraph(Unwrap(property.PropertyType));
        }
    }

    static Type Unwrap(Type type)
    {
        if (type.IsArray)
        {
            return type.GetElementType() ?? type;
        }

        if (type.IsGenericType)
        {
            var definition = type.GetGenericTypeDefinition();
            if (definition == typeof(Nullable<>))
            {
                return type.GetGenericArguments()[0];
            }

            if (typeof(IEnumerable).IsAssignableFrom(type))
            {
                return type.GetGenericArguments()[0];
            }
        }

        foreach (var i in type.GetInterfaces())
        {
            if (i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEnumerable<>))
            {
                return i.GetGenericArguments()[0];
            }
        }

        return type;
    }

    static bool ShouldRegister(Type type)
    {
        if (type.IsPrimitive ||
            type == typeof(string) ||
            type == typeof(decimal) ||
            type == typeof(DateTime) ||
            type == typeof(DateTimeOffset) ||
            type == typeof(TimeSpan) ||
            type == typeof(Date) ||
            type == typeof(Time) ||
            type == typeof(Guid))
        {
            return false;
        }

        if (type.IsEnum)
        {
            return false;
        }

        if (type.Namespace?.StartsWith("System", StringComparison.Ordinal) == true)
        {
            return false;
        }

        return type.IsClass || type is { IsValueType: true, IsPrimitive: false, IsEnum: false };
    }
}
