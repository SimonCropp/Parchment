namespace Parchment.Tests;

public class UsageTests
{
    [Test]
    public async Task Substitution()
    {
        var template = Fixtures.DocxTemplateBuilder.Build(
            "Invoice {{ Number }}",
            "Customer: {{ Customer.Name }}",
            "Total: {{ Total }} {{ Currency }}");

        var store = new TemplateStore();
        store.RegisterDocxTemplate<Invoice>("substitution", template);

        var bytes = await store.Render("substitution", SampleData.Invoice());
        await Verify(bytes, "docx");
    }
}
