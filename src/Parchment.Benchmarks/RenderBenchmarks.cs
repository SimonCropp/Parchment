using BenchmarkDotNet.Attributes;

[Config(typeof(BenchmarkConfig))]
public class RenderBenchmarks
{
    TemplateStore docxStore = null!;
    TemplateStore markdownStore = null!;
    Invoice invoice = null!;
    ReportContext report = null!;

    [GlobalSetup]
    public void Setup()
    {
        invoice = SampleData.Invoice();
        report = SampleData.Report();

        docxStore = new TemplateStore();
        using var docxTemplate = new MemoryStream(BuildDocxTemplate());
        docxStore.RegisterDocxTemplate<Invoice>("docx", docxTemplate);

        markdownStore = new TemplateStore();
        using var styleSource = new MemoryStream(BuildStyleSource());
        markdownStore.RegisterMarkdownTemplate<ReportContext>("md", MarkdownSource, styleSource);
    }

    [Benchmark]
    public async Task RenderDocxTemplate()
    {
        using var output = new MemoryStream();
        await docxStore.Render("docx", invoice, output);
    }

    [Benchmark]
    public async Task RenderMarkdownTemplate()
    {
        using var output = new MemoryStream();
        await markdownStore.Render("md", report, output);
    }

    const string MarkdownSource = """
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
        """;

    static byte[] BuildDocxTemplate()
    {
        using var stream = new MemoryStream();
        using (var doc = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document))
        {
            var mainPart = doc.AddMainDocumentPart();
            var body = new Body(
                new Paragraph(new Run(new Text("Invoice {{ Number }}") { Space = SpaceProcessingModeValues.Preserve })),
                new Paragraph(new Run(new Text("Customer: {{ Customer.Name }}") { Space = SpaceProcessingModeValues.Preserve })),
                new Paragraph(new Run(new Text("{% for line in Lines %}") { Space = SpaceProcessingModeValues.Preserve })),
                new Paragraph(new Run(new Text("{{ line.Description }}: {{ line.Quantity }} x {{ line.UnitPrice }}") { Space = SpaceProcessingModeValues.Preserve })),
                new Paragraph(new Run(new Text("{% endfor %}") { Space = SpaceProcessingModeValues.Preserve })),
                new Paragraph(new Run(new Text("Total: {{ Total }} {{ Currency }}") { Space = SpaceProcessingModeValues.Preserve })));
            mainPart.Document = new Document(body);

            var stylesPart = mainPart.AddNewPart<StyleDefinitionsPart>();
            var styles = new Styles();
            styles.Append(new Style { Type = StyleValues.Paragraph, StyleId = "Normal", Default = true }
                .AppendChild(new StyleName { Val = "Normal" }).Parent!);
            stylesPart.Styles = styles;
        }

        return stream.ToArray();
    }

    static byte[] BuildStyleSource()
    {
        using var stream = new MemoryStream();
        using (var doc = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document))
        {
            var mainPart = doc.AddMainDocumentPart();
            mainPart.Document = new Document(new Body(new Paragraph()));

            var stylesPart = mainPart.AddNewPart<StyleDefinitionsPart>();
            var styles = new Styles();
            styles.Append(new Style { Type = StyleValues.Paragraph, StyleId = "Normal", Default = true }
                .AppendChild(new StyleName { Val = "Normal" }).Parent!);
            for (var i = 1; i <= 6; i++)
            {
                styles.Append(new Style { Type = StyleValues.Paragraph, StyleId = $"Heading{i}" }
                    .AppendChild(new StyleName { Val = $"Heading{i}" }).Parent!);
            }

            styles.Append(new Style { Type = StyleValues.Paragraph, StyleId = "ListParagraph" }
                .AppendChild(new StyleName { Val = "List Paragraph" }).Parent!);
            styles.Append(new Style { Type = StyleValues.Paragraph, StyleId = "Quote" }
                .AppendChild(new StyleName { Val = "Quote" }).Parent!);
            stylesPart.Styles = styles;
        }

        return stream.ToArray();
    }
}
