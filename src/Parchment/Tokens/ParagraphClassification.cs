namespace Parchment.Tokens;

internal enum ParagraphKind
{
    Static,
    Substitution,
    Block
}

internal sealed record ParagraphClassification(
    Paragraph Paragraph,
    string AnchorName,
    ParagraphKind Kind,
    IReadOnlyList<DocxTokenSite> Substitutions,
    BlockMarker? Block);
