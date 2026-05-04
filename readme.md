# <img src="/src/icon.png" height="30px"> Parchment

[![Build status](https://img.shields.io/appveyor/build/SimonCropp/Parchment)](https://ci.appveyor.com/project/SimonCropp/Parchment)
[![NuGet Status](https://img.shields.io/nuget/v/Parchment.svg?label=Parchment)](https://www.nuget.org/packages/Parchment/)

Parchment is a Word (.docx) document generation library with two complementary rendering modes. It combines a .NET data model with either a docx template (token replacement, loops, conditionals) or a markdown template (full content rendering), both driven by [liquid](https://shopify.github.io/liquid/) via [Fluid](https://github.com/sebastienros/fluid). Markdown is parsed with [Markdig](https://github.com/xoofx/markdig); HTML chunks are converted via [OpenXmlHtml](https://github.com/SimonCropp/OpenXmlHtml).

**See [Milestones](../../milestones?state=closed) for release notes.**


## NuGet package

[Parchment](https://www.nuget.org/packages/Parchment/)


## Two modes

Parchment supports two complementary template formats:

1. **Docx template** — start from a hand-crafted Word document with `{{ field }}` substitution tokens and `{% for %}` / `{% if %}` block tags. Output preserves every detail of the source document.
2. **Markdown template** — start from a `.md` file with full liquid support. Markdown is parsed by Markdig and rendered into a target docx. Optionally provide a style-source `.docx` whose styles, headers, footers, and section properties are inherited.


## Docx template


### Input docx content

<!-- snippet: SubstitutionInput -->
<a id='snippet-SubstitutionInput'></a>
```cs
Invoice {{ Number }}

Customer: {{ Customer.Name }}

Total: {{ Total }} {{ Currency }}
```
<sup><a href='/src/Parchment.Tests/UsageTests.cs#L8-L14' title='Snippet source file'>snippet source</a> | <a href='#snippet-SubstitutionInput' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->


### Render

<!-- snippet: Substitution -->
<a id='snippet-Substitution'></a>
```cs
var store = new TemplateStore();
store.RegisterDocxTemplate<Invoice>("substitution", template);

using var stream = new MemoryStream();
await store.Render("substitution", SampleData.Invoice(), stream);
```
<sup><a href='/src/Parchment.Tests/UsageTests.cs#L17-L25' title='Snippet source file'>snippet source</a> | <a href='#snippet-Substitution' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->


### Token naming

Tokens match model members case-insensitively via Fluid's default member access strategy, but Parchment uses PascalCase by convention (`{{ Customer.Name }}`), not snake_case. There is no snake-case translation layer — stick with the property name as declared on the model.

Tokens in `MainDocumentPart`, every `HeaderPart` / `FooterPart`, `FootnotesPart`, and `EndnotesPart` are all scanned and substituted, so page headers, footers, and footnotes can use `{{ ... }}` and `{% ... %}` exactly like body paragraphs.


### Loops

A `{% for %}` paragraph and its matching `{% endfor %}` repeat the intervening paragraphs once per item.

```
{% for line in Lines %}
- {{ line.Description }}: {{ line.Quantity }} x {{ line.UnitPrice }}
{% endfor %}
```

### Conditionals

```
{% if Customer.IsPreferred %}
Preferred customer: {{ Customer.Name }}
{% else %}
Regular customer: {{ Customer.Name }}
{% endif %}
```


### Nested loops and conditionals

Loops and conditionals can be nested to arbitrary depth. The outer loop variable stays in scope for inner constructs.

```
{% for group in Groups %}
{{ group.Name }}
{% for item in group.Items %}
- {{ item }}
{% endfor %}
{% endfor %}
```


### Token override hatches

A token can resolve to one of three values:

- `TokenValue.Text(string)` — plain string substitution (the default for raw model values).
- `TokenValue.Markdown(string)` — the value is rendered as markdown via Markdig and spliced into the host paragraph.
- `TokenValue.OpenXml(Func<IOpenXmlContext, IEnumerable<OpenXmlElement>>)` — the value is a function that emits raw OpenXML elements. Useful for rich tables, generated charts, custom-styled lists.
- `TokenValue.Mutate(Action<Paragraph, IOpenXmlContext>)` — the callback receives the host paragraph and mutates it in place. The token text is cleared before the callback runs. Useful for adding runs with custom formatting, injecting bookmarks, or tweaking paragraph properties while preserving the original paragraph.

#### List filters

`bullet_list` and `numbered_list` render an `IEnumerable<string>` property as a real Word list (`<w:numPr>` with a proper numbering definition), not as literal text. The token must sit alone in its paragraph — the host paragraph is replaced with one `<w:p>` per item.

Content:

<!-- snippet: BulletListFilterContent -->
<a id='snippet-BulletListFilterContent'></a>
```cs
using var template = DocxTemplateBuilder.Build(
    """
    Tags:

    {{ Tags | bullet_list }}
    """);
```
<sup><a href='/src/Parchment.Tests/Docx/TokenOverrideTests.cs#L156-L165' title='Snippet source file'>snippet source</a> | <a href='#snippet-BulletListFilterContent' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

Render:

<!-- snippet: BulletListFilterRender -->
<a id='snippet-BulletListFilterRender'></a>
```cs
var store = new TemplateStore();
store.RegisterDocxTemplate<Invoice>("bullet-filter", template);
using var stream = new MemoryStream();
await store.Render("bullet-filter", SampleData.Invoice(), stream);
```
<sup><a href='/src/Parchment.Tests/Docx/TokenOverrideTests.cs#L167-L174' title='Snippet source file'>snippet source</a> | <a href='#snippet-BulletListFilterRender' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

`numbered_list` is identical in shape — swap the filter name to produce a decimal-numbered list instead of bullets.


#### `escape_xml` filter

Escapes `<`, `>`, `&`, `"`, and `'` in a string value. Useful when a token's value is spliced into a context where raw markup would be interpreted — e.g. feeding a user-supplied string into a `TokenValue.Markdown` that contains HTML blocks:

```
{{ UserSuppliedComment | escape_xml }}
```


#### Markdown property

Declare a model property as `TokenValue` and return `TokenValue.Markdown(...)` to inject rendered markdown at the substitution site:

Model:

<!-- snippet: MarkdownPropertyModel -->
<a id='snippet-MarkdownPropertyModel'></a>
```cs
public class NoteModel
{
    public required string Title { get; init; }
    public required TokenValue Body { get; init; }
}
```
<sup><a href='/src/Parchment.Tests/Docx/TokenOverrideTests.cs#L3-L11' title='Snippet source file'>snippet source</a> | <a href='#snippet-MarkdownPropertyModel' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

Content:

<!-- snippet: MarkdownPropertyContent -->
<a id='snippet-MarkdownPropertyContent'></a>
```cs
# {{ Title }}

{{ Body }}
```
<sup><a href='/src/Parchment.Tests/Docx/TokenOverrideTests.cs#L19-L23' title='Snippet source file'>snippet source</a> | <a href='#snippet-MarkdownPropertyContent' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

Render:

<!-- snippet: MarkdownPropertyRender -->
<a id='snippet-MarkdownPropertyRender'></a>
```cs
var store = new TemplateStore();
store.RegisterDocxTemplate<NoteModel>("markdown-hatch", template);
await store.Render(
    "markdown-hatch",
    new NoteModel
    {
        Title = "Weekly summary",
        Body = TokenValue.Markdown(
            """
            ## Highlights

            - Shipped the **new feature**
            - Closed _several_ bugs
            - Ran a code review

            > Stay the course
            """)
    },
    stream);
```
<sup><a href='/src/Parchment.Tests/Docx/TokenOverrideTests.cs#L26-L48' title='Snippet source file'>snippet source</a> | <a href='#snippet-MarkdownPropertyRender' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->


#### Markdown filter

Alternatively, use the `| markdown` filter on a plain `string` property:

Model:

<!-- snippet: MarkdownFilterModel -->
<a id='snippet-MarkdownFilterModel'></a>
```cs
public class ArticleModel
{
    public required string Heading { get; init; }
    public required string Content { get; init; }
}
```
<sup><a href='/src/Parchment.Tests/Docx/TokenOverrideTests.cs#L54-L62' title='Snippet source file'>snippet source</a> | <a href='#snippet-MarkdownFilterModel' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

Content:

<!-- snippet: MarkdownFilterContent -->
<a id='snippet-MarkdownFilterContent'></a>
```cs
# {{ Heading }}

{{ Content | markdown }}
```
<sup><a href='/src/Parchment.Tests/Docx/TokenOverrideTests.cs#L70-L74' title='Snippet source file'>snippet source</a> | <a href='#snippet-MarkdownFilterContent' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

Render:

<!-- snippet: MarkdownFilterRender -->
<a id='snippet-MarkdownFilterRender'></a>
```cs
var store = new TemplateStore();
store.RegisterDocxTemplate<ArticleModel>("markdown-filter", template);
await store.Render(
    "markdown-filter",
    new ArticleModel
    {
        Heading = "Release notes",
        Content =
            """
            ### Bug fixes

            - Fixed crash on **empty input**
            - Resolved _timeout_ in batch mode
            """
    },
    stream);
```
<sup><a href='/src/Parchment.Tests/Docx/TokenOverrideTests.cs#L77-L96' title='Snippet source file'>snippet source</a> | <a href='#snippet-MarkdownFilterRender' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

Both approaches produce the same structural replacement — the host paragraph is swapped with the rendered markdown elements when the token is the entire paragraph. If the token shares its paragraph with other text or with sibling tokens, the runtime falls back to inline splicing (single produced paragraph → its runs are extracted and merged into the host) or paragraph splitting (multiple produced blocks → host is split at the token offset and the produced blocks slot between the two halves). See [Inline-aware structural replacement](#inline-aware-structural-replacement) for the full rules.

**Markdown templates**: Neither `TokenValue.Markdown` nor `| markdown` is needed when using `RegisterMarkdownTemplate`. The entire template is already markdown — a plain `string` property containing markdown syntax is interpolated into the source before Markdig parses it, so formatting just works:

Model:

<!-- snippet: MarkdownTemplatePropertyModel -->
<a id='snippet-MarkdownTemplatePropertyModel'></a>
```cs
public class BriefModel
{
    public required string Title { get; init; }
    public required string Details { get; init; }
}
```
<sup><a href='/src/Parchment.Tests/Markdown/MarkdownFlowTests.cs#L57-L65' title='Snippet source file'>snippet source</a> | <a href='#snippet-MarkdownTemplatePropertyModel' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

Content:

<!-- snippet: MarkdownTemplatePropertyContent -->
<a id='snippet-MarkdownTemplatePropertyContent'></a>
```handlebars
# {{ Title }}

{{ Details }}
```
<sup><a href='/src/Parchment.Tests/Markdown/MarkdownFlowTests.cs#L73-L77' title='Snippet source file'>snippet source</a> | <a href='#snippet-MarkdownTemplatePropertyContent' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

Render:

<!-- snippet: MarkdownTemplatePropertyUsage -->
<a id='snippet-MarkdownTemplatePropertyUsage'></a>
```cs
var store = new TemplateStore();
store.RegisterMarkdownTemplate<BriefModel>(
    "brief",
    markdown,
    styleSource);

await store.Render(
    "brief",
    new BriefModel
    {
        Title = "Sprint recap",
        Details =
            """
            ## Done

            - Landed the **search** feature
            - Fixed _three_ regressions

            > Ship it.
            """
    },
    targetStream);
```
<sup><a href='/src/Parchment.Tests/Markdown/MarkdownFlowTests.cs#L82-L107' title='Snippet source file'>snippet source</a> | <a href='#snippet-MarkdownTemplatePropertyUsage' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->


#### Mutate paragraph

Use `TokenValue.Mutate` to receive the host paragraph and modify it in place. The token text is cleared before the callback runs, so the original paragraph structure (properties, styles) is preserved:

Model:

<!-- snippet: MutateModel -->
<a id='snippet-MutateModel'></a>
```cs
public class StyledModel
{
    public required string Label { get; init; }
    public required TokenValue Highlight { get; init; }
}
```
<sup><a href='/src/Parchment.Tests/Docx/TokenOverrideTests.cs#L102-L110' title='Snippet source file'>snippet source</a> | <a href='#snippet-MutateModel' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

Content:

<!-- snippet: MutateContent -->
<a id='snippet-MutateContent'></a>
```cs
{{ Label }}

{{ Highlight }}
```
<sup><a href='/src/Parchment.Tests/Docx/TokenOverrideTests.cs#L118-L122' title='Snippet source file'>snippet source</a> | <a href='#snippet-MutateContent' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

Render:

<!-- snippet: MutateRender -->
<a id='snippet-MutateRender'></a>
```cs
var store = new TemplateStore();
store.RegisterDocxTemplate<StyledModel>("mutate", template);
await store.Render(
    "mutate",
    new StyledModel
    {
        Label = "Before",
        Highlight = TokenValue.Mutate((paragraph, _) =>
        {
            paragraph.Append(
                new Run(
                    new RunProperties(
                        new Bold()),
                    new Text("Custom content")
                    {
                        Space = SpaceProcessingModeValues.Preserve
                    }));
        })
    }, stream);
```
<sup><a href='/src/Parchment.Tests/Docx/TokenOverrideTests.cs#L125-L147' title='Snippet source file'>snippet source</a> | <a href='#snippet-MutateRender' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->


### Excelsior tables

Mark any collection property on the model with `[ExcelsiorTable]` and the matching `{{ ... }}` substitution is rendered as a fully-formatted Word table by [Excelsior](https://github.com/SimonCropp/Excelsior) at render time. Headings, column ordering, formatting, null display, and custom render callbacks all come from Excelsior's `[Column]` attribute on the element type — the same configuration surface used for spreadsheets.

Mark the collection on the model:

<!-- snippet: ExcelsiorTableModel -->
<a id='snippet-ExcelsiorTableModel'></a>
```cs
public class Quote
{
    public required string Reference { get; init; }

    [ExcelsiorTable]
    public required IReadOnlyList<QuoteLine> Lines { get; init; }
}

public class QuoteLine
{
    [Column(Heading = "Item", Order = 1)]
    public required string Description { get; init; }

    [Column(Heading = "Qty", Order = 2)]
    public required int Quantity { get; init; }

    [Column(Order = 3, Format = "C0")]
    public required decimal UnitPrice { get; init; }
}
```
<sup><a href='/src/Parchment.Tests/Docx/ExcelsiorTableTests.cs#L12-L34' title='Snippet source file'>snippet source</a> | <a href='#snippet-ExcelsiorTableModel' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

Drop a `{{ Lines }}` substitution into the template on its own line. The template:

![Template before render](/src/Parchment.Tests/Scenarios/excelsior-table/input.png)

Register and render normally:

<!-- snippet: ExcelsiorTableUsage -->
<a id='snippet-ExcelsiorTableUsage'></a>
```cs
var templatePath = Path.Combine(ScenarioPath("excelsior-table"), "input.docx");

var store = new TemplateStore();
store.RegisterDocxTemplate<Quote>("excelsior-quote", templatePath);

var model = new Quote
{
    Reference = "Q-2026-0042",
    Lines =
    [
        new()
        {
            Description = "Strategy workshop",
            Quantity = 2,
            UnitPrice = 4500m
        },
        new()
        {
            Description = "Implementation support",
            Quantity = 8,
            UnitPrice = 1750m
        },
        new()
        {
            Description = "Documentation review",
            Quantity = 1,
            UnitPrice = 950m
        }
    ]
};

using var stream = new MemoryStream();
await store.Render("excelsior-quote", model, stream);
```
<sup><a href='/src/Parchment.Tests/Docx/ExcelsiorTableTests.cs#L150-L186' title='Snippet source file'>snippet source</a> | <a href='#snippet-ExcelsiorTableUsage' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

The rendered output:

![Rendered output](/src/Parchment.Tests/Scenarios/excelsior-table/output%23page01.verified.png)

Rules:

- The substitution must sit alone in its own paragraph — structural table replacement swaps the entire host paragraph, so surrounding text would be discarded. The runtime throws at registration (`ParchmentRegistrationException`) and the source generator emits `PARCH007` if this is violated.
- The substitution must be a plain member-access expression — filters (`{{ Lines | reverse }}`) and arithmetic are rejected because the Excelsior path walks the model object directly and bypasses Fluid evaluation. Diagnostic `PARCH008` covers this at compile time.
- Nested paths like `{{ Customer.Lines }}` work — the registration walks the model type recursively at build time, so `[ExcelsiorTable]` can sit on any reachable collection property.
- Currency and date formatting in the rendered table honor `Excelsior.ValueRenderer.Culture` (defaults to `CultureInfo.CurrentCulture`). Set it once in a module initializer to override the default locale.


### Html and Markdown properties

Mark a `string` property with `[Html]` or `[Markdown]` (any attribute named `HtmlAttribute` / `MarkdownAttribute`, or `[StringSyntax("html")]` / `[StringSyntax("markdown")]`) and the matching `{{ ... }}` substitution is rendered as a structurally-replaced block of Word content instead of raw text. Html runs through the `OpenXmlHtml` converter; markdown runs through the same Markdig pipeline used by the full markdown template flow.

The attributes are detected by name — Parchment does not ship them. Define them in your own project (or use `[StringSyntax("html")]` from `System.Diagnostics.CodeAnalysis`):

<!-- snippet: HtmlAttribute -->
<a id='snippet-HtmlAttribute'></a>
```cs
[AttributeUsage(AttributeTargets.Property)]
sealed class HtmlAttribute : Attribute;
```
<sup><a href='/src/Parchment.Tests/Docx/FormatAttributeTests.cs#L13-L16' title='Snippet source file'>snippet source</a> | <a href='#snippet-HtmlAttribute' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

Mark the property:

<!-- snippet: HtmlModel -->
<a id='snippet-HtmlModel'></a>
```cs
public class HtmlDoc
{
    public required string Title { get; init; }

    [Html]
    public required string Body { get; init; }
}
```
<sup><a href='/src/Parchment.Tests/Docx/FormatAttributeTests.cs#L23-L31' title='Snippet source file'>snippet source</a> | <a href='#snippet-HtmlModel' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

Drop a `{{ Body }}` substitution into the template on its own line:

![Template before render](/src/Parchment.Tests/Scenarios/html-property/input.png)

<!-- snippet: HtmlUsage -->
<a id='snippet-HtmlUsage'></a>
```cs
var templatePath = Path.Combine(ScenarioPath("html-property"), "input.docx");

var store = new TemplateStore();
store.RegisterDocxTemplate<HtmlDoc>("html-doc", templatePath);

var model = new HtmlDoc
{
    Title = "Report",
    Body = "<p>Hello <b>world</b></p><p>Second para</p>"
};

using var stream = new MemoryStream();
await store.Render("html-doc", model, stream);
```
<sup><a href='/src/Parchment.Tests/Docx/FormatAttributeTests.cs#L80-L94' title='Snippet source file'>snippet source</a> | <a href='#snippet-HtmlUsage' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

![Rendered output](/src/Parchment.Tests/Scenarios/html-property/output%23page01.verified.png)

`[Markdown]` is the same shape — mark the property with a `MarkdownAttribute`-named attribute (or `[StringSyntax("markdown")]`) and the string is parsed as markdown at render time:

<!-- snippet: MarkdownModel -->
<a id='snippet-MarkdownModel'></a>
```cs
public class MarkdownDoc
{
    public required string Title { get; init; }

    [Markdown]
    public required string Body { get; init; }
}
```
<sup><a href='/src/Parchment.Tests/Docx/FormatAttributeTests.cs#L33-L41' title='Snippet source file'>snippet source</a> | <a href='#snippet-MarkdownModel' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

![Template before render](/src/Parchment.Tests/Scenarios/markdown-property/input.png)

![Rendered output](/src/Parchment.Tests/Scenarios/markdown-property/output%23page01.verified.png)

If you prefer not to define your own attributes, `[StringSyntax]` from `System.Diagnostics.CodeAnalysis` is equivalent (case-insensitive):

<!-- snippet: StringSyntaxHtmlModel -->
<a id='snippet-StringSyntaxHtmlModel'></a>
```cs
public class StringSyntaxHtmlDoc
{
    public required string Title { get; init; }

    [StringSyntax("html")]
    public required string Body { get; init; }
}
```
<sup><a href='/src/Parchment.Tests/Docx/FormatAttributeTests.cs#L43-L51' title='Snippet source file'>snippet source</a> | <a href='#snippet-StringSyntaxHtmlModel' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

<!-- snippet: StringSyntaxMarkdownModel -->
<a id='snippet-StringSyntaxMarkdownModel'></a>
```cs
public class StringSyntaxMarkdownDoc
{
    public required string Title { get; init; }

    [StringSyntax("markdown")]
    public required string Body { get; init; }
}
```
<sup><a href='/src/Parchment.Tests/Docx/FormatAttributeTests.cs#L53-L61' title='Snippet source file'>snippet source</a> | <a href='#snippet-StringSyntaxMarkdownModel' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

Rules:

- The substitution must be a plain member-access expression — filters and arithmetic are rejected because the formatted rendering is selected by attribute rather than Fluid evaluation. Diagnostic `PARCH010`.
- Only `string` / `string?` properties are supported.
- `[Html]` + `[Markdown]` on the same property, or `[Html]` + `[StringSyntax("markdown")]` (and vice versa), is rejected at registration as a mismatch.
- Unlike `[ExcelsiorTable]`, the token does **not** have to sit alone in its paragraph. Surrounding text and sibling tokens are preserved — see [Inline-aware structural replacement](#inline-aware-structural-replacement).

#### Inline-aware structural replacement

When an `[Html]` / `[Markdown]` token shares its paragraph with other text or sibling tokens, Parchment chooses one of three modes based on what the rendered content looks like:

| Token position | Rendered shape | Result |
| --- | --- | --- |
| Solo (covers the whole paragraph) | Anything | Host paragraph is replaced by the rendered elements. The host's `pPr` is lost; rendered paragraphs/tables stand on their own. |
| Non-solo | A single paragraph (typical for inline-only HTML like `<b>x</b>`, or single-line markdown) | The produced paragraph's `pPr` is dropped; its runs are spliced into the host at the token offset. Surrounding text and the host's `pPr` are preserved. |
| Non-solo | Multiple block-level elements, or a non-paragraph block (table) | The host is split at the token offset: text before becomes its own paragraph (cloning host's `pPr`), the rendered blocks slot in between, and text after becomes another paragraph (also cloning host's `pPr`). Empty before/after halves are still emitted. |

Two non-solo block-shaped tokens in the same paragraph are rejected at render time — the splits would overlap and there is no defined way to compose them. Move one of the tokens to its own paragraph.


### Enumerable string properties

Any property assignable to `IEnumerable<string>` (`string[]`, `List<string>`, `IReadOnlyList<string>`, etc.) is auto-rendered as a Word native bullet list when referenced as a solo `{{ Property }}` substitution. **No attribute is required** — detection is purely type-driven, mirroring [Excelsior's Enumerable string properties](https://github.com/SimonCropp/Excelsior#enumerable-string-properties) feature.

Mark the property:

<!-- snippet: StringListModel -->
<a id='snippet-StringListModel'></a>
```cs
public class Person
{
    public required string Name { get; init; }
    public required IEnumerable<string> Tags { get; init; }
}
```
<sup><a href='/src/Parchment.Tests/Docx/StringListTests.cs#L12-L18' title='Snippet source file'>snippet source</a> | <a href='#snippet-StringListModel' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

Drop a `{{ Tags }}` substitution into the template on its own line:

![Template before render](/src/Parchment.Tests/Scenarios/string-list/input.png)

Register and render normally:

<!-- snippet: StringListUsage -->
<a id='snippet-StringListUsage'></a>
```cs
var templatePath = Path.Combine(ScenarioPath("string-list"), "input.docx");

var store = new TemplateStore();
store.RegisterDocxTemplate<Person>("string-list-scenario", templatePath);

var model = new Person
{
    Name = "Ada Lovelace",
    Tags = ["Author", "Mathematician", "Engineer"]
};

using var stream = new MemoryStream();
await store.Render("string-list-scenario", model, stream);
```
<sup><a href='/src/Parchment.Tests/Docx/StringListTests.cs#L91-L107' title='Snippet source file'>snippet source</a> | <a href='#snippet-StringListUsage' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

The rendered output:

![Rendered output](/src/Parchment.Tests/Scenarios/string-list/output%23page01.verified.png)

Behavior:

- The auto path is **opportunistic, not strict**: it only fires when the token sits alone in its paragraph and has no filter chain. Otherwise the substitution falls through to standard Fluid evaluation — so existing `{{ Tags | bullet_list }}` and `{{ Tags | numbered_list }}` filter usage keeps working unchanged.
- A null collection renders as empty (no error). An empty collection renders as no paragraphs.
- Properties marked `[ExcelsiorTable]` keep ownership — the Excelsior path runs first and wins for `[ExcelsiorTable] IEnumerable<string>` (though Excelsior itself rejects `string` as an element type, so this combination is rarely useful in practice).
- Loop-scoped tokens fall through. The detection map is keyed on dotted paths from the root model only, matching the `[ExcelsiorTable]` and `[Html]`/`[Markdown]` limitations. Inside `{% for c in Customers %}{{ c.Tags }}{% endfor %}`, use the explicit `bullet_list` filter.
- Numbered output: opt out of the auto path with `{{ Tags | numbered_list }}`.


## Markdown template

A markdown template is a `.md` file containing the full body of the document plus liquid tokens for substitution, looping, and conditional content. The template below combines headings, emphasis, a pipe table driven by a loop, an ordered list driven by a loop, and a blockquote chosen by an `{% if %}`:


### Sample

<!-- snippet: MarkdownTemplate -->
<a id='snippet-MarkdownTemplate'></a>
```handlebars
# {{ Report.Title }}

*Prepared by **{{ Report.Author }}** on {{ Report.Date }}*

## Summary

{{ Report.Summary }}

## Findings

| Area | Status | Owner |
| --- | --- | --- |
{% for finding in Report.Findings -%}
| {{ finding.Area }} | {{ finding.Status }} | {{ finding.Owner }} |
{% endfor %}

## Action items

{% for item in Report.Actions %}
1. **{{ item.Title }}** — {{ item.Detail }}
{% endfor %}

{% if Report.HasRisks %}
> ⚠ Outstanding risks remain. See appendix for mitigation plan.
{% else %}
> No outstanding risks.
{% endif %}
```
<sup><a href='/src/Parchment.Tests/UsageTests.cs#L60-L88' title='Snippet source file'>snippet source</a> | <a href='#snippet-MarkdownTemplate' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

The model the template binds against:

<!-- snippet: ReportModel -->
<a id='snippet-ReportModel'></a>
```cs
public class ReportContext
{
    public required Report Report { get; init; }
}

public class Report
{
    public required string Title { get; init; }
    public required string Author { get; init; }
    public required Date Date { get; init; }
    public required string Summary { get; init; }
    public required IReadOnlyList<Finding> Findings { get; init; }
    public required IReadOnlyList<ActionItem> Actions { get; init; }
    public required bool HasRisks { get; init; }
}

public class Finding
{
    public required string Area { get; init; }
    public required string Status { get; init; }
    public required string Owner { get; init; }
}

public class ActionItem
{
    public required string Title { get; init; }
    public required string Detail { get; init; }
}
```
<sup><a href='/src/ParchmentModel/Report.cs#L3-L32' title='Snippet source file'>snippet source</a> | <a href='#snippet-ReportModel' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

Render it like any other template:

<!-- snippet: Markdown -->
<a id='snippet-Markdown'></a>
```cs
using var brandDocx = DocxTemplateBuilder.Build();
var reportModel = SampleData.Report();

var store = new TemplateStore();
store.RegisterMarkdownTemplate<ReportContext>(
    "report",
    markdownSource,
    styleSource: brandDocx);

using var stream = new MemoryStream();
await store.Render("report", reportModel, stream);
```
<sup><a href='/src/Parchment.Tests/UsageTests.cs#L91-L105' title='Snippet source file'>snippet source</a> | <a href='#snippet-Markdown' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

The rendered docx (page 1):

![Markdown template output](/src/Parchment.Tests/UsageTests.Markdown%23page01.verified.png)

The optional `styleSource` is a docx whose styles, headers, footers, theme, and section properties (page size, margins, header/footer references) are inherited by the output. If omitted, a built-in blank template is used.


### Supported Markdig extensions

#### [Emphasis extras](https://github.com/xoofx/markdig/blob/main/src/Markdig.Tests/Specs/EmphasisExtraSpecs.md)

Extended emphasis syntax beyond standard bold/italic:

```markdown
~~strikethrough~~
~subscript~
^superscript^
++underline++
==highlight==
```

Standard `*italic*`, `**bold**`, and `_italic_` work as usual.

#### [Pipe tables](https://github.com/xoofx/markdig/blob/main/src/Markdig.Tests/Specs/PipeTableSpecs.md)

```markdown
| A | B |
|---|---|
| 1 | 2 |
| 3 | 4 |
```

Header cells are bold and centered. Rendered output:

![Pipe table output](/src/Parchment.Tests/Markdown/Renderers/TableRendererTests.PipeTableEmitsTableWithGridRowsAndHeaderFormatting%23page01.verified.png)

#### [Grid tables](https://github.com/xoofx/markdig/blob/main/src/Markdig.Tests/Specs/GridTableSpecs.md)

Grid tables use `+---+` borders and `+===+` to separate the header row:

```markdown
+---+---+
| A | B |
+===+===+
| 1 | 2 |
+---+---+
| 3 | 4 |
+---+---+
```

Rendered output:

![Grid table output](/src/Parchment.Tests/Markdown/Renderers/TableRendererTests.GridTableEmitsTableWithCorrectStructure%23page01.verified.png)

#### [Auto links](https://github.com/xoofx/markdig/blob/main/src/Markdig.Tests/Specs/AutoLinks.md)

Bare URLs and email addresses are automatically converted to hyperlinks with the `Hyperlink` character style:

```markdown
Visit https://example.com or email user@example.com
```

#### [List extras](https://github.com/xoofx/markdig/blob/main/src/Markdig.Tests/Specs/ListExtraSpecs.md)

Alpha and roman numeral list markers beyond standard `1.` numbering:

```markdown
a. lower alpha
b. items

A. upper alpha
B. items

i. lower roman
ii. items

I. upper roman
II. items
```

Each format produces the corresponding Word numbering definition. Rendered output (lower alpha):

![Lower alpha list output](/src/Parchment.Tests/Markdown/Renderers/ListBlockRendererTests.LowerAlphaListUsesLowerLetterFormat%23page01.verified.png)

#### [Smarty pants](https://github.com/xoofx/markdig/blob/main/src/Markdig.Tests/Specs/SmartyPantsSpecs.md)

ASCII quotes and dashes are replaced with typographic equivalents:

| Input | Output |
|---|---|
| `'text'` | \u2018text\u2019 (curly single quotes) |
| `"text"` | \u201Ctext\u201D (curly double quotes) |
| `--` | \u2013 (en-dash) |
| `---` | \u2014 (em-dash) |
| `...` | \u2026 (ellipsis) |

#### [Generic attributes](https://github.com/xoofx/markdig/blob/main/src/Markdig.Tests/Specs/GenericAttributesSpecs.md)

Attach a Word style to a heading or paragraph with `{.StyleName}` syntax. The first class attribute is used as the paragraph's `ParagraphStyleId`:

```markdown
## Section heading {.MyCustomHeading}

Some intro paragraph. {.IntroBlock}
```


### HTML comments are stripped

HTML comment blocks (`<!-- ... -->`) are dropped during rendering rather than passed through as empty paragraphs. This lets you embed snippet markers, authoring notes, or TODOs in template sources without bleeding visible whitespace into the output docx:

```markdown
# {{ Title }}

<!-- TODO: add executive summary -->
Body text follows the heading.
```

Only standalone comment *blocks* are removed; inline HTML, scripts, styles, and any other HTML constructs render normally via [OpenXmlHtml](https://github.com/SimonCropp/OpenXmlHtml).


## Registration-time validation

Whether you register by hand (`RegisterDocxTemplate<T>(...)`) or through the source generator's `RegisterWith(store)` helper, the template is fully validated against `T` at registration — before any render runs. Missing members, block tags targeting non-enumerable properties, or `[ExcelsiorTable]` tokens that break the solo-in-paragraph / plain-member-access rules throw `ParchmentRegistrationException` immediately. Register templates at app startup and any binding mismatch surfaces there, not on the first render.


## Source generator (compile-time validation)

Decorate a `partial` class with `[ParchmentTemplate]` and Parchment's source generator validates the template tokens against the model type at compile time:

```csharp
[ParchmentTemplate("Templates/invoice.docx", typeof(Invoice))]
public partial class InvoiceReport
{
}
```

The generator emits the following diagnostics:


### `PARCH001` — unknown model member

A `{{ }}` token references a property that doesn't exist on the model type.

```
// Model
public class Letter
{
    public Customer Customer { get; set; }
}

public class Customer
{
    public string Name { get; set; }
}

// Template paragraph — "Missing" does not exist on Customer
{{ Customer.Missing }}
```


### `PARCH002` — loop source is not enumerable

A `{% for %}` tag targets a property that doesn't implement `IEnumerable<T>`. Note that `string` is explicitly rejected even though it implements `IEnumerable<char>`.

```
// Model — Customer is not a collection
public class Letter
{
    public Customer Customer { get; set; }
}

// Template paragraphs
{% for item in Customer %}
...
{% endfor %}
```


### `PARCH003` — unsupported block tag

Only `for`/`endfor`/`if`/`elsif`/`else`/`endif` are supported as block tags.

```
// Template paragraph — "foobar" is not a recognised tag
{% foobar %}
```


### `PARCH004` — template file not in `<AdditionalFiles>`

The path in `[ParchmentTemplate("...", typeof(T))]` wasn't found among the project's `<AdditionalFiles>`. Add the docx to the csproj:

```xml
<ItemGroup>
  <AdditionalFiles Include="Templates\invoice.docx" />
</ItemGroup>
```


### `PARCH005` — block tag shares a paragraph

Block tags (`{% for %}`, `{% if %}`, etc.) must sit alone in their own paragraph. Mixing a block tag with other text on the same line is rejected.

```
// Template paragraphs — "prefix" is on the same line as the for tag
prefix {% for line in Lines %}
{{ line.Description }}
{% endfor %}
```


### `PARCH006` — template file unreadable

The docx at the template path exists in `<AdditionalFiles>` but couldn't be opened — typically a corrupt or truncated file.


### `PARCH007` — `[ExcelsiorTable]` token not alone in paragraph

An `[ExcelsiorTable]` substitution replaces the entire host paragraph with a Word table. If the paragraph contains other text, that text would be discarded. The token must be the only content in its paragraph.

```
// Model
public class Invoice
{
    [ExcelsiorTable]
    public List<Line> Lines { get; set; }
}

// Template paragraph — "Prefix" shares the paragraph with {{ Lines }}
Prefix {{ Lines }}
```


### `PARCH008` — `[ExcelsiorTable]` token with filters or complex expression

The Excelsior render path walks the CLR model directly and bypasses Fluid evaluation, so filters and complex expressions would be silently ignored. Only plain member-access (`{{ Lines }}` or `{{ Customer.Lines }}`) is allowed.

```
// Template paragraph — the | reverse filter would be silently dropped
{{ Lines | reverse }}
```


### `PARCH009` — retired

Previously emitted when an `[Html]` / `[Markdown]` token shared its paragraph with other content. The runtime now splices inline content in place and splits the host paragraph for block-level content, so non-solo tokens are valid. The id is intentionally not reused.


### `PARCH010` — `[Html]` / `[Markdown]` token with filters or complex expression

Formatted rendering is selected by the property attribute rather than via Fluid, so filter chains are not applied. Use plain member access (`{{ Body }}` or `{{ Customer.Bio }}`).

It also generates a `RegisterWith(store)` helper so registration is one line at runtime.

Add the docx as an additional file:

```xml
<ItemGroup>
  <AdditionalFiles Include="Templates\invoice.docx" />
</ItemGroup>
```


## Benchmarks

``` ini
BenchmarkDotNet v0.14.0, Windows 11 (10.0.26200.8246)
AMD Ryzen 9 5900X, 1 CPU, 24 logical and 12 physical cores
.NET SDK 11.0.100-preview.2.26159.112
  [Host] : .NET 10.0.6 (10.0.626.17701), X64 RyuJIT AVX2
```

### Registration

| Method | Mean | Allocated |
|---|---|---|
| RegisterFromMemoryStream | 0.22 ms | 181 KB |
| RegisterFromBufferedStream | 0.22 ms | 180 KB |
| RegisterFromFilePath | 1.27 ms | 180 KB |

### Rendering

ItemCount varies the number of loop iterations (line items for docx, findings/actions for markdown).

| Method | ItemCount | Mean | Allocated |
|---|---|---|---|
| DocxTemplate | 3 | 0.22 ms | 194 KB |
| MarkdownTemplate | 3 | 0.50 ms | 371 KB |
| DocxTemplate | 50 | 0.54 ms | 467 KB |
| MarkdownTemplate | 50 | 1.09 ms | 780 KB |
| DocxTemplate | 500 | 10.36 ms | 3,132 KB |
| MarkdownTemplate | 500 | 6.65 ms | 4,633 KB |

Run benchmarks with:

```
dotnet run --project src/Parchment.Benchmarks --configuration Release
```


## Determinism

Rendering the same template with the same model produces a byte-identical output. Useful for hash-based caching, dedup, and legal sign-off workflows.


## Icon

[Parchment](https://thenounproject.com/icon/parchment-6992235/) icon designed by Alum Design from [The Noun Project](https://thenounproject.com).
