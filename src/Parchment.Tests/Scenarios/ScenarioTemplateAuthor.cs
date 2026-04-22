/// <summary>
/// One-shot authoring helper that writes programmatically-generated <c>input.docx</c> files into
/// scenarios whose templates are simple enough to build from <see cref="DocxTemplateBuilder"/>
/// rather than hand-authoring in Word. Marked <c>[Explicit]</c> so it only runs when a scenario
/// is added or its template is revised; the resulting <c>input.docx</c> is committed.
/// </summary>
public class ScenarioTemplateAuthor
{
    [Test, Explicit]
    public async Task WriteHtmlPropertyInput()
    {
        var bytes = DocxTemplateBuilder.Build(
            """
            Title: {{ Title }}

            {{ Body }}

            End.
            """).ToArray();
        await File.WriteAllBytesAsync(ScenarioPath("html-property"), bytes);
    }

    [Test, Explicit]
    public async Task WriteMarkdownPropertyInput()
    {
        var bytes = DocxTemplateBuilder.Build(
            """
            Title: {{ Title }}

            {{ Body }}

            End.
            """).ToArray();
        await File.WriteAllBytesAsync(ScenarioPath("markdown-property"), bytes);
    }

    static string ScenarioPath(string name) =>
        Path.GetFullPath(Path.Combine(
            Path.GetDirectoryName(SourcePath())!,
            name,
            "input.docx"));

    static string SourcePath([CallerFilePath] string path = "") => path;
}
