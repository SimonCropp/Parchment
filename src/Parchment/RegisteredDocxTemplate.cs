namespace Parchment;

internal sealed class RegisteredDocxTemplate(
    string name,
    Type modelType,
    byte[] canonicalBytes,
    IReadOnlyList<PartScopeTree> parts) :
    RegisteredTemplate(name, modelType)
{
    public byte[] CanonicalBytes { get; } = canonicalBytes;
    public IReadOnlyList<PartScopeTree> Parts { get; } = parts;

    public override byte[] Render(object model, Cancel cancel)
    {
        using var stream = DocxCloner.ToWritableStream(CanonicalBytes);
        using (var doc = WordprocessingDocument.Open(stream, true))
        {
            var mainPart = doc.MainDocumentPart
                ?? throw new ParchmentRenderException(Name, "Document has no main part");

            cancel.ThrowIfCancellationRequested();

            var context = new TemplateContext(model, SharedFluid.Options, allowModelMembers: true);

            foreach (var part in Parts)
            {
                cancel.ThrowIfCancellationRequested();
                RenderPart(doc, mainPart, part, context);
            }

            foreach (var (_, root) in DocxCloner.EnumerateParts(doc))
            {
                Anchors.StripAll(root);
            }

            doc.Save();
        }

        return stream.ToArray();
    }

    void RenderPart(WordprocessingDocument doc, MainDocumentPart mainPart, PartScopeTree part, TemplateContext context)
    {
        OpenXmlCompositeElement? root = null;
        foreach (var (uri, candidate) in DocxCloner.EnumerateParts(doc))
        {
            if (uri == part.PartUri)
            {
                root = candidate;
                break;
            }
        }

        if (root == null)
        {
            return;
        }

        var map = Anchors.BuildMap(root);
        var runner = new ScopeTreeRunner(Name, part.PartUri, map, context, mainPart);
        runner.Run(part.Nodes);
        runner.ApplyStructural();
    }
}
