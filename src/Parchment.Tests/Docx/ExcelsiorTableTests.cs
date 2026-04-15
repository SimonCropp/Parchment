namespace Parchment.Tests.Docx;

using Excelsior;

public class ExcelsiorTableTests
{
    public class Quote
    {
        public required string Reference { get; init; }

        [ExcelsiorTable]
        public required IReadOnlyList<QuoteLine> Lines { get; init; }
    }

    public class QuoteLine
    {
        [Column(Heading = "Item", Order = 1)]
        public required string Description { get; init; }

        [Column(Heading = "Qty", Order = 2)]
        public required int Quantity { get; init; }

        [Column(Order = 3, Format = "C0")]
        public required decimal UnitPrice { get; init; }
    }

    [Test]
    public async Task LinesPropertyRendersAsExcelsiorTable()
    {
        // The {{ Lines }} substitution is in its own paragraph so the structural-replacement
        // path can swap it out for the table elements produced by Excelsior.
        var template = DocxTemplateBuilder.Build(
            "Quote {{ Reference }}",
            "{{ Lines }}",
            "Thank you for your business.");

        var store = new TemplateStore();
        store.RegisterDocxTemplate<Quote>("excelsior-quote", template);

        var model = new Quote
        {
            Reference = "Q-2026-0042",
            Lines =
            [
                new() { Description = "Strategy workshop", Quantity = 2, UnitPrice = 4500m },
                new() { Description = "Implementation support", Quantity = 8, UnitPrice = 1750m },
                new() { Description = "Documentation review", Quantity = 1, UnitPrice = 950m }
            ]
        };

        using var stream = new MemoryStream();
        await store.Render("excelsior-quote", model, stream);
        stream.Position = 0;
        await Verify(stream, "docx");
    }
}
