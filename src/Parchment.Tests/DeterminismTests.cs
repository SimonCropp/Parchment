public class DeterminismTests
{
    [Test]
    public async Task DocxRenderIsByteIdentical()
    {
        var template = DocxTemplateBuilder.Build(
            """
            Invoice {{ Number }}

            Customer: {{ Customer.Name }}

            Total: {{ Total }} {{ Currency }}
            """);

        var store = new TemplateStore();
        store.RegisterDocxTemplate<Invoice>("determinism", template);

        var first = await Render(store, SampleData.Invoice());
        var second = await Render(store, SampleData.Invoice());

        await Assert.That(first).IsEquivalentTo(second);
    }

    static async Task<byte[]> Render(TemplateStore store, Invoice model)
    {
        using var stream = new MemoryStream();
        await store.Render("determinism", model, stream);
        return stream.ToArray();
    }
}
