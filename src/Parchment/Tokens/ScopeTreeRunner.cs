namespace Parchment.Tokens;

/// <summary>
/// Walks a cached scope tree against a cloned docx, evaluating substitution tokens and block tags
/// to produce the final rendered document.
/// </summary>
internal sealed class ScopeTreeRunner(
    string templateName,
    string partUri,
    Dictionary<string, Paragraph> anchorMap,
    TemplateContext context,
    MainDocumentPart mainPart)
{
    readonly List<StructuralReplacement> structuralReplacements = [];

    public void Run(IReadOnlyList<RangeNode> nodes) =>
        Process(nodes);

    public void ApplyStructural()
    {
        foreach (var replacement in structuralReplacements)
        {
            var host = replacement.Host;
            var parent = host.Parent;
            if (parent == null)
            {
                continue;
            }

            OpenXmlElement cursor = host;
            foreach (var produced in replacement.Produced)
            {
                cursor = parent.InsertAfter(produced, cursor) ?? cursor;
            }

            host.Remove();
        }

        structuralReplacements.Clear();
    }

    void Process(IReadOnlyList<RangeNode> nodes)
    {
        foreach (var node in nodes)
        {
            Process(node);
        }
    }

    void Process(RangeNode node)
    {
        switch (node)
        {
            case SubstitutionNode substitution:
                ProcessSubstitution(substitution);
                break;
            case LoopNode loop:
                ProcessLoop(loop);
                break;
            case IfNode ifNode:
                ProcessIf(ifNode);
                break;
            case StaticNode:
                break;
        }
    }

    void ProcessSubstitution(SubstitutionNode node)
    {
        if (!anchorMap.TryGetValue(node.AnchorName, out var host))
        {
            return;
        }

        var sortedByOffset = node.Tokens.OrderByDescending(x => x.Offset).ToList();
        var structuralTokens = new List<(DocxTokenSite site, object value)>();

        foreach (var token in sortedByOffset)
        {
            var evaluated = EvaluateToken(token, host);
            if (evaluated is TokenValue.MarkdownToken || evaluated is TokenValue.OpenXmlToken)
            {
                structuralTokens.Add((token, evaluated));
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
                    result.AddRange(Markdown.MarkdownRendering.Render(md.Source, mainPart, headingOffset: 0));
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

    object EvaluateToken(DocxTokenSite site, Paragraph host)
    {
        try
        {
            // If the token resolves directly to a TokenValue on the model (via a filter or a typed
            // property), prefer that so structural rendering kicks in. Otherwise render to string.
            if (TryGetDirectValue(site, out var direct))
            {
                return direct;
            }

            return site.Template.Render(context);
        }
        catch (ParchmentException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new ParchmentRenderException(
                templateName,
                ex.Message,
                partUri,
                Snippet(host, site),
                site.Source,
                inner: ex);
        }
    }

    bool TryGetDirectValue(DocxTokenSite site, out object value)
    {
        // For v1, direct TokenValue extraction is done by the Fluid filter chain: a filter returning
        // an ObjectValue containing a TokenValue short-circuits into a render-time structural edit.
        // We detect this by evaluating the token as an object via the model accessor.
        value = null!;
        if (site.References.Count == 0)
        {
            return false;
        }

        var first = site.References[0];
        var raw = ResolveContextPath(first);
        if (raw is TokenValue tokenValue)
        {
            value = tokenValue;
            return true;
        }

        return false;
    }

    object? ResolveContextPath(IdentifierPath path)
    {
        var rootName = path.Root;
        var fromContext = context.GetValue(rootName)?.ToObjectValue();
        var value = fromContext;
        var start = 1;
        if (value == null)
        {
            value = context.Model?.ToObjectValue();
            start = 0;
            if (value == null)
            {
                return null;
            }
        }

        for (var i = start; i < path.Segments.Count; i++)
        {
            var segment = path.Segments[i];
            var property = value.GetType().GetProperty(
                segment,
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            if (property == null)
            {
                return null;
            }

            value = property.GetValue(value);
            if (value == null)
            {
                return null;
            }
        }

        return value;
    }

    void ProcessLoop(LoopNode loop)
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

        var items = ResolveIterable(loop);
        var bodyElements = CaptureBetween(open, close);
        OpenXmlElement insertAnchor = open;

        foreach (var item in items)
        {
            context.SetValue(loop.LoopVariable, item);
            var clones = bodyElements.Select(x => x.CloneNode(true)).ToList();
            var nameMap = RefreshAnchorNames(clones);
            var clonedRunner = new ScopeTreeRunner(
                templateName,
                partUri,
                BuildCloneAnchorMap(clones, nameMap),
                context,
                mainPart);
            var clonedBody = RemapBody(loop.Body, nameMap);
            clonedRunner.Run(clonedBody);
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

    static Dictionary<string, string> RefreshAnchorNames(IReadOnlyList<OpenXmlElement> clones)
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var clone in clones)
        {
            foreach (var start in clone.Descendants<BookmarkStart>())
            {
                var name = start.Name?.Value;
                if (name == null || !name.StartsWith(Anchors.Prefix, StringComparison.Ordinal))
                {
                    continue;
                }

                if (!map.TryGetValue(name, out var replacement))
                {
                    replacement = Anchors.Prefix + Guid.NewGuid().ToString("N");
                    map[name] = replacement;
                }

                start.Name = replacement;
            }
        }

        return map;
    }

    static Dictionary<string, Paragraph> BuildCloneAnchorMap(
        IReadOnlyList<OpenXmlElement> clones,
        Dictionary<string, string> nameMap)
    {
        var map = new Dictionary<string, Paragraph>(StringComparer.Ordinal);
        foreach (var clone in clones)
        {
            foreach (var start in clone.Descendants<BookmarkStart>())
            {
                var name = start.Name?.Value;
                if (name == null || !name.StartsWith(Anchors.Prefix, StringComparison.Ordinal))
                {
                    continue;
                }

                var host = start.Ancestors<Paragraph>().FirstOrDefault();
                if (host != null)
                {
                    map[name] = host;
                }
            }
        }

        return map;
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

    IEnumerable<object?> ResolveIterable(LoopNode loop)
    {
        // LoopSource is wrapped as `{{ expression }}` — we need the raw enumerable, not a string.
        // Walk the identifier path through the template context to find the underlying object.
        var refs = IdentifierVisitor.Collect(loop.LoopSource);
        if (refs.Count == 0)
        {
            yield break;
        }

        var raw = ResolveContextPath(refs[0]);
        if (raw is IEnumerable enumerable and not string)
        {
            foreach (var item in enumerable)
            {
                yield return item;
            }
        }
    }

    void ProcessIf(IfNode ifNode)
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
        OpenXmlElement? chosenStart = null;
        OpenXmlElement chosenEnd = close;

        for (var i = 0; i < ifNode.Branches.Count; i++)
        {
            var branch = ifNode.Branches[i];
            if (!EvaluateCondition(branch.Condition))
            {
                continue;
            }

            chosen = branch.Body;
            chosenStart = i == 0 ? open : anchorMap.GetValueOrDefault(branch.AnchorName);
            break;
        }

        if (chosen == null && ifNode.ElseBody.Count > 0)
        {
            chosen = ifNode.ElseBody;
        }

        // Collect all branch paragraphs between open and close — everything that should be removed
        var allBranchParagraphs = CaptureBetween(open, close);

        if (chosen != null && chosenStart != null)
        {
            // Process chosen branch in place (no cloning — branch paragraphs are used once)
            var branchNodes = chosen;
            var branchAnchors = new Dictionary<string, Paragraph>(StringComparer.Ordinal);
            foreach (var p in allBranchParagraphs.OfType<Paragraph>())
            {
                var start = p.Elements<BookmarkStart>().FirstOrDefault(x => x.Name?.Value?.StartsWith(Anchors.Prefix, StringComparison.Ordinal) == true);
                if (start?.Name?.Value != null)
                {
                    branchAnchors[start.Name.Value] = p;
                }
            }

            var innerRunner = new ScopeTreeRunner(templateName, partUri, branchAnchors, context, mainPart);
            innerRunner.Run(branchNodes);
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

    bool EvaluateCondition(IFluidTemplate template) =>
        template.Render(context) == "true";

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
