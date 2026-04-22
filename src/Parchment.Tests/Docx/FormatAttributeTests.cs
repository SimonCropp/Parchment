using System.Diagnostics.CodeAnalysis;

public class FormatAttributeTests
{
    static string SourcePath([CallerFilePath] string path = "") => path;

    static string ScenarioPath(string scenarioName) =>
        Path.GetFullPath(Path.Combine(
            Path.GetDirectoryName(SourcePath())!,
            "..",
            "Scenarios",
            scenarioName));

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
        using var template = DocxTemplateBuilder.Build(
            """
            {{ Body }}
            """);

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
    public async Task MixedInlineContentIsRejected()
    {
        using var template = DocxTemplateBuilder.Build(
            """
            Prefix {{ Body }}
            """);

        var store = new TemplateStore();
        var exception = await Assert.That(
            () => store.RegisterDocxTemplate<HtmlDoc>("mixed-format", template))
            .Throws<ParchmentRegistrationException>();
        await Assert.That(exception!.Message).Contains("must sit alone in its own paragraph");
    }

    [Test]
    public async Task FilterOnFormatTokenIsRejected()
    {
        using var template = DocxTemplateBuilder.Build(
            """
            {{ Body | upcase }}
            """);

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
