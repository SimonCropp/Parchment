static class DocxCloner
{
    public static MemoryStream ToWritableStream(byte[] bytes)
    {
        var stream = new MemoryStream();
        stream.Write(bytes, 0, bytes.Length);
        stream.Position = 0;
        return stream;
    }

    public static MemoryStream ToWritableStream(Stream source)
    {
        if (source is MemoryStream ms)
        {
            return ToWritableStream(ms.ToArray());
        }

        var stream = new MemoryStream();
        source.CopyTo(stream);
        stream.Position = 0;
        return stream;
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
}
