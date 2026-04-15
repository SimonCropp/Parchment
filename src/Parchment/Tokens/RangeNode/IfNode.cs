namespace Parchment.Tokens;

sealed record IfNode(
    string OpenAnchorName,
    string CloseAnchorName,
    IReadOnlyList<IfBranch> Branches,
    IReadOnlyList<RangeNode> ElseBody) :
    RangeNode;
