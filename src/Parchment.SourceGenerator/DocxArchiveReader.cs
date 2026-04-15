static class DocxArchiveReader
{
    static readonly XNamespace w = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";

    public static List<string> ReadParagraphTexts(string filePath)
    {
        var result = new List<string>();
        using var archive = ZipFile.OpenRead(filePath);
        foreach (var entry in archive.Entries)
        {
            if (!LooksLikeWordPart(entry.FullName))
            {
                continue;
            }

            using var stream = entry.Open();
            XDocument doc;
            try
            {
                doc = XDocument.Load(stream);
            }
            catch
            {
                continue;
            }

            foreach (var paragraph in doc.Descendants(w + "p"))
            {
                var builder = new StringBuilder();
                foreach (var t in paragraph.Descendants(w + "t"))
                {
                    builder.Append(t.Value);
                }

                var text = builder.ToString();
                if (text.Length > 0)
                {
                    result.Add(text);
                }
            }
        }

        return result;
    }

    static bool LooksLikeWordPart(string path) =>
        path.StartsWith("word/document.xml", StringComparison.OrdinalIgnoreCase) ||
        path.StartsWith("word/header", StringComparison.OrdinalIgnoreCase) ||
        path.StartsWith("word/footer", StringComparison.OrdinalIgnoreCase) ||
        path.StartsWith("word/footnotes.xml", StringComparison.OrdinalIgnoreCase) ||
        path.StartsWith("word/endnotes.xml", StringComparison.OrdinalIgnoreCase);
}
