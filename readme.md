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

<!-- snippet: Substitution -->
<a id='snippet-Substitution'></a>
```cs
var template = Fixtures.DocxTemplateBuilder.Build(
    "Invoice {{ Number }}",
    "Customer: {{ Customer.Name }}",
    "Total: {{ Total }} {{ Currency }}");

var store = new TemplateStore();
store.RegisterDocxTemplate<Invoice>("substitution", template);

var bytes = await store.Render("substitution", SampleData.Invoice());
```
<sup><a href='/src/Parchment.Tests/UsageTests.cs#L8-L18' title='Snippet source file'>snippet source</a> | <a href='#snippet-Substitution' title='Start of snippet'>anchor</a></sup>
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


## Markdown template

A markdown template is a `.md` file containing the full body of the document plus liquid tokens for substitution, looping, and conditional content. The template below combines headings, emphasis, a pipe table driven by a loop, an ordered list driven by a loop, and a blockquote chosen by an `{% if %}`:


### Sample

<!-- snippet: MarkdownTemplate -->
<a id='snippet-MarkdownTemplate'></a>
```cs
var markdownSource = """
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
    """;
```
<sup><a href='/src/Parchment.Tests/UsageTests.cs#L25-L55' title='Snippet source file'>snippet source</a> | <a href='#snippet-MarkdownTemplate' title='Start of snippet'>anchor</a></sup>
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
var brandDocxBytes = Fixtures.DocxTemplateBuilder.Build();
var reportModel = SampleData.Report();

var store = new TemplateStore();
store.RegisterMarkdownTemplate<ReportContext>(
    "report",
    markdownSource,
    styleSource: brandDocxBytes);
var bytes = await store.Render("report", reportModel);
```
<sup><a href='/src/Parchment.Tests/UsageTests.cs#L57-L67' title='Snippet source file'>snippet source</a> | <a href='#snippet-Markdown' title='Start of snippet'>anchor</a></sup>
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
