[Generator]
public sealed class ParchmentTemplateGenerator :
    IIncrementalGenerator
{
    const string attributeFullName = "Parchment.ParchmentTemplateAttribute";

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // ForAttributeWithMetadataName only re-fires ExtractTarget when the attributed class's
        // own syntax changes. Editing a model class (e.g. adding/removing a property on Invoice)
        // in a separate file will NOT re-validate templates that reference it until the
        // attributed class is touched. The tradeoff: combining with CompilationProvider would
        // make the extract re-run every compilation and defeat the point of the primitive-only
        // pipeline below. Kicking the attributed file forces revalidation in the meantime.
        var targets = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                attributeFullName,
                static (node, _) => node is ClassDeclarationSyntax,
                ExtractTarget)
            .Where(static target => target != null)
            .Select(static (target, _) => target!)
            .WithTrackingName(Stages.Targets)
            .Collect()
            .Select(static (array, _) => new EquatableArray<TargetInfo>(array))
            .WithTrackingName(Stages.TargetsCollected);

        var docs = context.AdditionalTextsProvider
            .Where(static text => text.Path.EndsWith(".docx", StringComparison.OrdinalIgnoreCase))
            .Select(static (text, _) => ReadDocx(text))
            .WithTrackingName(Stages.Docs)
            .Collect()
            .Select(static (array, _) => new EquatableArray<DocxData>(array))
            .WithTrackingName(Stages.DocsCollected);

        var combined = targets
            .Combine(docs)
            .WithTrackingName(Stages.Combined);

        context.RegisterSourceOutput(
            combined,
            static (productionContext, tuple) =>
            {
                var targetInfos = tuple.Left;
                var docData = tuple.Right;
                foreach (var target in targetInfos)
                {
                    Process(productionContext, target, docData);
                }
            });
    }

    static TargetInfo? ExtractTarget(GeneratorAttributeSyntaxContext context, Cancel cancel)
    {
        if (context.TargetSymbol is not INamedTypeSymbol typeSymbol)
        {
            return null;
        }

        var attribute = context.Attributes.FirstOrDefault();
        if (attribute == null ||
            attribute.ConstructorArguments.Length < 2)
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

        var syntaxReference = attribute.ApplicationSyntaxReference;
        var rawLocation = syntaxReference == null
            ? Location.None
            : Location.Create(
                syntaxReference.SyntaxTree,
                syntaxReference.Span);

        var declaringNamespace = typeSymbol.ContainingNamespace.IsGlobalNamespace
            ? null
            : typeSymbol.ContainingNamespace.ToDisplayString();

        var excelsiorTableType = context.SemanticModel.Compilation
            .GetTypeByMetadataName(ShapeBuilder.ExcelsiorTableAttributeFullName);
        var shape = ShapeBuilder.Build(modelType, excelsiorTableType, cancel);

        return new(
            declaringNamespace,
            typeSymbol.Name,
            modelType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
            modelType.Name,
            path,
            EquatableLocation.From(rawLocation),
            shape);
    }

    static DocxData ReadDocx(AdditionalText text)
    {
        try
        {
            var paragraphs = DocxArchiveReader.ReadParagraphTexts(text.Path);
            return new(text.Path, new(paragraphs.ToImmutableArray()), null);
        }
        catch (Exception exception)
        {
            return new(text.Path, EquatableArray<string>.Empty, exception.Message);
        }
    }

    static void Process(SourceProductionContext context, TargetInfo target, EquatableArray<DocxData> docs)
    {
        var location = target.Location.ToLocation();
        var normalized = target.TemplatePath.Replace('\\', '/');

        DocxData? matched = null;
        foreach (var doc in docs)
        {
            if (doc.Path.Replace('\\', '/')
                .EndsWith(normalized, StringComparison.OrdinalIgnoreCase))
            {
                matched = doc;
                break;
            }
        }

        if (matched == null)
        {
            context.ReportDiagnostic(
                Diagnostic.Create(
                    Diagnostics.TemplateFileMissing,
                    location,
                    target.TemplatePath));
            return;
        }

        if (matched.ReadError != null)
        {
            context.ReportDiagnostic(
                Diagnostic.Create(
                    Diagnostics.TemplateReadError,
                    location,
                    target.TemplatePath,
                    matched.ReadError));
            return;
        }

        var tokens = TokenScanner.Scan(matched.Paragraphs);
        ValidateTokens(context, target, tokens, location);

        var source = GenerateRegistration(target);
        var hintPrefix = target.DeclaringNamespace is null
            ? target.DeclaringName
            : $"{target.DeclaringNamespace}.{target.DeclaringName}";
        context.AddSource(
            $"{hintPrefix.Replace('.', '_')}_ParchmentTemplate.g.cs",
            SourceText.From(source, Encoding.UTF8));
    }

    static void ValidateTokens(
        SourceProductionContext context,
        TargetInfo target,
        IReadOnlyList<Token> tokens,
        Location location)
    {
        var scope = new Dictionary<string, string>(StringComparer.Ordinal);
        var loopStack = new Stack<string>();

        foreach (var token in tokens)
        {
            switch (token.Kind)
            {
                case TokenKind.Substitution:
                    ValidateReferences(context, target, location, token.References, scope, token.Source);
                    ValidateExcelsiorToken(context, target, location, token, scope);
                    ValidateFormatToken(context, target, location, token, scope);
                    break;

                case TokenKind.ForOpen:
                    if (token.HasOtherContent)
                    {
                        context.ReportDiagnostic(
                            Diagnostic.Create(
                                Diagnostics.MixedInlineBlockTag,
                                location,
                                target.TemplatePath,
                                token.Source));
                    }

                    if (token.LoopVariable == null || token.References.Count == 0)
                    {
                        break;
                    }

                    var sourceFqn = ShapeResolver.Resolve(target.Shape, token.References[0], scope);
                    if (sourceFqn == null)
                    {
                        context.ReportDiagnostic(
                            Diagnostic.Create(
                                Diagnostics.MissingMember,
                                location,
                                target.TemplatePath,
                                token.Source,
                                string.Join('.', token.References[0]),
                                target.ModelSimpleName));
                        break;
                    }

                    var elementFqn = ShapeResolver.GetElementType(target.Shape, sourceFqn);
                    if (elementFqn == null)
                    {
                        context.ReportDiagnostic(
                            Diagnostic.Create(
                                Diagnostics.LoopSourceNotEnumerable,
                                location,
                                target.TemplatePath,
                                token.Source));
                        break;
                    }

                    scope[token.LoopVariable] = elementFqn;
                    loopStack.Push(token.LoopVariable);
                    break;

                case TokenKind.ForClose:
                    if (token.HasOtherContent)
                    {
                        context.ReportDiagnostic(
                            Diagnostic.Create(
                                Diagnostics.MixedInlineBlockTag,
                                location,
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
                        context.ReportDiagnostic(
                            Diagnostic.Create(
                                Diagnostics.MixedInlineBlockTag,
                                location,
                                target.TemplatePath,
                                token.Source));
                    }

                    ValidateReferences(context, target, location, token.References, scope, token.Source);
                    break;

                case TokenKind.Else:
                case TokenKind.IfClose:
                    if (token.HasOtherContent)
                    {
                        context.ReportDiagnostic(
                            Diagnostic.Create(
                                Diagnostics.MixedInlineBlockTag,
                                location,
                                target.TemplatePath,
                                token.Source));
                    }

                    break;

                case TokenKind.UnknownBlock:
                    context.ReportDiagnostic(
                        Diagnostic.Create(
                            Diagnostics.UnsupportedBlockTag,
                            location,
                            target.TemplatePath,
                            token.Source));
                    break;
            }
        }
    }

    static void ValidateExcelsiorToken(
        SourceProductionContext context,
        TargetInfo target,
        Location location,
        Token token,
        Dictionary<string, string> scope)
    {
        // Only substitution tokens whose first identifier path resolves to an [ExcelsiorTable]
        // property need these extra checks. Everything else is handled by normal reference
        // validation (PARCH001/etc) or flows through the standard runtime substitution path.
        if (token.References.Count == 0)
        {
            return;
        }

        if (!ShapeResolver.IsExcelsiorTableMember(target.Shape, token.References[0], scope))
        {
            return;
        }

        if (token.HasOtherContent)
        {
            context.ReportDiagnostic(
                Diagnostic.Create(
                    Diagnostics.ExcelsiorTokenNotAlone,
                    location,
                    target.TemplatePath,
                    token.Source));
        }

        if (!token.IsPlainIdentifier)
        {
            context.ReportDiagnostic(
                Diagnostic.Create(
                    Diagnostics.ExcelsiorTokenNotPlainIdentifier,
                    location,
                    target.TemplatePath,
                    token.Source));
        }
    }

    static void ValidateFormatToken(
        SourceProductionContext context,
        TargetInfo target,
        Location location,
        Token token,
        Dictionary<string, string> scope)
    {
        if (token.References.Count == 0)
        {
            return;
        }

        var member = ShapeResolver.ResolveMember(target.Shape, token.References[0], scope);
        if (member is null || member is { IsHtml: false, IsMarkdown: false })
        {
            return;
        }

        if (!token.IsPlainIdentifier)
        {
            context.ReportDiagnostic(
                Diagnostic.Create(
                    Diagnostics.FormatTokenNotPlainIdentifier,
                    location,
                    target.TemplatePath,
                    token.Source));
        }
    }

    static void ValidateReferences(
        SourceProductionContext context,
        TargetInfo target,
        Location location,
        IReadOnlyList<string[]> references,
        IReadOnlyDictionary<string, string> scope,
        string tokenSource)
    {
        foreach (var reference in references)
        {
            var resolved = ShapeResolver.Resolve(target.Shape, reference, scope);
            if (resolved != null)
            {
                continue;
            }

            context.ReportDiagnostic(
                Diagnostic.Create(
                    Diagnostics.MissingMember,
                    location,
                    target.TemplatePath,
                    tokenSource,
                    string.Join('.', reference),
                    target.ModelSimpleName));
        }
    }

    static string GenerateRegistration(TargetInfo target)
    {
        var templatePath = target.TemplatePath.Replace("\\", @"\\");
        var builder = new StringBuilder(
            """
            // <auto-generated />
            #nullable enable

            """);

        if (target.DeclaringNamespace != null)
        {
            builder.AppendLine($"namespace {target.DeclaringNamespace};");
        }

        builder.AppendLine(
            $$"""
              partial class {{target.DeclaringName}}
              {
                  public static string TemplatePath => "{{templatePath}}";
                  public static string TemplateName => "{{target.DeclaringName}}";

                  public static void RegisterWith(global::Parchment.TemplateStore store, string? basePath = null)
                  {
                      var path = basePath is null ? TemplatePath : global::System.IO.Path.Combine(basePath, TemplatePath);
                      store.RegisterDocxTemplate<{{target.ModelFullyQualifiedName}}>(TemplateName, path);
                  }
              }
              """);

        return builder.ToString();
    }

    public static class Stages
    {
        public const string Targets = "Parchment_Targets";
        public const string TargetsCollected = "Parchment_TargetsCollected";
        public const string Docs = "Parchment_Docs";
        public const string DocsCollected = "Parchment_DocsCollected";
        public const string Combined = "Parchment_Combined";
    }
}
