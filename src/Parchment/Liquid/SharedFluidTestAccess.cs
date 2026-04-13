namespace Parchment.Liquid;

/// <summary>
/// Test-only access to the shared Fluid singletons. Not part of the public API surface in v1 but
/// necessary for writing tests without replicating the SharedFluid bootstrap.
/// </summary>
public static class SharedFluidTestAccess
{
    public static FluidParser Parser => SharedFluid.Parser;
    public static TemplateOptions Options => SharedFluid.Options;
    public static void Register(Type type) => SharedFluid.RegisterModel(type);
}
