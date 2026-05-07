sealed record IfBranch(
    Expression Condition,
    IReadOnlyList<RangeNode> Body);
