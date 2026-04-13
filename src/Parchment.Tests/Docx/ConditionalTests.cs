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
}
