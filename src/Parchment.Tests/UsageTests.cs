namespace Parchment.Tests;

public class UsageTests
{
    [Test]
    public async Task Substitution()
    {
        // begin-snippet: Substitution
        var template = Fixtures.DocxTemplateBuilder.Build(
            "Invoice {{ Number }}",
            "Customer: {{ Customer.Name }}",
            "Total: {{ Total }} {{ Currency }}");

        var store = new TemplateStore();
        store.RegisterDocxTemplate<Invoice>("substitution", template);

        var bytes = await store.Render("substitution", SampleData.Invoice());
        // end-snippet
        await Verify(bytes, "docx");
    }

    [Test]
    public async Task Markdown()
    {
        // begin-snippet: MarkdownTemplate
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
        // end-snippet

        // begin-snippet: Markdown
        var brandDocxBytes = Fixtures.DocxTemplateBuilder.Build();
        var reportModel = SampleData.Report();

        var store = new TemplateStore();
        store.RegisterMarkdownTemplate<ReportContext>(
            "report",
            markdownSource,
            styleSource: brandDocxBytes);
        var bytes = await store.Render("report", reportModel);
        // end-snippet
        await Verify(bytes, "docx");
    }
}
