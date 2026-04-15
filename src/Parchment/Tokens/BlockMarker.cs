sealed record BlockMarker(
    BlockTagKind Kind,
    string Source,
    string? Expression,
    Expression? Condition,
    string? LoopVariable,
    Expression? LoopSource,
    IReadOnlyList<IdentifierPath> References);
