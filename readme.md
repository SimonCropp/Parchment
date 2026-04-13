# <img src="/src/icon.png" height="30px"> Parchment

[![Build status](https://img.shields.io/appveyor/build/SimonCropp/Parchment)](https://ci.appveyor.com/project/SimonCropp/Parchment)
[![NuGet Status](https://img.shields.io/nuget/v/Parchment.svg?label=Parchment)](https://www.nuget.org/packages/Parchment/)

Parchment is a Word (.docx) document generation library with two complementary rendering modes. It combines a .NET data model with either a docx template (token replacement, loops, conditionals) or a markdown template (full content rendering), both driven by [liquid](https://shopify.github.io/liquid/) via [Fluid](https://github.com/sebastienros/fluid). Markdown is parsed with [Markdig](https://github.com/xoofx/markdig); HTML chunks are converted via [OpenXmlHtml](https://github.com/SimonCropp/OpenXmlHtml).

**See [Milestones](../../milestones?state=closed) for release notes.**


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
<sup><a href='/src/Parchment.Tests/UsageTests.cs#L5-L18' title='Snippet source file'>snippet source</a> | <a href='#snippet-Substitution' title='Start of snippet'>anchor</a></sup>
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

```cs
var store = new TemplateStore();
store.RegisterMarkdownTemplate<ReportModel>("report", markdownSource, styleSource: brandDocxBytes);
var bytes = await store.Render("report", reportModel);
```

The optional `styleSource` is a docx whose styles, headers, footers, theme, and section properties (page size, margins, header/footer references) are inherited by the output. If omitted, a built-in blank template is used.

Supported Markdig extensions:

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

The generator emits diagnostics if a token references a missing model member (`PARCH001`), if a loop source isn't enumerable (`PARCH002`), or if the template path isn't in `<AdditionalFiles>`. It also generates a `RegisterWith(store)` helper so registration is one line at runtime.

Add the docx as an additional file:

```xml
<ItemGroup>
  <AdditionalFiles Include="Templates\invoice.docx" />
</ItemGroup>
```


## Determinism

Rendering the same template with the same model produces a byte-identical output. Useful for hash-based caching, dedup, and legal sign-off workflows.


## NuGet package

[Parchment](https://www.nuget.org/packages/Parchment/)


## Icon

[Parchment](https://thenounproject.com/icon/parchment-6992235/) icon designed by Alum Design from [The Noun Project](https://thenounproject.com).
