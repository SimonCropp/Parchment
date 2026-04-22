public sealed record Token(
    TokenKind Kind,
    string Source,
    IReadOnlyList<string[]> References,
    string? LoopVariable,
    string? LoopSource,
    string Paragraph,
    bool HasOtherContent,
    bool IsPlainIdentifier = false);