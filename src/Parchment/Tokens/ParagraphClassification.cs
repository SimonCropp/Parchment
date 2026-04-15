sealed record ParagraphClassification(
    Paragraph Paragraph,
    string AnchorName,
    ParagraphKind Kind,
    IReadOnlyList<DocxTokenSite> Substitutions,
    BlockMarker? Block);
