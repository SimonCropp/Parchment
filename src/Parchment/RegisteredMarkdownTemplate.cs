namespace Parchment;

class RegisteredMarkdownTemplate(
    string name,
    Type modelType,
    byte[] styleSourceBytes,
    IFluidTemplate parsedTemplate) :
    RegisteredTemplate(name, modelType)
{
    public byte[] StyleSourceBytes { get; } = styleSourceBytes;
    public IFluidTemplate ParsedTemplate { get; } = parsedTemplate;

    public override async Task Render(object model, Stream output, Cancel cancel)
    {
        var context = new TemplateContext(model, SharedFluid.Options, allowModelMembers: true);
        await using var writer = new StringWriter();
        await ParsedTemplate.RenderAsync(writer, System.Text.Encodings.Web.HtmlEncoder.Default, context);
        var markdownText = writer.ToString();
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
            var elements = MarkdownRendering.Render(markdownText, mainPart, headingOffset: 0);
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

        stream.Position = 0;
        await stream.CopyToAsync(output, cancel);
    }
}
