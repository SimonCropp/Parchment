namespace Parchment;

public sealed class TemplateStore(ILogger<TemplateStore>? logger = null)
{
    ConcurrentDictionary<string, RegisteredTemplate> templates = new(StringComparer.Ordinal);
    ILogger logger = (ILogger?)logger ?? NullLogger.Instance;

    public void RegisterDocxTemplate<TModel>(string name, string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        using var file = File.OpenRead(path);
        RegisterDocxTemplate<TModel>(name, file);
    }

    public void RegisterDocxTemplate<TModel>(string name, Stream template)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        SharedFluid.RegisterModel(typeof(TModel));

        var excelsiorMap = ExcelsiorTableMap.Build(typeof(TModel), name);
        var formatMap = FormatMap.Build(typeof(TModel), name);

        using var stream = DocxCloner.ToWritableStream(template);
        IReadOnlyList<PartScopeTree> parts;
        using (var doc = WordprocessingDocument.Open(stream, true))
        {
            foreach (var (uri, root) in DocxCloner.EnumerateParts(doc))
            {
                var classifications = TokenScanner.Scan(root, name, uri);
                if (classifications.Count == 0)
                {
                    continue;
                }

                var tree = ScopeTreeBuilder.Build(classifications, name, uri);
                var validator = new ReferenceValidator(typeof(TModel), name, uri);
                validator.ValidateTree(tree);
                ExcelsiorTokenValidator.Validate(classifications, excelsiorMap, name, uri);
                FormatTokenValidator.Validate(classifications, formatMap, name, uri);
            }

            doc.Save();

            parts = ExtractParts(doc, name);
        }

