public class UsageTests
{
    [Test]
    public async Task Substitution()
    {
        // begin-snippet: Substitution
        var template = DocxTemplateBuilder.Build(
            "Invoice {{ Number }}",
            "Customer: {{ Customer.Name }}",
            "Total: {{ Total }} {{ Currency }}");

        var store = new TemplateStore();
        store.RegisterDocxTemplate<Invoice>("substitution", template);

        using var stream = new MemoryStream();
        await store.Render("substitution", SampleData.Invoice(), stream);
        // end-snippet
        stream.Position = 0;
        await Verify(stream, "docx");
    }

    [Test]
    public async Task Markdown()
    {
        var markdownSource = """
            <!-- begin-snippet: MarkdownTemplate(lang=handlebars) -->
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
            <!-- end-snippet -->
            """;

        // begin-snippet: Markdown
        var brandDocxBytes = DocxTemplateBuilder.Build();
        var reportModel = SampleData.Report();

        var store = new TemplateStore();
        store.RegisterMarkdownTemplate<ReportContext>(
            "report",
            markdownSource,
            styleSource: brandDocxBytes);

        using var stream = new MemoryStream();
        await store.Render("report", reportModel, stream);
        // end-snippet
        stream.Position = 0;
        await Verify(stream, "docx");
    }
}
