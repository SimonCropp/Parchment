namespace ParchmentModel;

public static class SampleData
{
    public static Invoice Invoice() =>
        new()
        {
            Number = "INV-2026-0042",
            IssueDate = new(2026, 4, 1),
            DueDate = new(2026, 4, 30),
            Currency = "USD",
            Notes = "Thanks for your business.",
            Tags = ["priority", "net-30", "repeat-customer"],
            Customer = new()
            {
                Name = "Globex Corporation",
                Email = "ap@globex.test",
                Address = "123 Commerce Way, Springfield, IL 62704",
                VatNumber = "US-123456789",
                IsPreferred = true
            },
            Lines =
            [
                new()
                {
                    Description = "Consulting services — API design",
                    Quantity = 12,
                    UnitPrice = 185m
                },
                new()
                {
                    Description = "Consulting services — implementation",
                    Quantity = 40,
                    UnitPrice = 165m
                },
                new()
                {
                    Description = "Project management",
                    Quantity = 8,
                    UnitPrice = 145m
                }
            ]
        };
}
