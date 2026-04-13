namespace Parchment.SourceGenerator;

internal static class Diagnostics
{
    public static readonly DiagnosticDescriptor MissingMember = new(
        id: "PARCH001",
        title: "Template references an unknown model member",
        messageFormat: "Template '{0}' token '{1}' references '{2}' which is not a member of '{3}'",
        category: "Parchment",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor LoopSourceNotEnumerable = new(
        id: "PARCH002",
        title: "Loop source is not enumerable",
        messageFormat: "Template '{0}' loop '{1}' source does not resolve to a type implementing IEnumerable<T>",
        category: "Parchment",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor UnsupportedBlockTag = new(
        id: "PARCH003",
        title: "Unsupported block tag",
        messageFormat: "Template '{0}' uses unsupported block tag '{1}' (supported: for, endfor, if, elsif, else, endif)",
        category: "Parchment",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor TemplateFileMissing = new(
        id: "PARCH004",
        title: "Template file not found in AdditionalFiles",
        messageFormat: "Template path '{0}' was not found in AdditionalFiles — add <AdditionalFiles Include=\"...\"/> to the csproj",
        category: "Parchment",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor MixedInlineBlockTag = new(
        id: "PARCH005",
        title: "Block tag must sit in its own paragraph",
        messageFormat: "Template '{0}' block tag '{1}' shares a paragraph with other content; block tags must be on their own lines",
        category: "Parchment",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor TemplateReadError = new(
        id: "PARCH006",
        title: "Failed to read template",
        messageFormat: "Template '{0}' could not be read: {1}",
        category: "Parchment",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);
}
