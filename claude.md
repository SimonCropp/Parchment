# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build and test

The repo has two solutions: `src/` (library + source generator + tests + sample model) and `IntegrationTests/` (separate solution that consumes the packed nuget end-to-end).

```bash
# Build everything in src
dotnet build src --configuration Release

# Build the integration tests (requires src to have been built/packed first — Parchment is consumed as a PackageReference, not a ProjectReference)
dotnet build IntegrationTests --configuration Release
```

Tests use **TUnit**, not NUnit/xUnit. Each test project is `OutputType=Exe` with `TestingPlatformDotnetTestSupport=true`, and you run them with `dotnet run`, not `dotnet test`:

```bash
dotnet run --project src/Parchment.Tests --configuration Release
dotnet run --project src/Parchment.SourceGenerator.Tests --configuration Release
dotnet run --project IntegrationTests/IntegrationTests --configuration Release

# Run a single test (TUnit's filter syntax)
dotnet run --project src/Parchment.Tests --configuration Release -- --filter "FullyQualifiedName~Substitution"
```

Snapshot testing uses **Verify.TUnit + Verify.OpenXml + Morph.OpenXml.Skia**. A failing test writes `*.received.*` files alongside the corresponding `*.verified.*`. To accept a new/changed snapshot, rename `received` → `verified` (or use a Verify diff tool). PNG page renders only fire on `net10.0` when `Morph.OpenXml.Skia` is referenced — that's automatic, no opt-in needed.

CI is `src/appveyor.yml` (AppVeyor). The build script first installs every TTF/OTF in `src/Fonts/` into the Windows fonts dir (Aptos is bundled), validating each via `System.Drawing.Text.PrivateFontCollection` so a CRLF-mangled font fails the build instead of producing confusing "font not found" errors at test time.

## Architecture

Parchment combines a .NET data model with one of two template formats and produces a `.docx`. The **two flows** share token scanning, the Fluid singletons, the markdown renderer, and the snapshot/test pipeline, but execute very differently.

### Flow A — Docx template (`RegisterDocxTemplate<T>` / `Render`)

This is the structurally interesting flow. The template is a real `.docx` containing liquid tokens scattered across paragraphs (substitutions like `{{ Customer.Name }}` plus block tags `{% for %}` / `{% if %}`). We can't just hand the whole file to Fluid because body paragraphs contain Word run-level formatting that text-based liquid would destroy. Instead:

1. **Token scanner** (`Tokens/TokenScanner.cs`, `Word/ParagraphText.cs`) — walks each paragraph in `MainDocumentPart`, every `HeaderPart`, `FooterPart`, `FootnotesPart`, `EndnotesPart`. For each paragraph it builds a flat `InnerText` plus a `RunMap` that maps character offsets back to the source `<w:t>` elements, regex-scans for `{{ ... }}` and `{% ... %}`, and classifies the paragraph as `Static` / `Substitution` / `Block`.

2. **Anchor bookmarks** (`Word/Anchors.cs`) — every token-bearing paragraph gets an invisible `<w:bookmarkStart w:name="parchment-anchor-{guid}"/>` injected at scan time. These survive byte-stream cloning intact and let render-time look up host paragraphs by name without fragile positional indices. Bookmarks are stripped from the final output before save.

3. **Scope tree** (`Tokens/RangeNode.cs`, `ScopeTreeBuilder.cs`) — once paragraphs are classified, a recursive walk groups block-tag paragraphs with their bodies into `LoopNode` / `IfNode` / `IfBranch` records, with non-block paragraphs as `SubstitutionNode` / `StaticNode` leaves. Block tags MUST sit alone in their own paragraph; mixed inline text + block tag is rejected at registration time.

4. **Reference validator** (`TemplateStore.cs::ReferenceValidator`) — walks the scope tree and, for each token, runs `ModelValidator` against `TModel` via reflection. Loop variables introduce scoped bindings (`item: ElementType`) into a `Dictionary<string, Type>` that nested loops/conditionals inherit. Throws `ParchmentRegistrationException` on the first missing member.

