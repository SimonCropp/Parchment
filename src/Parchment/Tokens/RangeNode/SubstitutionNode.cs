namespace Parchment.Tokens;

sealed record SubstitutionNode(
    string AnchorName,
    IReadOnlyList<DocxTokenSite> Tokens) :
    RangeNode;
