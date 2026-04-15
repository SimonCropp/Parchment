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
<a id='snippet-SubstitutionInput-1'></a>
```txt
Invoice INV-2026-0042
Customer: Globex Corporation
Total: 10978.0 USD
```
<sup><a href='/src/Parchment.Tests/UsageTests.Substitution%2300.verified.txt#L3-L7' title='Snippet source file'>snippet source</a> | <a href='#snippet-SubstitutionInput-1' title='Start of snippet'>anchor</a></sup>
<a id='snippet-SubstitutionInput-2'></a>
```txt
Invoice INV-2026-0042
Customer: Globex Corporation
Total: 10978.0 USD
```
<sup><a href='/src/Parchment.Tests/UsageTests.Substitution%2301.verified.txt#L1-L5' title='Snippet source file'>snippet source</a> | <a href='#snippet-SubstitutionInput-2' title='Start of snippet'>anchor</a></sup>
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
<sup><a href='/src/Parchment.Tests/UsageTests.cs#L17-L23' title='Snippet source file'>snippet source</a> | <a href='#snippet-Substitution' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->


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


### Token override hatches

A token can resolve to one of three values:

- `TokenValue.Text(string)` — plain string substitution (the default for raw model values).
- `TokenValue.Markdown(string)` — the value is rendered as markdown via Markdig and spliced into the host paragraph.
- `TokenValue.OpenXml(Func<IOpenXmlContext, IEnumerable<OpenXmlElement>>)` — the value is a function that emits raw OpenXML elements. Useful for rich tables, generated charts, custom-styled lists.

You can also use the bundled `bullet_list` and `numbered_list` filters to render an `IEnumerable<string>` as a real Word list.


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
<sup><a href='/src/Parchment.Tests/Docx/ExcelsiorTableTests.cs#L14-L34' title='Snippet source file'>snippet source</a> | <a href='#snippet-ExcelsiorTableModel' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

Drop a `{{ Lines }}` substitution into the template on its own line. The template:

![Template before render](/src/Parchment.Tests/Scenarios/excelsior-table/input.png)

Register and render normally:

<!-- snippet: ExcelsiorTableUsage -->
<a id='snippet-ExcelsiorTableUsage'></a>
```cs
var templateBytes = await File.ReadAllBytesAsync(
    Path.Combine(ScenarioPath("excelsior-table"), "input.docx"));

var store = new TemplateStore();
store.RegisterDocxTemplate<Quote>("excelsior-quote", templateBytes);

var model = new Quote
{
    Reference = "Q-2026-0042",
    Lines =
    [
        new() { Description = "Strategy workshop", Quantity = 2, UnitPrice = 4500m },
        new() { Description = "Implementation support", Quantity = 8, UnitPrice = 1750m },
        new() { Description = "Documentation review", Quantity = 1, UnitPrice = 950m }
    ]
};

using var stream = new MemoryStream();
await store.Render("excelsior-quote", model, stream);
```
<sup><a href='/src/Parchment.Tests/Docx/ExcelsiorTableTests.cs#L144-L164' title='Snippet source file'>snippet source</a> | <a href='#snippet-ExcelsiorTableUsage' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

The rendered output:

![Rendered output](/src/Parchment.Tests/Scenarios/excelsior-table/output%23page01.verified.png)

Rules:

- The substitution must sit alone in its own paragraph — structural table replacement swaps the entire host paragraph, so surrounding text would be discarded. The runtime throws at registration (`ParchmentRegistrationException`) and the source generator emits `PARCH007` if this is violated.
- The substitution must be a plain member-access expression — filters (`{{ Lines | reverse }}`) and arithmetic are rejected because the Excelsior path walks the model object directly and bypasses Fluid evaluation. Diagnostic `PARCH008` covers this at compile time.
- Nested paths like `{{ Customer.Lines }}` work — the registration walks the model type recursively at build time, so `[ExcelsiorTable]` can sit on any reachable collection property.
- Currency and date formatting in the rendered table honor `Excelsior.ValueRenderer.Culture` (defaults to `CultureInfo.CurrentCulture`). Set it once in a module initializer to override the default locale.


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
<sup><a href='/src/Parchment.Tests/UsageTests.cs#L32-L60' title='Snippet source file'>snippet source</a> | <a href='#snippet-MarkdownTemplate' title='Start of snippet'>anchor</a></sup>
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
var brandDocxBytes = DocxTemplateBuilder.Build();
var reportModel = SampleData.Report();

