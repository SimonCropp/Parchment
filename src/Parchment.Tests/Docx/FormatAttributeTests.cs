using System.Diagnostics.CodeAnalysis;

public class FormatAttributeTests
{
    static string SourcePath([CallerFilePath] string path = "") => path;

    static string ScenarioPath(string scenarioName) =>
        Path.Combine(
            ProjectFiles.ProjectDirectory,
            "Scenarios",
            scenarioName);

    #region HtmlAttribute
    [AttributeUsage(AttributeTargets.Property)]
    sealed class HtmlAttribute : Attribute;
    #endregion

    #region MarkdownAttribute
    [AttributeUsage(AttributeTargets.Property)]
    sealed class MarkdownAttribute : Attribute;
    #endregion

    #region HtmlModel
    public class HtmlDoc
    {
        public required string Title { get; init; }

        [Html]
        public required string Body { get; init; }
    }
    #endregion

    #region MarkdownModel
    public class MarkdownDoc
    {
        public required string Title { get; init; }

        [Markdown]
        public required string Body { get; init; }
    }
    #endregion

    #region StringSyntaxHtmlModel
    public class StringSyntaxHtmlDoc
    {
        public required string Title { get; init; }

        [StringSyntax("html")]
        public required string Body { get; init; }
    }
    #endregion

    #region StringSyntaxMarkdownModel
    public class StringSyntaxMarkdownDoc
    {
        public required string Title { get; init; }

        [StringSyntax("markdown")]
        public required string Body { get; init; }
    }
    #endregion

    public class MismatchDoc
    {
        [Html]
        [StringSyntax("markdown")]
        public required string Body { get; init; }
    }

    public class BothDoc
    {
        [Html]
        [Markdown]
        public required string Body { get; init; }
    }

    public class TwoHtmlBlockDoc
    {
        [Html]
        public required string A { get; init; }

        [Html]
        public required string B { get; init; }
    }

    [Test]
    public async Task RenderHtml()
    {
        #region HtmlUsage
        var templatePath = Path.Combine(ScenarioPath("html-property"), "input.docx");

        var store = new TemplateStore();
        store.RegisterDocxTemplate<HtmlDoc>("html-doc", templatePath);

        var model = new HtmlDoc
        {
            Title = "Report",
            Body = "<p>Hello <b>world</b></p><p>Second para</p>"
        };

        using var stream = new MemoryStream();
        await store.Render("html-doc", model, stream);
        #endregion

        var settings = new VerifySettings();
        settings.UseDirectory(ScenarioPath("html-property"));
        settings.UseFileName("output");

        stream.Position = 0;
        await Verify(stream, "docx", settings);
    }

    [Test]
    public async Task RenderMarkdown()
    {
        #region MarkdownUsage
        var templatePath = Path.Combine(ScenarioPath("markdown-property"), "input.docx");

        var store = new TemplateStore();
        store.RegisterDocxTemplate<MarkdownDoc>("markdown-doc", templatePath);

        var model = new MarkdownDoc
        {
            Title = "Report",
            Body = "# Heading\n\nSome **bold** text."
        };

        using var stream = new MemoryStream();
        await store.Render("markdown-doc", model, stream);
        #endregion

        var settings = new VerifySettings();
        settings.UseDirectory(ScenarioPath("markdown-property"));
        settings.UseFileName("output");

        stream.Position = 0;
        await Verify(stream, "docx", settings);
    }

    [Test]
    public async Task StringSyntaxHtmlIsEquivalentToHtmlAttribute()
    {
        using var template = DocxTemplateBuilder.Build("{{ Body }}");

        var store = new TemplateStore();
        store.RegisterDocxTemplate<StringSyntaxHtmlDoc>("stringsyntax-html", template);

        var model = new StringSyntaxHtmlDoc
        {
            Title = "T",
            Body = "<p>via <i>StringSyntax</i></p>"
        };

        using var stream = new MemoryStream();
        await store.Render("stringsyntax-html", model, stream);
        stream.Position = 0;
        await Verify(stream, "docx");
    }

    [Test]
    public async Task NonSoloHtml_InlineContent_SplicesIntoHostParagraph()
    {
        // Inline-only HTML in a non-solo token — the produced single paragraph is unwrapped and
        // its runs are spliced into the host paragraph, preserving the surrounding "Prefix " text.
        using var template = DocxTemplateBuilder.Build("Prefix {{ Body }} suffix");

        var store = new TemplateStore();
        store.RegisterDocxTemplate<HtmlDoc>("inline-non-solo", template);

        var model = new HtmlDoc
        {
            Title = "T",
            Body = "<b>bold</b> and <i>italic</i>"
        };

        using var stream = new MemoryStream();
        await store.Render("inline-non-solo", model, stream);
        stream.Position = 0;
        await Verify(stream, "docx");
    }

