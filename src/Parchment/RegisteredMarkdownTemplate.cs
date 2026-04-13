namespace Parchment;

internal sealed class RegisteredMarkdownTemplate(
    string name,
    Type modelType,
    byte[] styleSourceBytes,
    IFluidTemplate parsedTemplate) :
    RegisteredTemplate(name, modelType)
{
    public byte[] StyleSourceBytes { get; } = styleSourceBytes;
    public IFluidTemplate ParsedTemplate { get; } = parsedTemplate;

    public override byte[] Render(object model, Cancel cancel)
    {
        var context = new TemplateContext(model, SharedFluid.Options, allowModelMembers: true);
        var markdownText = ParsedTemplate.Render(context);
        cancel.ThrowIfCancellationRequested();

        using var stream = DocxCloner.ToWritableStream(StyleSourceBytes);
        using (var doc = WordprocessingDocument.Open(stream, true))
        {
            var mainPart = doc.MainDocumentPart
                ?? throw new ParchmentRenderException(Name, "Document has no main part");
            var document = mainPart.Document
                ?? throw new ParchmentRenderException(Name, "Document has no document part");
            var body = document.Body
                ?? throw new ParchmentRenderException(Name, "Document has no body");

            var sectPr = body.Elements<SectionProperties>().LastOrDefault()
                         ?? body.Descendants<SectionProperties>().LastOrDefault();
            sectPr?.Remove();
            body.RemoveAllChildren();

            cancel.ThrowIfCancellationRequested();
            var elements = Markdown.MarkdownRendering.Render(markdownText, mainPart, headingOffset: 0);
            foreach (var element in elements)
            {
                body.AppendChild(element);
            }

            if (sectPr != null)
            {
                body.AppendChild(sectPr);
            }

            doc.Save();
        }

        return stream.ToArray();
    }
}