var store = new TemplateStore();
store.RegisterMarkdownTemplate<ReportContext>(
    "report",
    markdownSource,
    styleSource: brandDocxBytes);

using var stream = new MemoryStream();
await store.Render("report", reportModel, stream);
```
<sup><a href='/src/Parchment.Tests/UsageTests.cs#L63-L77' title='Snippet source file'>snippet source</a> | <a href='#snippet-Markdown' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

The rendered docx (page 1):

![Markdown template output](/src/Parchment.Tests/UsageTests.Markdown%23page01.verified.png)

The optional `styleSource` is a docx whose styles, headers, footers, theme, and section properties (page size, margins, header/footer references) are inherited by the output. If omitted, a built-in blank template is used.

### Supported Markdig extensions:

- [Emphasis extras](https://github.com/xoofx/markdig/blob/main/src/Markdig.Tests/Specs/EmphasisExtraSpecs.md): `~~strike~~`, `~sub~`, `^sup^`, `++ins++`, `==mark==`
- [Grid tables](https://github.com/xoofx/markdig/blob/main/src/Markdig.Tests/Specs/GridTableSpecs.md)
- [Auto links](https://github.com/xoofx/markdig/blob/main/src/Markdig.Tests/Specs/AutoLinks.md)
- [List extras](https://github.com/xoofx/markdig/blob/main/src/Markdig.Tests/Specs/ListExtraSpecs.md): alpha and roman lists
- [Smarty pants](https://github.com/xoofx/markdig/blob/main/src/Markdig.Tests/Specs/SmartyPantsSpecs.md): curly quotes, em-dashes
- [Generic attributes](https://github.com/xoofx/markdig/blob/main/src/Markdig.Tests/Specs/GenericAttributesSpecs.md): attach a Word style with `{.StyleName}` syntax

Example with style attribute:

```markdown
## Section heading {.MyCustomHeading}

Some intro paragraph. {.IntroBlock}
```

### HTML comments are stripped

HTML comment blocks (`<!-- ... -->`) are dropped during rendering rather than passed through as empty paragraphs. This lets you embed snippet markers, authoring notes, or TODOs in template sources without bleeding visible whitespace into the output docx:

```markdown
<!-- begin-snippet: report -->
# {{ Title }}

<!-- TODO: add executive summary -->
Body text follows the heading.
<!-- end-snippet -->
```

Only standalone comment *blocks* are removed; inline HTML, scripts, styles, and any other HTML constructs render normally via [OpenXmlHtml](https://github.com/SimonCropp/OpenXmlHtml).


## Source generator (compile-time validation)

Decorate a `partial` class with `[ParchmentTemplate]` and Parchment's source generator validates the template tokens against the model type at compile time:

```csharp
[ParchmentTemplate("Templates/invoice.docx", typeof(Invoice))]
public partial class InvoiceReport
{
}
```

The generator emits the following diagnostics:

- `PARCH001` — token references a member that doesn't exist on the model
- `PARCH002` — loop source doesn't resolve to an `IEnumerable<T>`
- `PARCH003` — unsupported block tag (only `for`/`endfor`/`if`/`elsif`/`else`/`endif` are supported)
- `PARCH004` — template path isn't listed in `<AdditionalFiles>`
- `PARCH005` — block tag shares a paragraph with other content (block tags must sit on their own line)
- `PARCH006` — template file couldn't be read
- `PARCH007` — `[ExcelsiorTable]` token shares a paragraph with other content (structural table replacement swaps the whole paragraph)
- `PARCH008` — `[ExcelsiorTable]` token has filters or a complex expression (Excelsior dispatch bypasses Fluid)

It also generates a `RegisterWith(store)` helper so registration is one line at runtime.

Add the docx as an additional file:

```xml
<ItemGroup>
  <AdditionalFiles Include="Templates\invoice.docx" />
</ItemGroup>
```


## Determinism

Rendering the same template with the same model produces a byte-identical output. Useful for hash-based caching, dedup, and legal sign-off workflows.


## Icon

[Parchment](https://thenounproject.com/icon/parchment-6992235/) icon designed by Alum Design from [The Noun Project](https://thenounproject.com).
