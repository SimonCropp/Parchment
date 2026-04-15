static class DocxCloner
{
    public static MemoryStream ToWritableStream(byte[] bytes)
    {
        var stream = new MemoryStream();
        stream.Write(bytes, 0, bytes.Length);
        stream.Position = 0;
        return stream;
    }

    public static byte[] Save(WordprocessingDocument doc, MemoryStream stream)
    {
        doc.Save();
        doc.Dispose();
        return stream.ToArray();
    }

    public static IEnumerable<(string uri, OpenXmlCompositeElement root)> EnumerateParts(WordprocessingDocument doc)
    {
        var main = doc.MainDocumentPart;
        if (main == null)
        {
            yield break;
        }

        if (main.Document?.Body is { } body)
        {
            yield return (main.Uri.ToString(), body);
        }

        foreach (var headerPart in main.HeaderParts)
        {
            if (headerPart.Header is { } header)
            {
                yield return (headerPart.Uri.ToString(), header);
            }
        }

        foreach (var footerPart in main.FooterParts)
        {
            if (footerPart.Footer is { } footer)
            {
                yield return (footerPart.Uri.ToString(), footer);
            }
        }

        if (main.FootnotesPart?.Footnotes is { } footnotes)
        {
            yield return (main.FootnotesPart.Uri.ToString(), footnotes);
        }

        if (main.EndnotesPart?.Endnotes is { } endnotes)
        {
            yield return (main.EndnotesPart.Uri.ToString(), endnotes);
        }
    }

    public static IEnumerable<(string uri, Paragraph paragraph)> EnumerateParagraphs(WordprocessingDocument doc)
    {
        foreach (var (uri, root) in EnumerateParts(doc))
        {
            foreach (var paragraph in root.Descendants<Paragraph>())
            {
                yield return (uri, paragraph);
            }
        }
    }
}
