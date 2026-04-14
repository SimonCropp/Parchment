namespace Parchment.SourceGenerator;

internal sealed record TemplateTarget(
    INamedTypeSymbol Declaring,
    INamedTypeSymbol ModelType,
    string TemplatePath,
    Location Location);