    [Test]
    public async Task NonSoloHtml_BlockContent_SplitsHostParagraph()
    {
        // Block-level HTML (multiple `<p>`s) in a non-solo token — the host paragraph is split:
        // "Prefix " becomes its own paragraph, the produced `<p>`s slot in between, and " suffix"
        // becomes another paragraph after.
        using var template = DocxTemplateBuilder.Build("Prefix {{ Body }} suffix");

        var store = new TemplateStore();
        store.RegisterDocxTemplate<HtmlDoc>("block-non-solo", template);

        var model = new HtmlDoc
        {
            Title = "T",
            Body = "<p>First</p><p>Second</p>"
        };

        using var stream = new MemoryStream();
        await store.Render("block-non-solo", model, stream);
        stream.Position = 0;
        await Verify(stream, "docx");
    }

    [Test]
    public async Task NonSoloMarkdown_InlineContent_SplicesIntoHostParagraph()
    {
        using var template = DocxTemplateBuilder.Build("Note: {{ Body }} (end)");

        var store = new TemplateStore();
        store.RegisterDocxTemplate<MarkdownDoc>("md-inline-non-solo", template);

        var model = new MarkdownDoc
        {
            Title = "T",
            Body = "**bold** and *italic*"
        };

        using var stream = new MemoryStream();
        await store.Render("md-inline-non-solo", model, stream);
        stream.Position = 0;
        await Verify(stream, "docx");
    }

    [Test]
    public async Task NonSoloMarkdown_BlockContent_SplitsHostParagraph()
    {
        using var template = DocxTemplateBuilder.Build("Section: {{ Body }} (end)");

        var store = new TemplateStore();
        store.RegisterDocxTemplate<MarkdownDoc>("md-block-non-solo", template);

        var model = new MarkdownDoc
        {
            Title = "T",
            Body = "First paragraph.\n\nSecond paragraph."
        };

        using var stream = new MemoryStream();
        await store.Render("md-block-non-solo", model, stream);
        stream.Position = 0;
        await Verify(stream, "docx");
    }

    [Test]
    public async Task FilterOnFormatTokenIsRejected()
    {
        using var template = DocxTemplateBuilder.Build("{{ Body | upcase }}");

        var store = new TemplateStore();
        var exception = await Assert.That(
            () => store.RegisterDocxTemplate<HtmlDoc>("filter-format", template))
            .Throws<ParchmentRegistrationException>();
        await Assert.That(exception!.Message).Contains("plain member-access");
    }

    [Test]
    public async Task MismatchedHtmlAndStringSyntaxMarkdownIsRejected()
    {
        using var template = DocxTemplateBuilder.Build("{{ Body }}");

        var store = new TemplateStore();
        var exception = await Assert.That(
            () => store.RegisterDocxTemplate<MismatchDoc>("mismatch", template))
            .Throws<ParchmentRegistrationException>();
        await Assert.That(exception!.Message).Contains("mismatched");
    }

    [Test]
    public async Task TwoBlockShapedFormatTokensInSameParagraph_Throws()
    {
        // Two non-solo block-shaped structural tokens in one paragraph would require overlapping
        // host splits. ParagraphSplicer doesn't compose them — the second one must throw and
        // tell the author to put one on its own line.
        using var template = DocxTemplateBuilder.Build("Prefix {{ A }} between {{ B }} suffix");

        var store = new TemplateStore();
        store.RegisterDocxTemplate<TwoHtmlBlockDoc>("two-block", template);

        var model = new TwoHtmlBlockDoc
        {
            A = "<p>A1</p><p>A2</p>",
            B = "<p>B1</p><p>B2</p>"
        };

        using var stream = new MemoryStream();
        var exception = await Assert.That(
            async () => await store.Render("two-block", model, stream))
            .Throws<ParchmentRenderException>();
        await Assert.That(exception!.Message).Contains("Move one of the tokens to its own paragraph");
    }

    [Test]
    public async Task BothHtmlAndMarkdownIsRejected()
    {
        using var template = DocxTemplateBuilder.Build("{{ Body }}");

        var store = new TemplateStore();
        var exception = await Assert.That(
            () => store.RegisterDocxTemplate<BothDoc>("both", template))
            .Throws<ParchmentRegistrationException>();
        await Assert.That(exception!.Message).Contains("both [Html] and [Markdown]");
    }
}
