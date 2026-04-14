public class ConsumerTests
{
    public class Customer
    {
        public required string Name { get; init; }
    }

    public class InvoiceModel
    {
        public required string Number { get; init; }
        public required Customer Customer { get; init; }
    }

    [Test]
    public async Task RegisterAndRender()
    {
        var template = BuildTemplate();
        var store = new TemplateStore();
        store.RegisterDocxTemplate<InvoiceModel>("invoice", template);

        using var stream = new MemoryStream();
        await store.Render("invoice", new InvoiceModel
        {
            Number = "INT-001",
            Customer = new() { Name = "Acme" }
        }, stream);

        await Assert.That(stream.Length).IsGreaterThan(0);
    }

    [Test]
    public async Task LoopsAndConditionals()
    {
        var template = BuildLoopTemplate();
        var store = new TemplateStore();
        store.RegisterDocxTemplate<LoopModel>("loop", template);

        using var stream = new MemoryStream();
        await store.Render("loop", new LoopModel
        {
            Items = ["alpha", "beta", "gamma"]
        }, stream);

        await Assert.That(stream.Length).IsGreaterThan(0);
    }

    public class LoopModel
    {
        public required IReadOnlyList<string> Items { get; init; }
    }

    static byte[] BuildLoopTemplate()
    {
        using var stream = new MemoryStream();
        using (var doc = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document))
        {
            var mainPart = doc.AddMainDocumentPart();
            mainPart.Document = new Document(new Body(
                new Paragraph(new Run(new Text("{% for item in Items %}") { Space = SpaceProcessingModeValues.Preserve })),
                new Paragraph(new Run(new Text("- {{ item }}") { Space = SpaceProcessingModeValues.Preserve })),
                new Paragraph(new Run(new Text("{% endfor %}") { Space = SpaceProcessingModeValues.Preserve }))));

            var stylesPart = mainPart.AddNewPart<StyleDefinitionsPart>();
            var styles = new Styles();
            styles.Append(new Style { Type = StyleValues.Paragraph, StyleId = "Normal", Default = true }.AppendChild(new StyleName { Val = "Normal" }).Parent!);
            stylesPart.Styles = styles;
        }

        return stream.ToArray();
    }

    static byte[] BuildTemplate()
    {
        using var stream = new MemoryStream();
        using (var doc = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document))
        {
            var mainPart = doc.AddMainDocumentPart();
            mainPart.Document = new Document(new Body(
                new Paragraph(new Run(new Text("Invoice {{ Number }}") { Space = SpaceProcessingModeValues.Preserve })),
                new Paragraph(new Run(new Text("Customer: {{ Customer.Name }}") { Space = SpaceProcessingModeValues.Preserve }))));

            var stylesPart = mainPart.AddNewPart<StyleDefinitionsPart>();
            var styles = new Styles();
            styles.Append(new Style { Type = StyleValues.Paragraph, StyleId = "Normal", Default = true }.AppendChild(new StyleName { Val = "Normal" }).Parent!);
            stylesPart.Styles = styles;
        }

        return stream.ToArray();
    }
}
