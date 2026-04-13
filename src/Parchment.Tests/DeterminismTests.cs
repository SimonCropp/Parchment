namespace Parchment.Tests;

public class DeterminismTests
{
    [Test]
    public async Task DocxRenderIsByteIdentical()
    {
        var template = Fixtures.DocxTemplateBuilder.Build(
            "Invoice {{ Number }}",
            "Customer: {{ Customer.Name }}",
            "Total: {{ Total }} {{ Currency }}");

        var store = new TemplateStore();
        store.RegisterDocxTemplate<Invoice>("determinism", template);

        var first = await store.Render("determinism", SampleData.Invoice());
        var second = await store.Render("determinism", SampleData.Invoice());

        await Assert.That(first).IsEquivalentTo(second);
    }
}
