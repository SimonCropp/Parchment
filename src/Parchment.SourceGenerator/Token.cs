public sealed record Token(
    TokenKind Kind,
    string Source,
    IReadOnlyList<string[]> References,
    string? LoopVariable,
    bool HasOtherContent,
    bool IsPlainIdentifier = false);