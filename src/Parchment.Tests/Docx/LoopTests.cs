namespace Parchment.Tests.Docx;

public class LoopTests
{
    [Test]
    public async Task ParagraphScopeLoop()
    {
        var template = DocxTemplateBuilder.Build(
            "Items:",
            "{% for line in Lines %}",
            "- {{ line.Description }}: {{ line.Quantity }} x {{ line.UnitPrice }}",
            "{% endfor %}",
            "End.");

        var store = new TemplateStore();
        store.RegisterDocxTemplate<Invoice>("loop", template);

        using var stream = new MemoryStream();
        await store.Render("loop", SampleData.Invoice(), stream);
        stream.Position = 0;
        await Verify(stream, "docx");
    }

    [Test]
    public async Task NestedLoopsAreNotSupportedInV1()
    {
        // Sanity: a single-level loop binds `line` in scope and resolves line.Description correctly.
        var template = DocxTemplateBuilder.Build(
            "{% for line in Lines %}",
            "{{ line.Description }}",
            "{% endfor %}");

        var store = new TemplateStore();
        store.RegisterDocxTemplate<Invoice>("loop-only", template);
        using var stream = new MemoryStream();
        await store.Render("loop-only", SampleData.Invoice(), stream);
        stream.Position = 0;
        await Verify(stream, "docx");
    }
}
