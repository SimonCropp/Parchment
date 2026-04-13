namespace Parchment.Tests.Docx;

public class ConditionalTests
{
    [Test]
    public async Task IfTrue()
    {
        var template = Fixtures.DocxTemplateBuilder.Build(
            "Start",
            "{% if Customer.IsPreferred %}",
            "Preferred customer: {{ Customer.Name }}",
            "{% else %}",
            "Regular customer: {{ Customer.Name }}",
            "{% endif %}",
            "End");

        var store = new TemplateStore();
        store.RegisterDocxTemplate<Invoice>("conditional", template);
        var bytes = await store.Render("conditional", SampleData.Invoice());
        await Verify(bytes, "docx");
    }

    public class FlagModel
    {
        public required bool Flag { get; init; }
        public required string Label { get; init; }
    }

    [Test]
    public async Task ElseBranchRenders()
    {
        var template = Fixtures.DocxTemplateBuilder.Build(
            "Start",
            "{% if Flag %}",
            "Affirmative: {{ Label }}",
            "{% else %}",
            "Negative: {{ Label }}",
            "{% endif %}",
            "End");

        var store = new TemplateStore();
        store.RegisterDocxTemplate<FlagModel>("else-branch", template);
        var bytes = await store.Render("else-branch", new FlagModel
        {
            Flag = false,
            Label = "fallback"
        });
        await Verify(bytes, "docx");
    }
}
