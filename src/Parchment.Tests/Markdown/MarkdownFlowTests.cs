namespace Parchment.Tests.Markdown;

public class MarkdownFlowTests
{
    public class ReportModel
    {
        public required string Title { get; init; }
        public required string Author { get; init; }
        public required IReadOnlyList<string> Findings { get; init; }
    }

    [Test]
    public async Task BasicMarkdown()
    {
        var markdown = """
            # {{ Title }}

            by *{{ Author }}*

            ## Key findings

            {% for finding in Findings %}
            - {{ finding }}
            {% endfor %}

            > Review complete.
            """;

        var styleSource = Fixtures.DocxTemplateBuilder.Build();

        var store = new TemplateStore();
        store.RegisterMarkdownTemplate<ReportModel>("report", markdown, styleSource);

        using var stream = new MemoryStream();
        await store.Render("report", new ReportModel
        {
            Title = "Q2 Engineering Review",
            Author = "Alex Chen",
            Findings =
            [
                "Build times improved 40%",
                "Test flake rate halved",
                "Three new services in production"
            ]
        }, stream);
        stream.Position = 0;
        await Verify(stream, "docx");
    }
}
