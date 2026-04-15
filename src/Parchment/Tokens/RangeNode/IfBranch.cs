namespace Parchment.Tokens;

sealed record IfBranch(
    string AnchorName,
    Expression Condition,
    IReadOnlyList<RangeNode> Body);
