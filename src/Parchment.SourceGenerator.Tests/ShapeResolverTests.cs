public class ShapeResolverTests
{
    static readonly ModelShape shape = BuildShape();

    [Test]
    public async Task Resolve_RootMember_ReturnsMemberType()
    {
        var result = ShapeResolver.Resolve(shape, ["Customer"], emptyScope);
        await Assert.That(result).IsEqualTo("global::Sample.Customer");
    }

    [Test]
    public async Task Resolve_NestedMember_WalksTypeChain()
    {
        var result = ShapeResolver.Resolve(shape, ["Customer", "Name"], emptyScope);
        await Assert.That(result).IsEqualTo("string");
    }

    [Test]
    public async Task Resolve_IsCaseInsensitive()
    {
        var result = ShapeResolver.Resolve(shape, ["customer", "NAME"], emptyScope);
        await Assert.That(result).IsEqualTo("string");
    }

    [Test]
    public async Task Resolve_UnknownMember_ReturnsNull()
    {
        var result = ShapeResolver.Resolve(shape, ["Customer", "DoesNotExist"], emptyScope);
        await Assert.That(result).IsNull();
    }

    [Test]
    public async Task Resolve_UnknownRootMember_ReturnsNull()
    {
        var result = ShapeResolver.Resolve(shape, ["NotAField"], emptyScope);
        await Assert.That(result).IsNull();
    }

    [Test]
    public async Task Resolve_EmptySegments_ReturnsNull()
    {
        var result = ShapeResolver.Resolve(shape, [], emptyScope);
        await Assert.That(result).IsNull();
    }

    [Test]
    public async Task Resolve_TraversingPrimitive_ReturnsNull()
    {
        // "Customer.Name" resolves to string, but string isn't in the shape, so going further fails.
        var result = ShapeResolver.Resolve(shape, ["Customer", "Name", "Length"], emptyScope);
        await Assert.That(result).IsNull();
    }

    [Test]
    public async Task Resolve_ScopedIdentifierShortCircuitsToBoundType()
    {
        // Loop variable `item` bound to Customer — "item.Name" should resolve via the binding,
        // not the root.
        var scope = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["item"] = "global::Sample.Customer"
        };

        var result = ShapeResolver.Resolve(shape, ["item", "Name"], scope);
        await Assert.That(result).IsEqualTo("string");
    }

    [Test]
    public async Task Resolve_ScopedIdentifierAlone_ReturnsBoundType()
    {
        var scope = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["item"] = "global::Sample.Customer"
        };

        var result = ShapeResolver.Resolve(shape, ["item"], scope);
        await Assert.That(result).IsEqualTo("global::Sample.Customer");
    }

    [Test]
    public async Task GetElementType_ReturnsConfiguredElement()
    {
        var element = ShapeResolver.GetElementType(shape, "global::Sample.Invoice");
        await Assert.That(element).IsEqualTo("global::Sample.LineItem");
    }

    [Test]
    public async Task GetElementType_NonCollectionType_ReturnsNull()
    {
        var element = ShapeResolver.GetElementType(shape, "global::Sample.Customer");
        await Assert.That(element).IsNull();
    }

    [Test]
    public async Task GetElementType_UnknownType_ReturnsNull()
    {
        var element = ShapeResolver.GetElementType(shape, "global::Sample.Unknown");
        await Assert.That(element).IsNull();
    }

    static Dictionary<string, string> emptyScope = new(StringComparer.OrdinalIgnoreCase);

    static ModelShape BuildShape()
    {
        // Sample.Invoice
        //   Customer : Sample.Customer
        //   Lines    : (collection of Sample.LineItem)
        // Sample.Customer
        //   Name     : string
        // Sample.LineItem
        //   Sku      : string
        var invoice = new TypeEntry(
            "global::Sample.Invoice",
            ElementTypeFullyQualifiedName: "global::Sample.LineItem",
            new(
            [
                new("Customer", "global::Sample.Customer"),
                new("Lines", "global::Sample.LineItemCollection")
            ]));

        var customer = new TypeEntry(
            "global::Sample.Customer",
            ElementTypeFullyQualifiedName: null,
            new([new("Name", "string")]));

        var lineItem = new TypeEntry(
            "global::Sample.LineItem",
            ElementTypeFullyQualifiedName: null,
            new([new("Sku", "string")]));

        return new(
            "global::Sample.Invoice",
            new([invoice, customer, lineItem]));
    }
}
