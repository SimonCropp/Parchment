namespace Parchment.Tests.Docx;

public class TokenOverrideTests
{
    public class NoteModel
    {
        public required string Title { get; init; }
        public required TokenValue Body { get; init; }
    }

    [Test]
    public async Task MarkdownHatch()
    {
        var template = DocxTemplateBuilder.Build(
            "# {{ Title }}",
            "{{ Body }}");

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
        stream.Position = 0;
        await Verify(stream, "docx");
    }

    [Test]
    public async Task BulletListFilter()
    {
        var template = DocxTemplateBuilder.Build(
            "Tags:",
            "{{ Tags | bullet_list }}");

        var store = new TemplateStore();
        store.RegisterDocxTemplate<Invoice>("bullet-filter", template);
        using var stream = new MemoryStream();
        await store.Render("bullet-filter", SampleData.Invoice(), stream);
        stream.Position = 0;
        await Verify(stream, "docx");
    }
}
