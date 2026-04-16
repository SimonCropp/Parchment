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
        using var stream = new MemoryStream();
        var template = DocxTemplateBuilder.Build(
            """
            // begin-snippet: MarkdownPropertyContent
            # {{ Title }}

            {{ Body }}
            // end-snippet
            """);

        #region MarkdownPropertyRender
        var store = new TemplateStore();
        store.RegisterDocxTemplate<NoteModel>("markdown-hatch", template);
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
        using var stream = new MemoryStream();
        var template = DocxTemplateBuilder.Build(
            """
            // begin-snippet: MarkdownFilterContent
            # {{ Heading }}

            {{ Content | markdown }}
            // end-snippet
            """);

        #region MarkdownFilterRender
        var store = new TemplateStore();
        store.RegisterDocxTemplate<ArticleModel>("markdown-filter", template);
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
        using var stream = new MemoryStream();
        var template = DocxTemplateBuilder.Build(
            """
            // begin-snippet: MutateContent
            {{ Label }}

            {{ Highlight }}
            // end-snippet
            """);

        #region MutateRender
        var store = new TemplateStore();
        store.RegisterDocxTemplate<StyledModel>("mutate", template);
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
