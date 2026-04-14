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
        var docxPath = WriteDocx(templateParagraphs);

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

        var additionalTexts = ImmutableArray.Create<AdditionalText>(new PathAdditionalText(docxPath));

        var driver = CSharpGeneratorDriver
            .Create(new ParchmentTemplateGenerator())
            .AddAdditionalTexts(additionalTexts)
            .RunGenerators(compilation);

        return driver.GetRunResult();
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