        var canonicalBytes = stream.ToArray();
        var registered = new RegisteredDocxTemplate(name, typeof(TModel), canonicalBytes, parts, excelsiorMap, formatMap);
        templates[name] = registered;
        logger.LogInformation("Registered docx template {Name} for {ModelType}", name, typeof(TModel).Name);
    }

    public void RegisterMarkdownTemplate<TModel>(string name, string markdown, Stream? styleSource = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        SharedFluid.RegisterModel(typeof(TModel));

        if (!SharedFluid.Parser.TryParse(markdown, out var template, out var error))
        {
            throw new ParchmentRegistrationException(
                name,
                $"Failed to parse markdown as a liquid template: {error}");
        }

        var refs = IdentifierVisitor.Collect(template);
        foreach (var reference in refs)
        {
            if (reference.Segments.Count == 0)
            {
                continue;
            }

            // Skip loop variables — they get bound at render time via liquid scope and can't be
            // pre-validated without knowing the full scope tree.
            if (IsLikelyLoopVariable(reference.Root, markdown))
            {
                continue;
            }

            ModelValidator.Validate(typeof(TModel), reference, null, name, null, null);
        }

        byte[] bytes;
        if (styleSource is MemoryStream existingMs)
        {
            bytes = existingMs.ToArray();
        }
        else if (styleSource != null)
        {
            using var ms = new MemoryStream();
            styleSource.CopyTo(ms);
            bytes = ms.ToArray();
        }
        else
        {
            bytes = BlankDocxTemplate;
        }

        var registered = new RegisteredMarkdownTemplate(name, typeof(TModel), bytes, template);
        templates[name] = registered;
        logger.LogInformation("Registered markdown template {Name} for {ModelType}", name, typeof(TModel).Name);
    }

    static byte[] BlankDocxTemplate { get; } = BuildBlankDocx();

    static byte[] BuildBlankDocx()
    {
        using var stream = new MemoryStream();
        using (var doc = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document))
        {
            var mainPart = doc.AddMainDocumentPart();
            mainPart.Document = new(new Body(new Paragraph()));

            var stylesPart = mainPart.AddNewPart<StyleDefinitionsPart>();
            var styles = new Styles();
            styles.Append(new Style { Type = StyleValues.Paragraph, StyleId = "Normal", Default = true }.AppendChild(new StyleName { Val = "Normal" }).Parent!);
            for (var i = 1; i <= 6; i++)
            {
                styles.Append(new Style { Type = StyleValues.Paragraph, StyleId = $"Heading{i}" }.AppendChild(new StyleName { Val = $"Heading{i}" }).Parent!);
            }

            styles.Append(new Style { Type = StyleValues.Paragraph, StyleId = "ListParagraph" }.AppendChild(new StyleName { Val = "List Paragraph" }).Parent!);
            styles.Append(new Style { Type = StyleValues.Paragraph, StyleId = "Quote" }.AppendChild(new StyleName { Val = "Quote" }).Parent!);
            stylesPart.Styles = styles;
        }

        return stream.ToArray();
    }

    static bool IsLikelyLoopVariable(string identifier, string markdown)
    {
        var pattern = $"{{%\\s*for\\s+{Regex.Escape(identifier)}\\s+in\\s+";
        return Regex.IsMatch(markdown, pattern);
    }

    public Task Render(string name, object model, Stream output, Cancel cancel = default)
    {
        if (!templates.TryGetValue(name, out var template))
        {
            throw new ParchmentRenderException(name, "Template is not registered");
        }

        if (!template.ModelType.IsInstanceOfType(model))
        {
            throw new ParchmentRenderException(
                name,
                $"Model type mismatch: registered as {template.ModelType.Name} but received {model.GetType().Name}");
        }

        return template.Render(model, output, cancel);
    }

    public async Task RenderToFile(string name, object model, string path, Cancel cancel = default)
    {
        await using var file = File.Create(path);
        await Render(name, model, file, cancel).ConfigureAwait(false);
    }

    public static void AddFilter(string name, FilterDelegate filter) =>
        SharedFluid.Options.Filters.AddFilter(name, filter);

    static IReadOnlyList<PartScopeTree> ExtractParts(WordprocessingDocument doc, string name)
    {
        var parts = new List<PartScopeTree>();
        foreach (var (uri, root) in DocxCloner.EnumerateParts(doc))
        {
            var classifications = new List<ParagraphClassification>();
            foreach (var paragraph in root.Descendants<Paragraph>())
            {
                var anchorName = paragraph
                    .Elements<BookmarkStart>()
                    .FirstOrDefault(x => x.Name?.Value?.StartsWith(Anchors.Prefix, StringComparison.Ordinal) == true)
                    ?.Name?.Value;
                if (anchorName == null)
                {
                    continue;
                }

                var reclassified = RestoreClassification(paragraph, anchorName);
                classifications.Add(reclassified);
            }

            if (classifications.Count == 0)
            {
                continue;
            }

            var tree = ScopeTreeBuilder.Build(classifications, name, uri);
            parts.Add(new(uri, tree));
        }

        return parts;
    }

    static ParagraphClassification RestoreClassification(
        Paragraph paragraph,
        string anchorName)
    {
        var text = ParagraphText.Build(paragraph);
        var innerText = text.InnerText;
        var sites = TokenScan.Scan(innerText);
        if (sites.Count == 0)
        {
            return new(paragraph, anchorName, ParagraphKind.Static, [], null);
        }

        var substitutions = new List<DocxTokenSite>();
        BlockMarker? block = null;
        foreach (var site in sites)
        {
            var source = innerText.Substring(site.Offset, site.Length);
            if (site.Kind == TokenSiteKind.Substitution)
            {
                if (SharedFluid.Parser.TryParse(source, out var template, out _))
                {
                    var refs = IdentifierVisitor.Collect(template);
                    substitutions.Add(new(site.Offset, site.Length, source, template, refs));
                }
            }
            else
            {
                block = ParseBlock(source);
            }
        }

        if (block != null)
        {
            return new(paragraph, anchorName, ParagraphKind.Block, [], block);
        }

        return new(paragraph, anchorName, ParagraphKind.Substitution, substitutions, null);
    }

    static BlockMarker? ParseBlock(string source)
    {
        var tagMatch = TokenRegex.BlockTag.Match(source);
        if (!tagMatch.Success)
        {
            return null;
        }

        var tag = tagMatch.Groups["tag"].Value;
        var expression = tagMatch.Groups["expr"].Success ? tagMatch.Groups["expr"].Value.Trim() : null;

        return tag switch
        {
            "for" => RebuildFor(source, expression),
            "endfor" => new(BlockTagKind.EndFor, source, null, null, null, null, []),
            "if" => RebuildIf(source, expression),
            "elsif" or "elseif" => RebuildElsif(source, expression),
            "else" => new(BlockTagKind.Else, source, null, null, null, null, []),
            "endif" => new(BlockTagKind.EndIf, source, null, null, null, null, []),
            _ => null
        };
    }

    static BlockMarker? RebuildFor(string source, string? expression)
    {
        if (expression == null)
        {
            return null;
        }

        var liquid = $"{{% for {expression} %}}{{% endfor %}}";
        if (!SharedFluid.Parser.TryParse(liquid, out var template, out _))
        {
            return null;
        }

        var forStatement = ((Fluid.Parser.FluidTemplate)template).Statements
            .OfType<ForStatement>()
            .FirstOrDefault();
        if (forStatement == null)
        {
            return null;
        }

        var refs = IdentifierVisitor.Collect(forStatement.Source);
        return new(BlockTagKind.For, source, expression, null, forStatement.Identifier, forStatement.Source, refs);
    }

    static BlockMarker? RebuildIf(string source, string? expression) =>
        RebuildConditional(BlockTagKind.If, source, expression);

    static BlockMarker? RebuildElsif(string source, string? expression) =>
        RebuildConditional(BlockTagKind.ElsIf, source, expression);

    static BlockMarker? RebuildConditional(BlockTagKind kind, string source, string? expression)
    {
        if (expression == null)
        {
            return null;
        }

        var liquid = $"{{% if {expression} %}}{{% endif %}}";
        if (!SharedFluid.Parser.TryParse(liquid, out var template, out _))
        {
            return null;
        }

        var ifStatement = ((Fluid.Parser.FluidTemplate)template).Statements
            .OfType<IfStatement>()
            .FirstOrDefault();
        if (ifStatement == null)
        {
            return null;
        }

        var refs = IdentifierVisitor.Collect(ifStatement.Condition);
        return new(kind, source, expression, ifStatement.Condition, null, null, refs);
    }
}

