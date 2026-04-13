# Parchment

Parchment is a Word (.docx) generation library that combines a .NET data model with either a docx template (token replacement) or a markdown template (full content rendering), driven by [liquid](https://shopify.github.io/liquid/) via [Fluid](https://github.com/sebastienros/fluid), [Markdig](https://github.com/xoofx/markdig), and [OpenXmlHtml](https://github.com/SimonCropp/OpenXmlHtml).

## Docx template flow

```cs
var store = new TemplateStore();
store.RegisterDocxTemplate<Invoice>("invoice", File.ReadAllBytes("invoice-template.docx"));
var bytes = await store.Render("invoice", SampleData.Invoice());
File.WriteAllBytes("out.docx", bytes);
```

The template may include:

- Substitution tokens: `{{ customer.name }}`
- Paragraph-scope loops: `{% for line in lines %}` … `{% endfor %}`
- Table-row-scope loops: put `{% for line in lines %}` on its own in one row and `{% endfor %}` on its own in another
- Conditionals: `{% if customer.is_preferred %}` … `{% endif %}`

## Markdown template flow

```cs
store.RegisterMarkdownTemplate<Report>("report", markdownSource, styleSource: brandDocxBytes);
var bytes = await store.Render("report", reportModel);
```

## Source generator

Decorate a partial class with `[ParchmentTemplate]` and Parchment's source generator validates the template tokens against the model type at compile time.

```cs
[ParchmentTemplate("Templates/invoice.docx", typeof(Invoice))]
public partial class InvoiceReport
{
}
```

See the [readme](https://github.com/SimonCropp/Parchment#readme) for full documentation.
