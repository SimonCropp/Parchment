sealed record LoopNode(
    string OpenAnchorName,
    string CloseAnchorName,
    string LoopVariable,
    Expression LoopSource,
    IReadOnlyList<RangeNode> Body) :
    RangeNode;
