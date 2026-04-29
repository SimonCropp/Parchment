public class LoopTests
{
    [Test]
    public async Task ParagraphScopeLoop()
    {
        using var template = DocxTemplateBuilder.Build(
            """
            Items:

            {% for line in Lines %}

            - {{ line.Description }}: {{ line.Quantity }} x {{ line.UnitPrice }}

            {% endfor %}

            End.
            """);

        var store = new TemplateStore();
        store.RegisterDocxTemplate<Invoice>("loop", template);

        using var stream = new MemoryStream();
        await store.Render("loop", SampleData.Invoice(), stream);
        stream.Position = 0;
        await Verify(stream, "docx");
    }

    [Test]
    public async Task NestedLoop()
    {
        using var template = DocxTemplateBuilder.Build(
            """
            {% for group in Groups %}

            {{ group.Name }}

            {% for item in group.Items %}

            - {{ item }}

            {% endfor %}

            {% endfor %}
            """);

        var store = new TemplateStore();
        store.RegisterDocxTemplate<NestedModel>("nested-loop", template);
        using var stream = new MemoryStream();
        await store.Render(
            "nested-loop",
            new NestedModel
            {
                Groups =
                [
                    new()
                    {
                        Name = "Fruit",
                        Items = ["apple", "pear"]
                    },
                    new()
                    {
                        Name = "Tools",
                        Items = ["hammer", "saw", "drill"]
                    }
                ]
            },
            stream);
        stream.Position = 0;
        await Verify(stream, "docx");
    }

    public class NestedModel
    {
        public required IReadOnlyList<NestedGroup> Groups { get; init; }
    }

    public class NestedGroup
    {
        public required string Name { get; init; }
        public required IReadOnlyList<string> Items { get; init; }
    }
}
