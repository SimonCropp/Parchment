public sealed record Token(
    TokenKind Kind,
    string Source,
    List<List<string>> References,
    string? LoopVariable,
    bool HasOtherContent,
    bool IsPlainIdentifier = false);
