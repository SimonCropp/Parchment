namespace Parchment.Tests.Docx;

using Excelsior;

public class ExcelsiorTableTests
{
    // begin-snippet: ExcelsiorTableModel
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
    // end-snippet

    public class Order
    {
        public required string Number { get; init; }
        public required Buyer Buyer { get; init; }
    }

    public class Buyer
    {
        public required string Name { get; init; }

        [ExcelsiorTable]
        public required IReadOnlyList<Address> Addresses { get; init; }
    }

    public class Address
    {
        [Column(Order = 1)]
        public required string Street { get; init; }

        [Column(Order = 2)]
        public required string City { get; init; }
    }

    [Test]
    public async Task NestedPathRendersAsExcelsiorTable()
    {
        // The [ExcelsiorTable] is on Buyer.Addresses, not on the root Order. The substitution
        // {{ Buyer.Addresses }} must walk the nested path at registration time and the runner
        // must dispatch on the dotted-path lookup.
        var template = DocxTemplateBuilder.Build(
            "Order {{ Number }}",
            "Buyer: {{ Buyer.Name }}",
            "{{ Buyer.Addresses }}",
            "End.");

        var store = new TemplateStore();
        store.RegisterDocxTemplate<Order>("nested-order", template);

        var model = new Order
        {
            Number = "ORD-2026-0001",
            Buyer = new()
            {
                Name = "Acme Corp",
                Addresses =
                [
                    new() { Street = "1 Pine St", City = "Portland" },
                    new() { Street = "42 Oak Ave", City = "Seattle" }
                ]
            }
        };

        using var stream = new MemoryStream();
        await store.Render("nested-order", model, stream);
        stream.Position = 0;
        await Verify(stream, "docx");
    }

    [Test]
    public async Task MixedInlineContentInSameParagraphIsRejected()
    {
        // "Prefix {{ Lines }}" — the token doesn't cover the whole paragraph. Structural
        // replacement would drop "Prefix ", so registration must fail up-front.
        var template = DocxTemplateBuilder.Build(
            "Quote {{ Reference }}",
            "Prefix {{ Lines }}",
            "End.");

        var store = new TemplateStore();
        var exception = await Assert.That(
            () => store.RegisterDocxTemplate<Quote>("mixed-inline", template))
            .Throws<ParchmentRegistrationException>();
        await Assert.That(exception!.Message).Contains("must sit alone in its own paragraph");
    }

    [Test]
    public async Task FilterOnExcelsiorTokenIsRejected()
    {
        // {{ Lines | reverse }} — the Excelsior renderer walks the model object directly
        // and bypasses Fluid, so a filter would be silently dropped. Registration must fail.
        var template = DocxTemplateBuilder.Build(
            "Quote {{ Reference }}",
            "{{ Lines | reverse }}",
            "End.");

        var store = new TemplateStore();
        var exception = await Assert.That(
            () => store.RegisterDocxTemplate<Quote>("filter-rejected", template))
            .Throws<ParchmentRegistrationException>();
        await Assert.That(exception!.Message).Contains("plain member-access");
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
