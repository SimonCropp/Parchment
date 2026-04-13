namespace Parchment.SourceGenerator;

[Generator]
public sealed class ParchmentTemplateGenerator :
    IIncrementalGenerator
{
    const string AttributeFullName = "Parchment.ParchmentTemplateAttribute";

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var additionalFiles = context.AdditionalTextsProvider
            .Where(static x =>
                x.Path.EndsWith(".docx", StringComparison.OrdinalIgnoreCase))
            .Collect();

        var attributed = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                AttributeFullName,
                static (node, _) => node is ClassDeclarationSyntax,
                static (ctx, _) => ExtractTarget(ctx))
            .Where(static x => x != null)
            .Collect();

        var combined = attributed.Combine(additionalFiles);

        context.RegisterSourceOutput(combined, (productionContext, tuple) =>
        {
            var (targets, files) = tuple;
            foreach (var target in targets)
            {
                if (target == null)
                {
                    continue;
                }

                Process(productionContext, target, files);
            }
        });
    }

    static TemplateTarget? ExtractTarget(GeneratorAttributeSyntaxContext context)
    {
        if (context.TargetSymbol is not INamedTypeSymbol typeSymbol)
        {
            return null;
        }

        var attribute = context.Attributes.FirstOrDefault();
        if (attribute == null || attribute.ConstructorArguments.Length < 2)
        {
            return null;
        }

        if (attribute.ConstructorArguments[0].Value is not string path)
        {
            return null;
        }

        if (attribute.ConstructorArguments[1].Value is not INamedTypeSymbol modelType)
        {
            return null;
        }

        var location = attribute.ApplicationSyntaxReference != null
            ? Location.Create(
                attribute.ApplicationSyntaxReference.SyntaxTree,
                attribute.ApplicationSyntaxReference.Span)
            : Location.None;

        return new(typeSymbol, modelType, path, location);
    }

    static void Process(SourceProductionContext context, TemplateTarget target, ImmutableArray<AdditionalText> files)
    {
        var normalized = target.TemplatePath.Replace('\\', '/');
        var file = files.FirstOrDefault(_ =>
            _.Path.Replace('\\', '/')
                .EndsWith(normalized, StringComparison.OrdinalIgnoreCase));
        if (file == null)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                Diagnostics.TemplateFileMissing,
                target.Location,
                target.TemplatePath));
            return;
        }

        List<string> paragraphs;
        try
        {
            paragraphs = DocxArchiveReader.ReadParagraphTexts(file.Path);
        }
        catch (Exception exception)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                Diagnostics.TemplateReadError,
                target.Location,
                target.TemplatePath,
                exception.Message));
            return;
        }

        var tokens = TokenScanner.Scan(paragraphs);
        ValidateTokens(context, target, tokens);

        var source = GenerateRegistration(target);
        context.AddSource($"{target.Declaring.ToDisplayString().Replace('.', '_')}_ParchmentTemplate.g.cs", SourceText.From(source, Encoding.UTF8));
    }

    static void ValidateTokens(SourceProductionContext context, TemplateTarget target, IReadOnlyList<Token> tokens)
    {
        var scope = new Dictionary<string, ITypeSymbol>(StringComparer.Ordinal);
        var loopStack = new Stack<string>();

        foreach (var token in tokens)
        {
            switch (token.Kind)
            {
                case TokenKind.Substitution:
                    if (token.HasOtherContent)
                    {
                        // ok — substitution tokens can share a paragraph with static text
                    }

                    ValidateReferences(context, target, token.References, scope, token.Source);
                    break;

                case TokenKind.ForOpen:
                    if (token.HasOtherContent)
                    {
                        context.ReportDiagnostic(Diagnostic.Create(
                            Diagnostics.MixedInlineBlockTag,
                            target.Location,
                            target.TemplatePath,
                            token.Source));
                    }

                    if (token.LoopVariable == null || token.References.Count == 0)
                    {
                        break;
                    }

                    var sourceType = ResolveType(target, token.References[0], scope);
                    if (sourceType == null)
                    {
                        context.ReportDiagnostic(Diagnostic.Create(
                            Diagnostics.MissingMember,
                            target.Location,
                            target.TemplatePath,
                            token.Source,
                            string.Join(".", token.References[0]),
                            target.ModelType.Name));
                        break;
                    }

                    var elementType = ModelSymbolResolver.TryGetElementType(sourceType);
                    if (elementType == null)
                    {
                        context.ReportDiagnostic(Diagnostic.Create(
                            Diagnostics.LoopSourceNotEnumerable,
                            target.Location,
                            target.TemplatePath,
                            token.Source));
                        break;
                    }

                    scope[token.LoopVariable] = elementType;
                    loopStack.Push(token.LoopVariable);
                    break;

                case TokenKind.ForClose:
                    if (token.HasOtherContent)
                    {
                        context.ReportDiagnostic(Diagnostic.Create(
                            Diagnostics.MixedInlineBlockTag,
                            target.Location,
                            target.TemplatePath,
                            token.Source));
                    }

                    if (loopStack.Count > 0)
                    {
                        scope.Remove(loopStack.Pop());
                    }

                    break;

                case TokenKind.IfOpen:
                case TokenKind.ElsIf:
                    if (token.HasOtherContent)
                    {
                        context.ReportDiagnostic(Diagnostic.Create(
                            Diagnostics.MixedInlineBlockTag,
                            target.Location,
                            target.TemplatePath,
                            token.Source));
                    }

                    ValidateReferences(context, target, token.References, scope, token.Source);
                    break;

                case TokenKind.Else:
                case TokenKind.IfClose:
                    if (token.HasOtherContent)
                    {
                        context.ReportDiagnostic(Diagnostic.Create(
                            Diagnostics.MixedInlineBlockTag,
                            target.Location,
                            target.TemplatePath,
                            token.Source));
                    }

                    break;

                case TokenKind.UnknownBlock:
                    context.ReportDiagnostic(Diagnostic.Create(
                        Diagnostics.UnsupportedBlockTag,
                        target.Location,
                        target.TemplatePath,
                        token.Source));
                    break;
            }
        }
    }

    static void ValidateReferences(
        SourceProductionContext context,
        TemplateTarget target,
        IReadOnlyList<string[]> references,
        IReadOnlyDictionary<string, ITypeSymbol> scope,
        string tokenSource)
    {
        foreach (var reference in references)
        {
            var resolved = ResolveType(target, reference, scope);
            if (resolved == null)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    Diagnostics.MissingMember,
                    target.Location,
                    target.TemplatePath,
                    tokenSource,
                    string.Join(".", reference),
                    target.ModelType.Name));
            }
        }
    }

    static ITypeSymbol? ResolveType(TemplateTarget target, string[] segments, IReadOnlyDictionary<string, ITypeSymbol> scope)
    {
        if (segments.Length == 0)
        {
            return null;
        }

        if (scope.TryGetValue(segments[0], out var scoped))
        {
            if (segments.Length == 1)
            {
                return scoped;
            }

            return ModelSymbolResolver.ResolvePath(scoped, segments.Skip(1).ToArray());
        }

        return ModelSymbolResolver.ResolvePath(target.ModelType, segments);
    }

    static string GenerateRegistration(TemplateTarget target)
    {
        var declaring = target.Declaring;
        var namespaceName = declaring.ContainingNamespace.IsGlobalNamespace ? null : declaring.ContainingNamespace.ToDisplayString();
        var className = declaring.Name;
        var modelFullName = target.ModelType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

        var builder = new StringBuilder();
        builder.AppendLine("// <auto-generated />");
        builder.AppendLine("#nullable enable");
        if (namespaceName != null)
        {
            builder.Append("namespace ").Append(namespaceName).AppendLine(";");
            builder.AppendLine();
        }

        builder.Append("partial class ").AppendLine(className);
        builder.AppendLine("{");
        builder.Append("    public static string TemplatePath => \"").Append(target.TemplatePath.Replace("\\", "\\\\")).AppendLine("\";");
        builder.Append("    public static string TemplateName => \"").Append(className).AppendLine("\";");
        builder.AppendLine();
        builder.AppendLine("    public static void RegisterWith(global::Parchment.TemplateStore store, string? basePath = null)");
        builder.AppendLine("    {");
        builder.AppendLine("        var path = basePath is null ? TemplatePath : global::System.IO.Path.Combine(basePath, TemplatePath);");
        builder.AppendLine("        var bytes = global::System.IO.File.ReadAllBytes(path);");
        builder.Append("        store.RegisterDocxTemplate<").Append(modelFullName).AppendLine(">(TemplateName, bytes);");
        builder.AppendLine("    }");
        builder.AppendLine("}");

        return builder.ToString();
    }
}

internal sealed record TemplateTarget(
    INamedTypeSymbol Declaring,
    INamedTypeSymbol ModelType,
    string TemplatePath,
    Location Location);
