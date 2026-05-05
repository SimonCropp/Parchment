class LinkInlineRenderer :
    MarkdownObjectRenderer<OpenXmlMarkdownRenderer, LinkInline>
{
    protected override void Write(OpenXmlMarkdownRenderer renderer, LinkInline inline)
    {
        if (inline.IsImage)
        {
            WriteImage(renderer, inline);
            return;
        }

        WriteLink(renderer, inline);
    }

    static void WriteLink(OpenXmlMarkdownRenderer renderer, LinkInline inline)
    {
        var url = inline.Url ?? string.Empty;
        if (string.IsNullOrEmpty(url))
        {
            renderer.WriteChildren(inline);
            return;
        }

        var relId = renderer.MainPart.AddHyperlinkRelationship(new(url, UriKind.RelativeOrAbsolute), true).Id;

        var top = renderer.Top;
        var before = top.CurrentRuns.Count;
        renderer.WriteChildren(inline);
        var produced = top.CurrentRuns.Skip(before).ToList();

        var hyperlink = new Hyperlink
        {
            Id = relId
        };
        foreach (var run in produced)
        {
            if (run is Run runElement)
            {
                runElement.RunProperties ??= new();
                runElement.RunProperties.Append(
                    new RunStyle
                    {
                        Val = "Hyperlink"
                    });
                hyperlink.Append(runElement);
            }
            else
            {
                hyperlink.Append(run);
            }
        }

        top.CurrentRuns.RemoveRange(before, top.CurrentRuns.Count - before);
        renderer.AddRun(hyperlink);
    }

    // Synthesize an <img> tag and delegate to OpenXmlHtml so drawing/blip plumbing stays in one place.
    // data: URIs and absolute file paths are resolved to data URIs here; everything else is passed
    // through, which makes OpenXmlHtml fall back to rendering the alt text — same behavior as a raw
    // <img> in an [Html] property.
    static void WriteImage(OpenXmlMarkdownRenderer renderer, LinkInline inline)
    {
        var url = inline.Url;
        if (string.IsNullOrWhiteSpace(url))
        {
            renderer.WriteChildren(inline);
            return;
        }

        var src = ResolveImageSrc(url);
        var alt = ExtractAlt(inline);
        var html = $"<img src=\"{HtmlEscape(src)}\" alt=\"{HtmlEscape(alt)}\" />";

        var elements = OpenXmlHtml.WordHtmlConverter.ToElements(html, renderer.MainPart);
        foreach (var element in elements)
        {
            if (element is Paragraph paragraph)
            {
                foreach (var run in paragraph.ChildElements.OfType<Run>().ToList())
                {
                    run.Remove();
                    renderer.AddRun(run);
                }
            }
        }
    }

    static string ResolveImageSrc(string url)
    {
        if (url.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
        {
            return url;
        }

        string? path = null;
        if (url.StartsWith("file://", StringComparison.OrdinalIgnoreCase))
        {
            if (Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.IsFile)
            {
                path = uri.LocalPath;
            }
        }
        else if (!url.Contains("://") && Path.IsPathRooted(url))
        {
            path = url;
        }

        if (path is null || !File.Exists(path))
        {
            return url;
        }

        var bytes = File.ReadAllBytes(path);
        var mime = MimeFromExtension(Path.GetExtension(path));
        return $"data:{mime};base64,{Convert.ToBase64String(bytes)}";
    }

    static string MimeFromExtension(string extension) =>
        extension.ToLowerInvariant() switch
        {
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".gif" => "image/gif",
            ".bmp" => "image/bmp",
            ".tif" or ".tiff" => "image/tiff",
            _ => "application/octet-stream"
        };

    static string ExtractAlt(LinkInline inline)
    {
        var builder = new StringBuilder();
        Walk(inline, builder);
        return builder.ToString();
    }

    static void Walk(ContainerInline container, StringBuilder builder)
    {
        var child = container.FirstChild;
        while (child != null)
        {
            switch (child)
            {
                case LiteralInline literal:
                    builder.Append(literal.Content);
                    break;
                case ContainerInline inner:
                    Walk(inner, builder);
                    break;
            }
            child = child.NextSibling;
        }
    }

    static string HtmlEscape(string value) =>
        value
            .Replace("&", "&amp;")
            .Replace("\"", "&quot;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;");
}
