public class ThreadSafetyTests
{
    [Test]
    public async Task ParallelRendersProduceIdenticalOutput()
    {
        var template = DocxTemplateBuilder.Build(
            """
            Invoice {{ Number }}

            Customer: {{ Customer.Name }}
            """);

        var store = new TemplateStore();
        store.RegisterDocxTemplate<Invoice>("threading", template);

        var tasks = Enumerable.Range(0, 20)
            .Select(async _ =>
            {
                using var stream = new MemoryStream();
                await store.Render("threading", SampleData.Invoice(), stream);
                return stream.ToArray();
            })
            .ToArray();

        var results = await Task.WhenAll(tasks);

        var first = results[0];
        foreach (var result in results)
        {
            await Assert.That(result).IsEquivalentTo(first);
        }
    }
}
