sealed record BlockMarker(
    BlockTagKind Kind,
    string Source,
    Expression? Condition,
    string? LoopVariable,
    Expression? LoopSource);
