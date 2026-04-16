using System.Linq;
using BenchmarkDotNet.Attributes;

[Config(typeof(BenchmarkConfig))]
public class RenderBenchmarks
{
    TemplateStore docxStore = null!;
    TemplateStore markdownStore = null!;
    Invoice docxModel = null!;
    ReportContext markdownModel = null!;

    [Params(3, 50, 500)]
    public int ItemCount { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        docxModel = BuildInvoice(ItemCount);
        markdownModel = BuildReport(ItemCount);

        docxStore = new TemplateStore();
        using var docxTemplate = new MemoryStream(BuildDocxTemplate());
        docxStore.RegisterDocxTemplate<Invoice>("docx", docxTemplate);

        markdownStore = new TemplateStore();
        using var styleSource = new MemoryStream(BuildStyleSource());
        markdownStore.RegisterMarkdownTemplate<ReportContext>("md", MarkdownSource, styleSource);
    }

    [Benchmark]
    public async Task DocxTemplate()
    {
        using var output = new MemoryStream();
        await docxStore.Render("docx", docxModel, output);
    }

    [Benchmark]
    public async Task MarkdownTemplate()
    {
        using var output = new MemoryStream();
        await markdownStore.Render("md", markdownModel, output);
    }

    static Invoice BuildInvoice(int lineCount) =>
        new()
        {
            Number = "INV-2026-0042",
            IssueDate = new(2026, 4, 1),
            DueDate = new(2026, 4, 30),
            Currency = "USD",
            Notes = "Thanks for your business.",
            Tags = ["priority", "net-30"],
            Customer = new()
            {
                Name = "Globex Corporation",
                Email = "ap@globex.test",
                Address = "123 Commerce Way, Springfield, IL 62704",
                IsPreferred = true
            },
            Lines = Enumerable.Range(1, lineCount)
                .Select(i => new LineItem
                {
                    Description = $"Service line item {i}",
                    Quantity = i % 10 + 1,
                    UnitPrice = 100m + i
                })
                .ToList()
        };

    static ReportContext BuildReport(int findingCount) =>
        new()
        {
            Report = new()
            {
                Title = "Quarterly Review",
                Author = "Alex Chen",
                Date = new(2026, 7, 1),
                Summary = "Platform reliability trended upward this quarter.",
                Findings = Enumerable.Range(1, findingCount)
                    .Select(i => new Finding
                    {
                        Area = $"Area-{i}",
                        Status = i % 3 == 0 ? "Watch" : "Improved",
                        Owner = $"Team-{i % 5}"
                    })
                    .ToList(),
                Actions = Enumerable.Range(1, Math.Max(1, findingCount / 5))
                    .Select(i => new ActionItem
                    {
                        Title = $"Action item {i}",
                        Detail = $"Details for action {i}."
                    })
                    .ToList(),
                HasRisks = findingCount > 10
            }
        };

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

        {% if Report.HasRisks %}
        > Outstanding risks remain. See appendix for mitigation plan.
        {% else %}
        > No outstanding risks.
        {% endif %}
        """;

    static byte[] BuildDocxTemplate()
    {
        using var stream = new MemoryStream();
        using (var doc = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document))
        {
            var mainPart = doc.AddMainDocumentPart();
            var body = new Body(
                Para("Invoice {{ Number }}"),
                Para("Customer: {{ Customer.Name }}"),
                Para("{% if Customer.IsPreferred %}"),
                Para("Preferred customer discount applied."),
                Para("{% endif %}"),
                Para("{% for line in Lines %}"),
                Para("{{ line.Description }}: {{ line.Quantity }} x {{ line.UnitPrice }}"),
                Para("{% endfor %}"),
                Para("Total: {{ Total }} {{ Currency }}"));
            mainPart.Document = new Document(body);

            var stylesPart = mainPart.AddNewPart<StyleDefinitionsPart>();
            var styles = new Styles();
            styles.Append(new Style { Type = StyleValues.Paragraph, StyleId = "Normal", Default = true }
                .AppendChild(new StyleName { Val = "Normal" }).Parent!);
            stylesPart.Styles = styles;
        }

        return stream.ToArray();
    }

    static Paragraph Para(string text) =>
        new(new Run(new Text(text) { Space = SpaceProcessingModeValues.Preserve }));

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