5. **Render time** (`Tokens/ScopeTreeRunner.cs`, `RegisteredDocxTemplate.cs`) — clones the canonical bytes into a fresh `WordprocessingDocument`, builds an anchor → paragraph map, walks the cached scope tree:
   - **Substitutions**: get the parsed `FluidTemplate`, pull its `OutputStatement.Expression`, call `Expression.EvaluateAsync(context)`, check whether the resulting `FluidValue.ToObjectValue()` is a `TokenValue`. If so, queue a structural replacement; otherwise apply in-paragraph text substitution. In-paragraph substitutions are applied **in reverse offset order** so earlier offsets stay valid.
   - **Loops**: call `LoopNode.LoopSource.EvaluateAsync(context).Enumerate(context)` to iterate (this is Fluid's own `Expression`, not a wrapped template). For each item, deep-clone the body elements, rewrite their anchor-bookmark names to fresh per-iteration GUIDs, and recursively process the clones with the loop variable bound in the scope.
   - **Conditionals**: call `IfBranch.Condition.EvaluateAsync(context).ToBooleanValue()`. The chosen branch is processed in place; non-chosen branch paragraphs are removed.
   - Structural replacements are applied last, top-down: each host paragraph is replaced with the produced elements via `parent.InsertAfter` + `host.Remove`.

**Important**: `ScopeTreeRunner` uses Fluid's actual AST nodes (`Fluid.Ast.Expression`, `ForStatement`, `IfStatement`, `OutputStatement`) for runtime evaluation. We do NOT hand-roll loop iteration via reflection or string-render conditions to `"true"`/`"false"`. `IdentifierVisitor` is only used at **registration time** for compile-time-style validation against a .NET `Type`.

### Flow B — Markdown template (`RegisterMarkdownTemplate<T>` / `Render`)

The whole `.md` source is one Fluid template parsed once. At render time:

1. Render the cached `IFluidTemplate` against the model → final markdown text.
2. `Markdig.Markdown.Parse(text, MarkdigPipeline.Pipeline)` → `MarkdownDocument`.
3. Open the optional `styleSource` docx (or a built-in blank), preserve its `<w:sectPr>` (page size, margins, header/footer references), clear the body, walk the markdown AST through `OpenXmlMarkdownRenderer`, append elements, re-attach the sectPr.
4. Header parts, footer parts, styles, theme, font tables, and every other part are passed through verbatim — the renderer only mutates `Body` and (additively) `NumberingDefinitionsPart`.

`MarkdigPipeline` enables: emphasis-extras, grid + pipe tables, autolinks, list-extras (alpha + roman), smarty pants, and **generic attributes** (`{.StyleName}` syntax for attaching Word styles to headings/paragraphs — the renderer honors the first class on each block).

### `OpenXmlMarkdownRenderer` — container stack pattern

The markdown renderer (`Markdown/OpenXmlMarkdownRenderer.cs`) subclasses `Markdig.Renderers.RendererBase` (NOT `TextRendererBase` — it has no `TextWriter`). State is a `Stack<ContainerState>` where each `ContainerState` holds `{ List<OpenXmlElement> Blocks, List<OpenXmlElement> CurrentRuns }`. Inline renderers append runs to `Top.CurrentRuns`; block renderers call `FlushParagraph()` to wrap accumulated runs into a `<w:p>`. Nested constructs (table cells, quote blocks) push/pop new container states. **One renderer instance per render** — do not cache; the stack is mutable.

When the renderer encounters an `HtmlBlock` or `HtmlInline`, it delegates to `OpenXmlHtml.WordHtmlConverter.ToElements(html, mainPart, settings)`, passing `HeadingLevelOffset` derived from the renderer's current heading depth. **OpenXmlHtml ≥ 0.4.0 is required** — the heading-offset parameter was added specifically for Parchment.

### Fluid integration (`Liquid/SharedFluid.cs`)

A single static `FluidParser` and `TemplateOptions` shared across all renders (Fluid documents these as thread-safe and recommends per-process singletons). `RegisterModel(Type)` walks the model's reachable type graph and calls `MemberAccessStrategy.Register<T>()` on every POCO type via reflection (`MakeGenericMethod`). This is **load-bearing**: Fluid 2.x's `DefaultMemberAccessStrategy` does NOT auto-discover nested types from a model passed via `TemplateContext(model)` — without the recursive walk, `{{ Customer.Name }}` returns empty and you waste an hour debugging it.

Filters (`Liquid/Filters.cs`): `markdown`, `escape_xml`, `bullet_list`, `numbered_list`. Filters returning a `TokenValue` (wrapped in `ObjectValue`) trigger structural replacement at the substitution site — don't replace this with a string-only filter unless you also wire a separate detection path.

### Source generator (`src/Parchment.SourceGenerator/`)

`netstandard2.0;net10.0`, packaged inside `Parchment.nupkg` under `analyzers/dotnet/roslyn5.0/cs` and `analyzers/dotnet/roslyn5.3/cs`. `IIncrementalGenerator` + `ForAttributeWithMetadataName("Parchment.ParchmentTemplateAttribute")`. For each `[ParchmentTemplate(path, modelType)]`-decorated class:

1. Resolves `path` against `<AdditionalFiles>` in the consuming project.
2. Reads the docx via `AdditionalText.Path` + `File.ReadAllBytes` (binary AdditionalFiles pattern — `GetText()` doesn't work for binary).
3. Unzips with `System.IO.Compression.ZipArchive` and walks `word/document.xml`, `word/header*.xml`, `word/footer*.xml`, `word/footnotes.xml`, `word/endnotes.xml` via `XDocument`.
4. Regex-scans paragraph `InnerText` for tokens (the SG has its own `TokenScanner.cs` — it does NOT share source with the runtime library because the runtime library targets `net10.0` and depends on `DocumentFormat.OpenXml`, neither of which work in a netstandard2.0 Roslyn analyzer).
5. Validates references against the model's `ITypeSymbol` (Roslyn semantic model, not reflection).
6. Emits diagnostics `PARCH001`–`PARCH006` and a generated partial class with a `RegisterWith(TemplateStore store)` helper.

The generator deliberately **does not** embed docx bytes into the assembly — it generates a runtime helper that reads the file from disk. Users deploy the docx alongside their app.

`Parchment.SourceGenerator.csproj` has `<InternalsVisibleTo Include="Parchment.SourceGenerator.Tests"/>` — but signed assemblies don't honor IVT without a public-key match, so the test-facing types (`TokenScanner`, `TokenKind`, `Token`) are `public`. Records use `init`, which requires the `IsExternalInit` polyfill in `Polyfills.cs` for the `netstandard2.0` target.

### Determinism guarantee

Same template + same model → byte-identical output. Avoid `w:rsid` randomness, never set `PackageProperties.Created`, no timestamps anywhere. `DeterminismTests.cs` renders a sample twice and asserts byte equality. Users hash outputs for caching/dedup, so don't break this.

## Non-obvious things that will trip you up

- **Tokens straddling run boundaries**: Word splits text into multiple `<w:r>` elements when formatting changes, when proofing markers fire, or when smart-quote autocorrect runs. `{{ customer.name }}` can land across N runs. The scanner uses `paragraph.InnerText` + a `RunMap` (offset → `<w:t>` element) so substitutions land correctly. The formatting of the **first run** containing the opening `{{` wins for the entire substitution — document this constraint when adding tests.

- **PascalCase tokens**: Liquid in Parchment uses PascalCase (`{{ Customer.Name }}`), not snake_case. Fluid's default member access compares case-insensitively against the actual property name. There is no snake-case → PascalCase translation layer; an earlier attempt to wire `MemberNameStrategies.SnakeCase` was abandoned because that API doesn't exist in Fluid 2.15.

- **`OpenXmlMarkdownRenderer` is not thread-safe** — one instance per render. The `Stack<ContainerState>` and `ObjectRenderers` collection are mutable. The `RegisteredTemplate` (cached canonical bytes + scope tree) IS immutable and safe to share, so concurrent renders work — they just each get their own renderer.

- **`appveyor.yml` font validation step** — every TTF/OTF in `src/Fonts/` is loaded through `System.Drawing.Text.PrivateFontCollection` BEFORE being copied to `%WINDIR%\Fonts`. This catches Git CRLF corruption upfront. If you add a font, mark it as binary in `.gitattributes` (`*.ttf binary`, `*.otf binary` — already present).

- **`ParchmentModel` is a separate project** (not `Model`) to avoid name clashes with common test fixture names in IDE autocomplete.
