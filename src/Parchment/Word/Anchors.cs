/// <summary>
/// Manages the Parchment anchor bookmarks injected at registration time into token-bearing paragraphs.
/// Anchors survive clones intact and let us locate the host paragraph by name at render time, without
/// relying on fragile positional indices that break when structural replacements expand a token into
/// multiple elements.
/// </summary>
static class Anchors
{
    public const string Prefix = "parchment-anchor-";

    public static string EnsureOn(Paragraph paragraph)
    {
        var existing = paragraph
            .Elements<BookmarkStart>()
            .FirstOrDefault(_ => _.Name != null &&
                                 _.Name.Value != null &&
                                 _.Name.Value.StartsWith(Prefix, StringComparison.Ordinal));
        if (existing?.Name?.Value != null)
        {
            return existing.Name.Value;
        }

        var name = Prefix + Guid.NewGuid().ToString("N");
        var id = NextBookmarkId(paragraph);
        var start = new BookmarkStart
        {
            Id = id.ToString(),
            Name = name
        };
        var end = new BookmarkEnd
        {
            Id = id.ToString()
        };
        InsertAfterProperties(paragraph, start, end);
        return name;
    }

    public static Dictionary<string, Paragraph> BuildMap(OpenXmlCompositeElement root)
    {
        var map = new Dictionary<string, Paragraph>(StringComparer.Ordinal);
        foreach (var bookmarkStart in root.Descendants<BookmarkStart>())
        {
            var name = bookmarkStart.Name?.Value;
            if (name == null || !name.StartsWith(Prefix, StringComparison.Ordinal))
            {
                continue;
            }

            var host = bookmarkStart.Ancestors<Paragraph>().FirstOrDefault();
            if (host != null)
            {
                map[name] = host;
            }
        }

        return map;
    }

    public static void StripAll(OpenXmlCompositeElement root)
    {
        var starts = root
            .Descendants<BookmarkStart>()
            .Where(_ => _.Name?.Value?.StartsWith(Prefix, StringComparison.Ordinal) == true)
            .ToList();
        foreach (var start in starts)
        {
            var id = start.Id?.Value;
            start.Remove();
            if (id == null)
            {
                continue;
            }

            var end = root.Descendants<BookmarkEnd>().FirstOrDefault(_ => _.Id?.Value == id);
            end?.Remove();
        }
    }

    public static void RenameIn(OpenXmlCompositeElement root, IDictionary<string, string> map)
    {
        foreach (var start in root.Descendants<BookmarkStart>())
        {
            var name = start.Name?.Value;
            if (name != null &&
                map.TryGetValue(name, out var replacement))
            {
                start.Name = replacement;
            }
        }
    }

    static int NextBookmarkId(Paragraph paragraph)
    {
        var root = (OpenXmlCompositeElement?)paragraph.Ancestors<Body>().FirstOrDefault() ??
                   (OpenXmlCompositeElement?)paragraph.Ancestors<Header>().FirstOrDefault() ??
                   (OpenXmlCompositeElement?)paragraph.Ancestors<Footer>().FirstOrDefault() ??
                   paragraph.Ancestors<OpenXmlCompositeElement>().LastOrDefault() ??
                   paragraph;

        var max = 0;
        foreach (var b in root.Descendants<BookmarkStart>())
        {
            if (int.TryParse(b.Id?.Value, out var value) && value > max)
            {
                max = value;
            }
        }

        return max + 1;
    }

    static void InsertAfterProperties(Paragraph paragraph, BookmarkStart start, BookmarkEnd end)
    {
        var pPr = paragraph.ParagraphProperties;
        if (pPr == null)
        {
            paragraph.InsertAt(end, 0);
            paragraph.InsertAt(start, 0);
        }
        else
        {
            paragraph.InsertAfter(start, pPr);
            paragraph.InsertAfter(end, start);
        }
    }
}
