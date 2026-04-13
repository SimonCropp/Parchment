namespace Parchment.Tokens;

internal abstract record RangeNode;

internal sealed record SubstitutionNode(
    string AnchorName,
    IReadOnlyList<DocxTokenSite> Tokens) :
    RangeNode;

internal sealed record StaticNode(string AnchorName) :
    RangeNode;

internal sealed record LoopNode(
    string OpenAnchorName,
    string CloseAnchorName,
    RangeScopeKind Scope,
    string LoopVariable,
    IFluidTemplate LoopSource,
    IReadOnlyList<RangeNode> Body) :
    RangeNode;

internal sealed record IfNode(
    string OpenAnchorName,
    string CloseAnchorName,
    IReadOnlyList<IfBranch> Branches,
    IReadOnlyList<RangeNode> ElseBody) :
    RangeNode;

internal sealed record IfBranch(
    string AnchorName,
    IFluidTemplate Condition,
    IReadOnlyList<RangeNode> Body);

internal enum RangeScopeKind
{
    Paragraph
}

internal sealed record PartScopeTree(
    string PartUri,
    IReadOnlyList<RangeNode> Nodes);
