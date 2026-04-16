public class ConditionalTests
{
    [Test]
    public async Task IfTrue()
    {
        using var template = DocxTemplateBuilder.Build(
            """
            Start

            {% if Customer.IsPreferred %}

            Preferred customer: {{ Customer.Name }}

            {% else %}

            Regular customer: {{ Customer.Name }}

            {% endif %}

            End
            """);

        var store = new TemplateStore();
        store.RegisterDocxTemplate<Invoice>("conditional", template);
        using var stream = new MemoryStream();
        await store.Render("conditional", SampleData.Invoice(), stream);
        stream.Position = 0;
        await Verify(stream, "docx");
    }

    public class FlagModel
    {
        public required bool Flag { get; init; }
        public required string Label { get; init; }
    }

    [Test]
    public async Task ElseBranchRenders()
    {
        using var template = DocxTemplateBuilder.Build(
            """
            Start

            {% if Flag %}

            Affirmative: {{ Label }}

            {% else %}

            Negative: {{ Label }}

            {% endif %}

            End
            """);

        var store = new TemplateStore();
        store.RegisterDocxTemplate<FlagModel>("else-branch", template);
        using var stream = new MemoryStream();
        await store.Render("else-branch", new FlagModel
        {
            Flag = false,
            Label = "fallback"
        }, stream);
        stream.Position = 0;
        await Verify(stream, "docx");
    }
}
