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
    ExcelsiorTableMap excelsiorTables,
    FormatMap formats,
    StringListMap stringLists,
    WordNumberingState numberingState)
{
    List<StructuralReplacement> structuralReplacements = [];

    public Task RunAsync(IReadOnlyList<RangeNode> nodes) =>
        ProcessAsync(nodes);

    public void ApplyStructural()
    {
        foreach (var replacement in structuralReplacements)
        {
            ApplyOne(replacement);
        }

        structuralReplacements.Clear();
    }

    static void ApplyOne(StructuralReplacement replacement)
    {
        var host = replacement.Host;
        var parent = host.Parent;
        if (parent == null)
        {
            return;
        }

        OpenXmlElement cursor = host;
        foreach (var produced in replacement.Produced)
        {
            cursor = parent.InsertAfter(produced, cursor);
        }

        host.Remove();
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

        // Snapshot the host paragraph's original character length once. As we apply substitutions
        // in reverse-offset order, later-offset edits don't shift earlier-offset tokens — so each
        // token's solo-ness can be judged against the original text.
        var originalLength = ParagraphText.Build(host).InnerText.Length;
        var sortedByOffset = node.Tokens.OrderByDescending(_ => _.Offset).ToList();
        var soloStructuralTokens = new List<(DocxTokenSite site, object value)>();
        var splitQueued = false;

        foreach (var token in sortedByOffset)
        {
            var evaluated = await EvaluateTokenAsync(token, host, node.Tokens.Count);
            if (evaluated is MarkdownToken or HtmlToken or OpenXmlToken)
            {
                if (node.Tokens.Count == 1 && token.Offset == 0 && token.Length == originalLength)
                {
                    // Whole host paragraph is the token — queue for replacement after every
                    // other in-paragraph substitution has run.
                    soloStructuralTokens.Add((token, evaluated));
                    continue;
                }

                ApplyNonSoloStructural(token, host, evaluated, ref splitQueued);
                continue;
            }

            if (evaluated is MutateToken mutate)
            {
                // Clear the token text, then hand the paragraph to the caller for in-place mutation.
                ParagraphText.Build(host).Replace(token.Offset, token.Length, string.Empty);
                var ctx = new OpenXmlContextImpl(
                    mainPart,
                    numberingState,
                    StyleSet.Read(mainPart));
                mutate.Apply(host, ctx);
                continue;
            }

            var replacement = ToDisplayString(evaluated);
            var text = ParagraphText.Build(host);
            text.Replace(token.Offset, token.Length, replacement);
        }

        if (soloStructuralTokens.Count > 0)
        {
            structuralReplacements.Add(new(host, BuildStructuralReplacements(soloStructuralTokens)));
        }
    }

    /// <summary>
    /// Splice or split the host paragraph for a non-solo structural token. Inline-equivalent
    /// output (single produced paragraph) is unwrapped and spliced in place; anything else
    /// (multiple blocks, a table) splits the host paragraph at the token offset and inserts
    /// the produced elements between the two halves.
    /// </summary>
    void ApplyNonSoloStructural(DocxTokenSite token, Paragraph host, object value, ref bool splitQueued)
    {
        var produced = RenderTokenValue(value);
        if (produced.Count == 0)
        {
            // Nothing to render — strip the token text from the host paragraph.
            ParagraphText.Build(host).Replace(token.Offset, token.Length, string.Empty);
            return;
        }

        if (ParagraphSplicer.IsInlineEquivalent(produced))
        {
            ParagraphSplicer.SpliceInline(host, token.Offset, token.Length, (Paragraph)produced[0]);
            return;
        }

        if (splitQueued)
        {
            // A second block-shaped substitution on the same paragraph would create overlapping
            // structural replacements; we don't try to compose them. The author needs to give
            // the second token its own paragraph.
            throw new ParchmentRenderException(
                templateName,
                $"Token '{token.Source}' produced block-level content but another structural substitution on the same paragraph already required a paragraph split. Move one of the tokens to its own paragraph.",
                partUri,
                Snippet(host, token),
                token.Source);
        }

        // Block-shaped output in a non-solo context: split the host paragraph and insert the
        // produced block elements between the resulting before/after halves. We queue this as a
        // structural replacement so any other in-paragraph substitutions on the same host have
        // already applied to the host's text by the time we replace it.
        var split = ParagraphSplicer.Split(host, token.Offset, token.Length, produced);
        structuralReplacements.Add(new(host, split));
        splitQueued = true;
    }

    IReadOnlyList<OpenXmlElement> BuildStructuralReplacements(IReadOnlyList<(DocxTokenSite site, object value)> tokens)
    {
        var result = new List<OpenXmlElement>();
        foreach (var (_, value) in tokens)
        {
            result.AddRange(RenderTokenValue(value));
        }

        return result;
    }

    IReadOnlyList<OpenXmlElement> RenderTokenValue(object value) =>
        value switch
        {
            MarkdownToken md => MarkdownRendering.Render(md.Source, mainPart, numberingState, headingOffset: 0).ToList(),
            HtmlToken html => OpenXmlHtml.WordHtmlConverter.ToElements(
                    html.Source,
                    mainPart,
                    new()
                    {
                        NumberingSession = numberingState.GetHtmlSession()
                    })
                .ToList(),
            OpenXmlToken raw => raw
                .Render(new OpenXmlContextImpl(mainPart, numberingState, StyleSet.Read(mainPart)))
                .ToList(),
            _ => []
        };

    async Task<object> EvaluateTokenAsync(DocxTokenSite site, Paragraph host, int siblingCount)
    {
        try
        {
            if (TryResolveExcelsiorTable(site) is { } excelsiorToken)
            {
                return excelsiorToken;
            }

            if (TryResolveFormatted(site) is { } formatted)
            {
                return formatted;
            }

            if (TryResolveStringList(site, host, siblingCount) is { } stringList)
            {
                return stringList;
            }

            // Walk the parsed FluidTemplate to its OutputStatement and evaluate the underlying
            // Expression directly (filter chain included). This lets us see whether the value is a
            // TokenValue (markdown / openxml hatch) before falling back to string rendering, without
            // round-tripping through the Render() pipeline twice.
            var statements = ((Fluid.Parser.FluidTemplate)site.Template).Statements;
            if (statements.Count > 0 &&
                statements[0] is OutputStatement output)
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
        if (excelsiorTables.IsEmpty ||
            site.References.Count == 0)
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

    TokenValue? TryResolveStringList(DocxTokenSite site, Paragraph host, int siblingCount)
    {
        if (stringLists.IsEmpty ||
            site.References.Count == 0)
        {
            return null;
        }

        var reference = site.References[0];
        var dottedPath = string.Join('.', reference.Segments);
        if (!stringLists.TryGet(dottedPath, out var entry))
        {
            return null;
        }

        // Auto-bullet rendering swaps the entire host paragraph. Skip silently if the token
        // doesn't sit alone — the user gets Fluid stringification in that case (consistent with
        // pre-feature behavior) instead of a surprising paragraph swap that drops surrounding text.
        if (siblingCount != 1)
        {
            return null;
        }

        var paragraphText = ParagraphText.Build(host).InnerText;
        if (site.Offset != 0 ||
            site.Length != paragraphText.Length)
        {
            return null;
        }

        // If the user attached a filter chain (e.g. `{{ Tags | numbered_list }}`), they're
        // explicitly opting into Fluid-driven rendering — let that path handle it.
        var statements = ((Fluid.Parser.FluidTemplate)site.Template).Statements;
        if (statements.Count == 0 ||
            statements[0] is not OutputStatement { Expression: MemberExpression })
        {
            return null;
        }

        var data = entry.Getter(rootModel);
        if (data is not IEnumerable<string> items)
        {
            return TokenValue.OpenXml(_ => []);
        }

        // Materialize so the deferred render delegate doesn't re-enumerate a fresh sequence
        // (and so a null model walk can't surface here).
        return TokenValueHelpers.BulletList(items.ToList());
    }

    TokenValue? TryResolveFormatted(DocxTokenSite site)
    {
        if (formats.IsEmpty || site.References.Count == 0)
        {
            return null;
        }

        var reference = site.References[0];
        var dottedPath = string.Join('.', reference.Segments);
        if (!formats.TryGet(dottedPath, out var entry))
        {
            return null;
        }

        var walker = rootModel;
        foreach (var segment in reference.Segments)
        {
            if (walker == null)
            {
                break;
            }

            var property = walker.GetType().GetProperty(segment, BindingFlags.Public | BindingFlags.Instance);
            if (property == null)
            {
                return null;
            }

            walker = property.GetValue(walker);
        }

        var text = walker as string ?? string.Empty;
        return entry.Kind switch
        {
            FormatKind.Html => TokenValue.Html(text),
            FormatKind.Markdown => TokenValue.Markdown(text),
            _ => null
        };
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

        // Detach the body elements and cache them. Cloning from detached copies avoids
        // walking the live document DOM and keeps namespace declarations clean on insertion.
        var bodyTemplates = new OpenXmlElement[bodyElements.Count];
        for (var i = 0; i < bodyElements.Count; i++)
        {
            bodyTemplates[i] = bodyElements[i].CloneNode(true);
        }

        var nameMap = new Dictionary<string, string>(StringComparer.Ordinal);
        var cloneAnchors = new Dictionary<string, Paragraph>(StringComparer.Ordinal);
        var clones = new List<OpenXmlElement>(bodyElements.Count);
        var clonedBody = new RangeNode[loop.Body.Count];

        foreach (var item in items)
        {
            context.SetValue(loop.LoopVariable, item);
            clones.Clear();
            foreach (var template in bodyTemplates)
            {
                clones.Add(template.CloneNode(true));
            }

            nameMap.Clear();
            cloneAnchors.Clear();
            RefreshAnchorsAndBuildMap(clones, nameMap, cloneAnchors);

            // Attach clones to a scratch parent before running the nested scope tree. Nested
            // ProcessLoopAsync / ProcessIfAsync / ApplyStructural all rely on open.Parent and
            // sibling traversal — on detached clones those return null and the nested scope
            // silently no-ops, leaving inner block-tag text in the output.
            var scratch = new Body();
            foreach (var clone in clones)
            {
                scratch.AppendChild(clone);
            }

            var clonedRunner = new ScopeTreeRunner(
                templateName,
                partUri,
                cloneAnchors,
                context,
                mainPart,
                rootModel,
                excelsiorTables,
                formats,
                stringLists,
                numberingState);
            RemapBodyInto(loop.Body, nameMap, clonedBody);
            await clonedRunner.RunAsync(clonedBody);
            clonedRunner.ApplyStructural();

            foreach (var produced in scratch.ChildElements.ToList())
            {
                produced.Remove();
                insertAnchor = parent.InsertAfter(produced, insertAnchor);
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
        while (cursor != null &&
               cursor != end)
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
                if (name == null ||
                    !name.StartsWith(Anchors.Prefix, StringComparison.Ordinal))
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
        body.Select(_ => Remap(_, nameMap)).ToList();

    static RangeNode Remap(RangeNode node, Dictionary<string, string> nameMap) =>
        node switch
        {
            SubstitutionNode substitutionNode => new SubstitutionNode(Rename(substitutionNode.AnchorName, nameMap), substitutionNode.Tokens),
            StaticNode staticNode => new StaticNode(Rename(staticNode.AnchorName, nameMap)),
            LoopNode loopNode => new LoopNode(
                Rename(loopNode.OpenAnchorName, nameMap),
                Rename(loopNode.CloseAnchorName, nameMap),
                loopNode.Scope,
                loopNode.LoopVariable,
                loopNode.LoopSource,
                RemapBody(loopNode.Body, nameMap)),
            IfNode ifNode => new IfNode(
                Rename(ifNode.OpenAnchorName, nameMap),
                Rename(ifNode.CloseAnchorName, nameMap),
                ifNode.Branches.Select(_ => new IfBranch(Rename(_.AnchorName, nameMap), _.Condition, RemapBody(_.Body, nameMap))).ToList(),
                RemapBody(ifNode.ElseBody, nameMap)),
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

        if (chosen == null &&
            ifNode.ElseBody.Count > 0)
        {
            chosen = ifNode.ElseBody;
        }

        // Collect all branch paragraphs between open and close — everything that should be removed
        var allBranchParagraphs = CaptureBetween(open, close);

        if (chosen == null)
        {
            foreach (var element in allBranchParagraphs)
            {
                element.Remove();
            }
        }
        else
        {
            // Process chosen branch in place (no cloning — branch paragraphs are used once)
            var branchAnchors = new Dictionary<string, Paragraph>(StringComparer.Ordinal);
            foreach (var p in allBranchParagraphs.OfType<Paragraph>())
            {
                var start = p.Elements<BookmarkStart>()
                    .FirstOrDefault(_ => _.Name?.Value?.StartsWith(Anchors.Prefix, StringComparison.Ordinal) == true);
                if (start?.Name?.Value != null)
                {
                    branchAnchors[start.Name.Value] = p;
                }
            }

            var innerRunner = new ScopeTreeRunner(templateName, partUri, branchAnchors, context, mainPart, rootModel, excelsiorTables, formats, stringLists, numberingState);
            await innerRunner.RunAsync(chosen);
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
