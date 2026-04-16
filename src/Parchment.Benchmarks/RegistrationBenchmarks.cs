using BenchmarkDotNet.Attributes;

[Config(typeof(BenchmarkConfig))]
public class RegistrationBenchmarks
{
    byte[] templateBytes = null!;

    [GlobalSetup]
    public void Setup() =>
        templateBytes = BuildTemplate();

    [Benchmark]
    public void RegisterFromMemoryStream()
    {
        using var ms = new MemoryStream(templateBytes);
        var store = new TemplateStore();
        store.RegisterDocxTemplate<Invoice>("bench", ms);
    }

    [Benchmark]
    public void RegisterFromBufferedStream()
    {
        using var ms = new MemoryStream(templateBytes);
        using var buffered = new BufferedStream(ms);
        var store = new TemplateStore();
        store.RegisterDocxTemplate<Invoice>("bench", buffered);
    }

    [Benchmark]
    public void RegisterFromFilePath()
    {
        var path = Path.GetTempFileName();
        try
        {
            File.WriteAllBytes(path, templateBytes);
            var store = new TemplateStore();
            store.RegisterDocxTemplate<Invoice>("bench", path);
        }
        finally
        {
            File.Delete(path);
        }
    }

    static byte[] BuildTemplate()
    {
        using var stream = new MemoryStream();
        using (var doc = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document))
        {
            var mainPart = doc.AddMainDocumentPart();
            mainPart.Document = new(new Body(
                new Paragraph(new Run(new Text("Invoice {{ Number }}") { Space = SpaceProcessingModeValues.Preserve })),
                new Paragraph(new Run(new Text("Customer: {{ Customer.Name }}") { Space = SpaceProcessingModeValues.Preserve })),
                new Paragraph(new Run(new Text("Total: {{ Total }} {{ Currency }}") { Space = SpaceProcessingModeValues.Preserve }))));

            var stylesPart = mainPart.AddNewPart<StyleDefinitionsPart>();
            var styles = new Styles();
            styles.Append(new Style { Type = StyleValues.Paragraph, StyleId = "Normal", Default = true }
                .AppendChild(new StyleName { Val = "Normal" }).Parent!);
            stylesPart.Styles = styles;
        }

        return stream.ToArray();
    }
}
