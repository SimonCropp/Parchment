sealed record SubstitutionNode(
    string AnchorName,
    IReadOnlyList<DocxTokenSite> Tokens) :
    RangeNode;
