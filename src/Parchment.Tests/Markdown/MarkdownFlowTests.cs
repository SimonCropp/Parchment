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
        var markdown =
            """
            # {{ Title }}

            by *{{ Author }}*

            ## Key findings

            {% for finding in Findings %}
            - {{ finding }}
            {% endfor %}

            > Review complete.
            """;

        using var styleSource = DocxTemplateBuilder.Build();

        var store = new TemplateStore();
        store.RegisterMarkdownTemplate<ReportModel>("report", markdown, styleSource);

        using var stream = new MemoryStream();
        await store.Render(
            "report",
            new ReportModel
            {
                Title = "Q2 Engineering Review",
                Author = "Alex Chen",
                Findings =
                [
                    "Build times improved 40%",
                    "Test flake rate halved",
                    "Three new services in production"
                ]
            },
            stream);
        stream.Position = 0;
        await Verify(stream, "docx");
    }

    public class TitleModel
    {
        public required string Title { get; init; }
    }

    #region MarkdownTemplatePropertyModel

    public class BriefModel
    {
        public required string Title { get; init; }
        public required string Details { get; init; }
    }

    #endregion

    [Test]
    public async Task PropertyContainingMarkdown()
    {
        using var targetStream = new MemoryStream();
        var markdown =
            """
            <!-- begin-snippet: MarkdownTemplatePropertyContent(lang=handlebars) -->
            # {{ Title }}

            {{ Details }}
            <!-- end-snippet -->
            """;

        using var styleSource = DocxTemplateBuilder.Build();

        #region MarkdownTemplatePropertyUsage

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

        #endregion

        targetStream.Position = 0;
        await Verify(targetStream, "docx");
    }

    [Test]
    public async Task HtmlCommentsAreStripped()
    {
        // HTML comment blocks (snippet markers, authoring notes, TODOs) must not bleed into the
        // rendered docx as blank paragraphs. Two markdowns that differ only by surrounding
        // comment lines should produce byte-identical output.
        var withComments =
            """
            <!-- begin-snippet: report(lang=handlebars) -->
            # {{ Title }}

            <!-- TODO: add executive summary -->
            Body text follows the heading.
            <!-- end-snippet -->
            """;

        var withoutComments =
            """
            # {{ Title }}

            Body text follows the heading.
            """;

        using var styleSource = DocxTemplateBuilder.Build();
        var store = new TemplateStore();
        store.RegisterMarkdownTemplate<TitleModel>("with-comments", withComments, styleSource);
        styleSource.Position = 0;
        store.RegisterMarkdownTemplate<TitleModel>("without-comments", withoutComments, styleSource);

        var model = new TitleModel {Title = "Sample"};

        using var withStream = new MemoryStream();
        await store.Render("with-comments", model, withStream);

        using var withoutStream = new MemoryStream();
        await store.Render("without-comments", model, withoutStream);

        await Assert.That(withStream.ToArray()).IsEquivalentTo(withoutStream.ToArray());
    }
}
