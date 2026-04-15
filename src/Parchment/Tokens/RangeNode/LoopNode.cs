namespace Parchment.Tokens;

sealed record LoopNode(
    string OpenAnchorName,
    string CloseAnchorName,
    RangeScopeKind Scope,
    string LoopVariable,
    Expression LoopSource,
    IReadOnlyList<RangeNode> Body) :
    RangeNode;