class ReferenceValidator(Type modelType, string templateName, string partUri)
{
    public void ValidateTree(IReadOnlyList<RangeNode> nodes)
    {
        var scope = new Dictionary<string, Type>(StringComparer.Ordinal);
        WalkNodes(nodes, scope);
    }

    void WalkNodes(IReadOnlyList<RangeNode> nodes, Dictionary<string, Type> scope)
    {
        foreach (var node in nodes)
        {
            WalkNode(node, scope);
        }
    }

    void WalkNode(RangeNode node, Dictionary<string, Type> scope)
    {
        switch (node)
        {
            case SubstitutionNode s:
                foreach (var token in s.Tokens)
                {
                    foreach (var reference in token.References)
                    {
                        ModelValidator.Validate(modelType, reference, scope, templateName, partUri, token.Source);
                    }
                }

                break;
            case LoopNode loop:
                var sourceRefs = IdentifierVisitor.Collect(loop.LoopSource);
                Type? elementType = null;
                if (sourceRefs.Count > 0)
                {
                    var iterableType = ResolvePathType(sourceRefs[0], scope);
                    if (iterableType != null)
                    {
                        elementType = ModelValidator.TryResolveElementType(iterableType);
                    }
                }

                if (elementType == null)
                {
                    var firstRef = sourceRefs.Count > 0 ? sourceRefs[0].ToString() : null;
                    throw new ParchmentRegistrationException(
                        templateName,
                        $"Loop source '{firstRef}' does not resolve to an enumerable type.",
                        partUri,
                        loop.LoopVariable);
                }

                var loopScope = new Dictionary<string, Type>(scope, StringComparer.Ordinal)
                {
                    [loop.LoopVariable] = elementType
                };
                WalkNodes(loop.Body, loopScope);
                break;
            case IfNode ifNode:
                foreach (var branch in ifNode.Branches)
                {
                    var branchRefs = IdentifierVisitor.Collect(branch.Condition);
                    foreach (var reference in branchRefs)
                    {
                        ModelValidator.Validate(modelType, reference, scope, templateName, partUri, "if");
                    }

                    WalkNodes(branch.Body, scope);
                }

                WalkNodes(ifNode.ElseBody, scope);
                break;
            case StaticNode:
                break;
        }
    }

    Type? ResolvePathType(IdentifierPath path, Dictionary<string, Type> scope)
    {
        var rootType = scope.TryGetValue(path.Root, out var scoped)
            ? scoped
            : ModelValidator.ResolveMember(modelType, path.Root);
        if (rootType == null)
        {
            return null;
        }

        var current = rootType;
        for (var i = 1; i < path.Segments.Count; i++)
        {
            var next = ModelValidator.ResolveMember(current, path.Segments[i]);
            if (next == null)
            {
                return null;
            }

            current = next;
        }

        return current;
    }
}
