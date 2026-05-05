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
        await store.Render(
            "else-branch",
            new FlagModel
            {
                Flag = false,
                Label = "fallback"
            },
            stream);
        stream.Position = 0;
        await Verify(stream, "docx");
    }

    [Test]
    public async Task NoMatchingBranch_NoElse_AllBranchParagraphsRemoved()
    {
        // Condition is false and there is no else branch. Every paragraph between {% if %} and
        // {% endif %} (plus the open/close anchor paragraphs) must be removed; surrounding
        // "Before"/"After" paragraphs stay.
        using var template = DocxTemplateBuilder.Build(
            """
            Before

            {% if Flag %}

            Affirmative: {{ Label }}

            {% endif %}

            After
            """);

        var store = new TemplateStore();
        store.RegisterDocxTemplate<FlagModel>("no-match-no-else", template);
        using var stream = new MemoryStream();
        await store.Render(
            "no-match-no-else",
            new FlagModel
            {
                Flag = false,
                Label = "ignored"
            },
            stream);
        stream.Position = 0;

        using var doc = WordprocessingDocument.Open(stream, false);
        var texts = doc.MainDocumentPart!.Document!.Body!
            .Elements<Paragraph>()
            .Select(_ => string.Concat(_.Descendants<Text>().Select(t => t.Text)))
            .Where(_ => _.Length > 0)
            .ToList();
        await Assert.That(texts).IsEquivalentTo(new[] { "Before", "After" });
    }
}
