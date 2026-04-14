static class GeneratorDriver
{
    const string attributeSource =
        """
        namespace Parchment
        {
            [System.AttributeUsage(System.AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
            public sealed class ParchmentTemplateAttribute : System.Attribute
            {
                public ParchmentTemplateAttribute(string templatePath, System.Type modelType)
                {
                    TemplatePath = templatePath;
                    ModelType = modelType;
                }

                public string TemplatePath { get; }
                public System.Type ModelType { get; }
            }

            public sealed class TemplateStore { }
        }
        """;

    public static GeneratorDriverRunResult Run(string userSource, params string[] templateParagraphs)
    {
        var setup = CreateDriver(userSource, templateParagraphs);
        return setup.Driver.RunGenerators(setup.Compilation).GetRunResult();
    }

    public static DriverSetup CreateDriver(string userSource, params string[] templateParagraphs) =>
        CreateDriverWithDocxes(userSource, ("template.docx", BuildDocx(templateParagraphs)));

    public static DriverSetup CreateDriverWithDocxes(
        string userSource,
        params (string FileName, byte[] Bytes)[] docxes)
    {
        var directory = Path.Combine(Path.GetTempPath(), "parchment-sg-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);

        var texts = ImmutableArray.CreateBuilder<AdditionalText>();
        var paths = ImmutableArray.CreateBuilder<string>();
        foreach (var (name, bytes) in docxes)
        {
            var path = Path.Combine(directory, name);
            File.WriteAllBytes(path, bytes);
            texts.Add(new PathAdditionalText(path));
            paths.Add(path);
        }

        var syntaxTrees = new[]
        {
            CSharpSyntaxTree.ParseText(attributeSource),
            CSharpSyntaxTree.ParseText(userSource)
        };

        var compilation = CSharpCompilation.Create(
            "GeneratorTest",
            syntaxTrees,
            BuildReferences(),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var additionalTexts = texts.ToImmutable();
        var driver = CSharpGeneratorDriver.Create(
            generators: [new ParchmentTemplateGenerator().AsSourceGenerator()],
            additionalTexts: additionalTexts,
            parseOptions: (CSharpParseOptions) syntaxTrees[0].Options,
            optionsProvider: null,
            driverOptions: new(IncrementalGeneratorOutputKind.None, trackIncrementalGeneratorSteps: true));

        return new(driver, compilation, additionalTexts, paths.ToImmutable());
    }

    public static byte[] BuildDocxBytes(params string[] paragraphs) => BuildDocx(paragraphs);

    public static AdditionalText RewriteDocx(string path, params string[] paragraphs)
    {
        File.WriteAllBytes(path, BuildDocx(paragraphs));
        return new PathAdditionalText(path);
    }

    public sealed record DriverSetup(
        CSharpGeneratorDriver Driver,
        CSharpCompilation Compilation,
        ImmutableArray<AdditionalText> AdditionalTexts,
        ImmutableArray<string> DocxPaths)
    {
        public AdditionalText DocxAdditionalText => AdditionalTexts[0];
        public string DocxPath => DocxPaths[0];
    }

    static string WriteDocx(string[] paragraphs)
    {
        var directory = Path.Combine(Path.GetTempPath(), "parchment-sg-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "template.docx");
        File.WriteAllBytes(path, BuildDocx(paragraphs));
        return path;
    }

    static byte[] BuildDocx(string[] paragraphs)
    {
        using var stream = new MemoryStream();
        using (var doc = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document))
        {
            var mainPart = doc.AddMainDocumentPart();
            mainPart.Document = new Document(new Body());
            var body = mainPart.Document.Body!;
            foreach (var text in paragraphs)
            {
                body.Append(new Paragraph(new Run(new Text(text) {Space = SpaceProcessingModeValues.Preserve})));
            }
        }

        return stream.ToArray();
    }

    static MetadataReference[] BuildReferences()
    {
        var tpa = (string?) AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") ?? "";
        return tpa
            .Split(Path.PathSeparator)
            .Where(_ => !string.IsNullOrEmpty(_))
            .Select(_ => (MetadataReference) MetadataReference.CreateFromFile(_))
            .ToArray();
    }

    sealed class PathAdditionalText(string path) :
        AdditionalText
    {
        public override string Path { get; } = path;

        public override SourceText? GetText(Cancel cancel = default) => null;
    }
}
