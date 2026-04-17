/// <summary>
/// Walks a cached scope tree against a cloned docx, evaluating substitution tokens and block tags
/// to produce the final rendered document.
/// </summary>
class ScopeTreeRunner(
    string templateName,
    string partUri,
    Dictionary<string, Paragraph> anchorMap,
    TemplateContext context,
    MainDocumentPart mainPart,
    object rootModel,
    ExcelsiorTableMap excelsiorTables)
{
    List<StructuralReplacement> structuralReplacements = [];

    public Task RunAsync(IReadOnlyList<RangeNode> nodes) =>
        ProcessAsync(nodes);

    public void ApplyStructural()
    {
        foreach (var (host, replacement) in structuralReplacements)
        {
            var parent = host.Parent;
            if (parent == null)
            {
                continue;
            }

            OpenXmlElement cursor = host;
            foreach (var produced in replacement)
            {
                cursor = parent.InsertAfter(produced, cursor);
            }

            host.Remove();
        }

        structuralReplacements.Clear();
    }

    async Task ProcessAsync(IReadOnlyList<RangeNode> nodes)
    {
        foreach (var node in nodes)
        {
            await ProcessAsync(node);
        }
    }

    Task ProcessAsync(RangeNode node) =>
        node switch
        {
            SubstitutionNode substitution => ProcessSubstitutionAsync(substitution),
            LoopNode loop => ProcessLoopAsync(loop),
            IfNode ifNode => ProcessIfAsync(ifNode),
            _ => Task.CompletedTask
        };

    async Task ProcessSubstitutionAsync(SubstitutionNode node)
    {
        if (!anchorMap.TryGetValue(node.AnchorName, out var host))
        {
            return;
        }

        var sortedByOffset = node.Tokens.OrderByDescending(_ => _.Offset).ToList();
        var structuralTokens = new List<(DocxTokenSite site, object value)>();

        foreach (var token in sortedByOffset)
        {
            var evaluated = await EvaluateTokenAsync(token, host);
            if (evaluated is TokenValue.MarkdownToken or TokenValue.OpenXmlToken)
            {
                structuralTokens.Add((token, evaluated));
                continue;
            }

            if (evaluated is TokenValue.MutateToken mutate)
            {
                // Clear the token text, then hand the paragraph to the caller for in-place mutation.
                ParagraphText.Build(host).Replace(token.Offset, token.Length, string.Empty);
                var ctx = new OpenXmlContextImpl(
                    mainPart,
                    new(mainPart),
                    StyleSet.Read(mainPart));
                mutate.Apply(host, ctx);
                continue;
            }

            var replacement = ToDisplayString(evaluated);
            var text = ParagraphText.Build(host);
            text.Replace(token.Offset, token.Length, replacement);
        }

        if (structuralTokens.Count > 0)
        {
            structuralReplacements.Add(new(host, BuildStructuralReplacements(structuralTokens)));
        }
    }

    IReadOnlyList<OpenXmlElement> BuildStructuralReplacements(IReadOnlyList<(DocxTokenSite site, object value)> tokens)
    {
        var result = new List<OpenXmlElement>();
        foreach (var (_, value) in tokens)
        {
            switch (value)
            {
                case TokenValue.MarkdownToken md:
                    result.AddRange(MarkdownRendering.Render(md.Source, mainPart, headingOffset: 0));
                    break;
                case TokenValue.OpenXmlToken raw:
                    var ctx = new OpenXmlContextImpl(
                        mainPart,
                        new(mainPart),
                        StyleSet.Read(mainPart));
                    result.AddRange(raw.Render(ctx));
                    break;
            }
        }

        return result;
    }

    async Task<object> EvaluateTokenAsync(DocxTokenSite site, Paragraph host)
    {
        try
        {
            if (TryResolveExcelsiorTable(site) is { } excelsiorToken)
            {
                return excelsiorToken;
            }

            // Walk the parsed FluidTemplate to its OutputStatement and evaluate the underlying
            // Expression directly (filter chain included). This lets us see whether the value is a
            // TokenValue (markdown / openxml hatch) before falling back to string rendering, without
            // round-tripping through the Render() pipeline twice.
            var statements = ((Fluid.Parser.FluidTemplate)site.Template).Statements;
            if (statements.Count > 0 && statements[0] is OutputStatement output)
            {
                var fluidValue = await output.Expression.EvaluateAsync(context);
                var underlying = fluidValue.ToObjectValue();
                if (underlying is TokenValue tokenValue)
                {
                    return tokenValue;
                }

                return fluidValue.ToStringValue();
            }

            await using var writer = new StringWriter();
            await site.Template.RenderAsync(writer, System.Text.Encodings.Web.HtmlEncoder.Default, context);
            return writer.ToString();
        }
        catch (ParchmentException)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw new ParchmentRenderException(
                templateName,
                exception.Message,
                partUri,
                Snippet(host, site),
                site.Source,
                inner: exception);
        }
    }

    TokenValue? TryResolveExcelsiorTable(DocxTokenSite site)
    {
        if (excelsiorTables.IsEmpty || site.References.Count == 0)
        {
            return null;
        }

        // Match the token's full dotted reference (e.g. `Customer.Lines`) against the registered
        // map. Single-segment, multi-segment, and arbitrarily-nested paths from the root model
        // all flow through the same lookup. Loop-scope variables (e.g. `{{ line.SubItems }}`
        // inside `{% for line in Lines %}`) won't match because the map is keyed on paths from
        // the root model only — they fall through to normal Fluid evaluation.
        var reference = site.References[0];
        var dottedPath = string.Join('.', reference.Segments);
        if (!excelsiorTables.TryGet(dottedPath, out var entry))
        {
            return null;
        }

        var data = entry.Getter(rootModel);
        if (data == null)
        {
            return TokenValue.OpenXml(_ => []);
        }

        return TokenValue.OpenXml(_ => [ExcelsiorTableBridge.BuildTable(entry.ElementType, data, mainPart)]);
    }

    async Task ProcessLoopAsync(LoopNode loop)
    {
        if (!anchorMap.TryGetValue(loop.OpenAnchorName, out var open) ||
            !anchorMap.TryGetValue(loop.CloseAnchorName, out var close))
        {
            return;
        }

        var parent = open.Parent;
        if (parent == null)
        {
            return;
        }

        var items = await ResolveIterableAsync(loop);
        var bodyElements = CaptureBetween(open, close);
        OpenXmlElement insertAnchor = open;

        var nameMap = new Dictionary<string, string>(StringComparer.Ordinal);
        var cloneAnchors = new Dictionary<string, Paragraph>(StringComparer.Ordinal);
        var clones = new List<OpenXmlElement>(bodyElements.Count);
        var clonedBody = new RangeNode[loop.Body.Count];

        foreach (var item in items)
        {
            context.SetValue(loop.LoopVariable, item);
            clones.Clear();
            foreach (var element in bodyElements)
            {
                clones.Add(element.CloneNode(true));
            }

            nameMap.Clear();
            cloneAnchors.Clear();
            RefreshAnchorsAndBuildMap(clones, nameMap, cloneAnchors);
            var clonedRunner = new ScopeTreeRunner(
                templateName,
                partUri,
                cloneAnchors,
                context,
                mainPart,
                rootModel,
                excelsiorTables);
            RemapBodyInto(loop.Body, nameMap, clonedBody);
            await clonedRunner.RunAsync(clonedBody);
            clonedRunner.ApplyStructural();

            foreach (var clone in clones)
            {
                insertAnchor = parent.InsertAfter(clone, insertAnchor);
            }
        }

        foreach (var element in bodyElements)
        {
            element.Remove();
        }

        open.Remove();
        close.Remove();
    }

    static List<OpenXmlElement> CaptureBetween(OpenXmlElement start, OpenXmlElement end)
    {
        var result = new List<OpenXmlElement>();
        var cursor = start.NextSibling();
        while (cursor != null && cursor != end)
        {
            result.Add(cursor);
            cursor = cursor.NextSibling();
        }

        return result;
    }

    static void RefreshAnchorsAndBuildMap(
        IReadOnlyList<OpenXmlElement> clones,
        Dictionary<string, string> nameMap,
        Dictionary<string, Paragraph> anchorMap)
    {
        foreach (var clone in clones)
        {
            foreach (var start in clone.Descendants<BookmarkStart>())
            {
                var name = start.Name?.Value;
                if (name == null || !name.StartsWith(Anchors.Prefix, StringComparison.Ordinal))
                {
                    continue;
                }

                if (!nameMap.TryGetValue(name, out var replacement))
                {
                    replacement = Anchors.Prefix + Guid.NewGuid().ToString("N");
                    nameMap[name] = replacement;
                }

                start.Name = replacement;

                var host = start.Ancestors<Paragraph>().FirstOrDefault();
                if (host != null)
                {
                    anchorMap[replacement] = host;
                }
            }
        }
    }

    static void RemapBodyInto(
        IReadOnlyList<RangeNode> body,
        Dictionary<string, string> nameMap,
        RangeNode[] target)
    {
        for (var i = 0; i < body.Count; i++)
        {
            target[i] = Remap(body[i], nameMap);
        }
    }

    static IReadOnlyList<RangeNode> RemapBody(
        IReadOnlyList<RangeNode> body,
        Dictionary<string, string> nameMap) =>
        body.Select(node => Remap(node, nameMap)).ToList();

    static RangeNode Remap(RangeNode node, Dictionary<string, string> nameMap) =>
        node switch
        {
            SubstitutionNode s => new SubstitutionNode(Rename(s.AnchorName, nameMap), s.Tokens),
            StaticNode s => new StaticNode(Rename(s.AnchorName, nameMap)),
            LoopNode l => new LoopNode(
                Rename(l.OpenAnchorName, nameMap),
                Rename(l.CloseAnchorName, nameMap),
                l.Scope,
                l.LoopVariable,
                l.LoopSource,
                RemapBody(l.Body, nameMap)),
            IfNode i => new IfNode(
                Rename(i.OpenAnchorName, nameMap),
                Rename(i.CloseAnchorName, nameMap),
                i.Branches.Select(b => new IfBranch(Rename(b.AnchorName, nameMap), b.Condition, RemapBody(b.Body, nameMap))).ToList(),
                RemapBody(i.ElseBody, nameMap)),
            _ => node
        };

    static string Rename(string name, Dictionary<string, string> map) =>
        map.GetValueOrDefault(name, name);

    async Task<IEnumerable<FluidValue>> ResolveIterableAsync(LoopNode loop)
    {
        // Hand the loop source straight to Fluid: ForStatement.Source is an Expression that, when
        // evaluated, yields a FluidValue we can enumerate. This honors filters, complex paths,
        // arithmetic, and any value converters Fluid is configured with — none of which the previous
        // reflection-walk supported.
        var sourceValue = await loop.LoopSource.EvaluateAsync(context);
        return sourceValue.Enumerate(context);
    }

    async Task ProcessIfAsync(IfNode ifNode)
    {
        if (!anchorMap.TryGetValue(ifNode.OpenAnchorName, out var open) ||
            !anchorMap.TryGetValue(ifNode.CloseAnchorName, out var close))
        {
            return;
        }

        var parent = open.Parent;
        if (parent == null)
        {
            return;
        }

        IReadOnlyList<RangeNode>? chosen = null;
        foreach (var branch in ifNode.Branches)
        {
            if (!await EvaluateConditionAsync(branch.Condition))
            {
                continue;
            }

            chosen = branch.Body;
            break;
        }

        if (chosen == null && ifNode.ElseBody.Count > 0)
        {
            chosen = ifNode.ElseBody;
        }

        // Collect all branch paragraphs between open and close — everything that should be removed
        var allBranchParagraphs = CaptureBetween(open, close);

        if (chosen != null)
        {
            // Process chosen branch in place (no cloning — branch paragraphs are used once)
            var branchNodes = chosen;
            var branchAnchors = new Dictionary<string, Paragraph>(StringComparer.Ordinal);
            foreach (var p in allBranchParagraphs.OfType<Paragraph>())
            {
                var start = p.Elements<BookmarkStart>().FirstOrDefault(_ => _.Name?.Value?.StartsWith(Anchors.Prefix, StringComparison.Ordinal) == true);
                if (start?.Name?.Value != null)
                {
                    branchAnchors[start.Name.Value] = p;
                }
            }

            var innerRunner = new ScopeTreeRunner(templateName, partUri, branchAnchors, context, mainPart, rootModel, excelsiorTables);
            await innerRunner.RunAsync(branchNodes);
            innerRunner.ApplyStructural();

            // Only keep paragraphs that belong to the chosen branch; remove others
            var keep = new HashSet<OpenXmlElement>(CollectBranchParagraphs(chosen, branchAnchors));
            foreach (var element in allBranchParagraphs)
            {
                if (!keep.Contains(element))
                {
                    element.Remove();
                }
            }
        }
        else
        {
            foreach (var element in allBranchParagraphs)
            {
                element.Remove();
            }
        }

        open.Remove();
        close.Remove();
    }

    static IEnumerable<OpenXmlElement> CollectBranchParagraphs(
        IReadOnlyList<RangeNode> nodes,
        Dictionary<string, Paragraph> anchors)
    {
        foreach (var node in nodes)
        {
            switch (node)
            {
                case SubstitutionNode s when anchors.TryGetValue(s.AnchorName, out var p):
                    yield return p;
                    break;
                case StaticNode s when anchors.TryGetValue(s.AnchorName, out var p):
                    yield return p;
                    break;
                case LoopNode l:
                    if (anchors.TryGetValue(l.OpenAnchorName, out var lo))
                    {
                        yield return lo;
                    }

                    foreach (var child in CollectBranchParagraphs(l.Body, anchors))
                    {
                        yield return child;
                    }

                    if (anchors.TryGetValue(l.CloseAnchorName, out var lc))
                    {
                        yield return lc;
                    }

                    break;
                case IfNode ifn:
                    if (anchors.TryGetValue(ifn.OpenAnchorName, out var io))
                    {
                        yield return io;
                    }

                    foreach (var branch in ifn.Branches)
                    {
                        foreach (var child in CollectBranchParagraphs(branch.Body, anchors))
                        {
                            yield return child;
                        }
                    }

                    foreach (var child in CollectBranchParagraphs(ifn.ElseBody, anchors))
                    {
                        yield return child;
                    }

                    if (anchors.TryGetValue(ifn.CloseAnchorName, out var ic))
                    {
                        yield return ic;
                    }

                    break;
            }
        }
    }

    async Task<bool> EvaluateConditionAsync(Expression condition) =>
        (await condition.EvaluateAsync(context)).ToBooleanValue();

    static string ToDisplayString(object value) =>
        value switch
        {
            null => string.Empty,
            string s => s,
            _ => value.ToString() ?? string.Empty
        };

    static string Snippet(Paragraph host, DocxTokenSite site)
    {
        var text = ParagraphText.Build(host).InnerText;
        var start = Math.Max(0, site.Offset - 40);
        var end = Math.Min(text.Length, site.Offset + site.Length + 40);
        return text[start..end];
    }

    sealed record StructuralReplacement(Paragraph Host, IReadOnlyList<OpenXmlElement> Produced);
}
