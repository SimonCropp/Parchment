public class TokenOverrideTests
{
    #region MarkdownPropertyModel
    public class NoteModel
    {
        public required string Title { get; init; }
        public required TokenValue Body { get; init; }
    }
    #endregion

    [Test]
    public async Task MarkdownProperty()
    {
        #region MarkdownPropertyUsage
        var template = DocxTemplateBuilder.Build(
            """
            # {{ Title }}

            {{ Body }}
            """);

        var store = new TemplateStore();
        store.RegisterDocxTemplate<NoteModel>("markdown-hatch", template);
        using var stream = new MemoryStream();
        await store.Render("markdown-hatch", new NoteModel
        {
            Title = "Weekly summary",
            Body = TokenValue.Markdown(
                """
                ## Highlights

                - Shipped the **new feature**
                - Closed _several_ bugs
                - Ran a code review

                > Stay the course
                """)
        }, stream);
        #endregion
        stream.Position = 0;
        await Verify(stream, "docx");
    }

    #region MarkdownFilterModel
    public class ArticleModel
    {
        public required string Heading { get; init; }
        public required string Content { get; init; }
    }
    #endregion

    [Test]
    public async Task MarkdownFilter()
    {
        #region MarkdownFilterUsage
        var template = DocxTemplateBuilder.Build(
            """
            # {{ Heading }}

            {{ Content | markdown }}
            """);

        var store = new TemplateStore();
        store.RegisterDocxTemplate<ArticleModel>("markdown-filter", template);
        using var stream = new MemoryStream();
        await store.Render("markdown-filter", new ArticleModel
        {
            Heading = "Release notes",
            Content = """
                ### Bug fixes

                - Fixed crash on **empty input**
                - Resolved _timeout_ in batch mode
                """
        }, stream);
        #endregion
        stream.Position = 0;
        await Verify(stream, "docx");
    }

    #region MutateModel
    public class StyledModel
    {
        public required string Label { get; init; }
        public required TokenValue Highlight { get; init; }
    }
    #endregion

    [Test]
    public async Task MutateParagraph()
    {
        #region MutateUsage
        var template = DocxTemplateBuilder.Build(
            """
            {{ Label }}

            {{ Highlight }}
            """);

        var store = new TemplateStore();
        store.RegisterDocxTemplate<StyledModel>("mutate", template);
        using var stream = new MemoryStream();
        await store.Render("mutate", new StyledModel
        {
            Label = "Before",
            Highlight = TokenValue.Mutate((paragraph, context) =>
            {
                paragraph.Append(
                    new Run(
                        new RunProperties(
                            new Bold()),
                        new Text("Custom content")
                        {
                            Space = SpaceProcessingModeValues.Preserve
                        }));
            })
        }, stream);
        #endregion
        stream.Position = 0;
        await Verify(stream, "docx");
    }

    [Test]
    public async Task BulletListFilter()
    {
        var template = DocxTemplateBuilder.Build(
            """
            Tags:

            {{ Tags | bullet_list }}
            """);

        var store = new TemplateStore();
        store.RegisterDocxTemplate<Invoice>("bullet-filter", template);
        using var stream = new MemoryStream();
        await store.Render("bullet-filter", SampleData.Invoice(), stream);
        stream.Position = 0;
        await Verify(stream, "docx");
    }
}
