namespace Parchment;

public static class TokenValueHelpers
{
    public static TokenValue BulletList(IEnumerable<string> items) =>
        new OpenXmlToken(context =>
        {
            var numId = context.CreateBulletNumbering();
            return items.Select(item => BuildListParagraph(item, numId, 0));
        });

    public static TokenValue NumberedList(IEnumerable<string> items, NumberFormatValues? format = null) =>
        new OpenXmlToken(context =>
        {
            var numId = context.CreateOrderedNumbering(format ?? NumberFormatValues.Decimal);
            return items.Select(item => BuildListParagraph(item, numId, 0));
        });

    static OpenXmlElement BuildListParagraph(string text, int numId, int ilvl)
    {
        var paragraph = new Paragraph();
        var props = new ParagraphProperties
        {
            ParagraphStyleId = new()
            {
                Val = "ListParagraph"
            },
            NumberingProperties = new(
                new NumberingLevelReference
                {
                    Val = ilvl
                },
                new NumberingId
                {
                    Val = numId
                })
        };
        paragraph.Append(props);
        paragraph.Append(new Run(
            new Text(XmlCharSanitizer.Strip(text))
            {
                Space = SpaceProcessingModeValues.Preserve
            }));
        return paragraph;
    }
}
