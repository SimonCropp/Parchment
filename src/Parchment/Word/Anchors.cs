/// <summary>
/// Manages the Parchment anchor bookmarks injected at registration time into token-bearing paragraphs.
/// Anchors survive clones intact and let us locate the host paragraph by name at render time, without
/// relying on fragile positional indices that break when structural replacements expand a token into
/// multiple elements.
/// </summary>
static class Anchors
{
    public const string Prefix = "parchment-anchor-";
    static long runtimeCounter;

    /// <summary>
    /// Generates a unique anchor name for runtime clones of registration-time bookmarks.
    /// Uses a monotonic counter (rather than a fresh GUID per call) because anchors only need
    /// uniqueness within the active document and are stripped before save — the counter is
    /// dramatically cheaper than <c>Guid.NewGuid().ToString("N")</c> in tight loop iterations.
    /// </summary>
    public static string NextRuntimeName() =>
        Prefix + Interlocked.Increment(ref runtimeCounter).ToString(CultureInfo.InvariantCulture);

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

            // Parchment-prefixed bookmarks are inserted as direct paragraph children
            // (see InsertAfterProperties), so a single Parent cast is sufficient.
            if (bookmarkStart.Parent is Paragraph host)
            {
                map[name] = host;
            }
        }

        return map;
    }

    public static void StripAll(OpenXmlCompositeElement root)
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        var starts = new List<BookmarkStart>();
        foreach (var start in root.Descendants<BookmarkStart>())
        {
            if (start.Name?.Value?.StartsWith(Prefix, StringComparison.Ordinal) != true)
            {
                continue;
            }

            starts.Add(start);
            var id = start.Id?.Value;
            if (id != null)
            {
                ids.Add(id);
            }
        }

        if (starts.Count == 0)
        {
            return;
        }

        // Walk BookmarkEnds once and collect those whose id matches any stripped start —
        // avoids the O(N²) per-start `Descendants<BookmarkEnd>().FirstOrDefault(...)` scan.
        var ends = new List<BookmarkEnd>();
        foreach (var end in root.Descendants<BookmarkEnd>())
        {
            var id = end.Id?.Value;
            if (id != null && ids.Contains(id))
            {
                ends.Add(end);
            }
        }

        foreach (var start in starts)
        {
            start.Remove();
        }

        foreach (var end in ends)
        {
            end.Remove();
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
