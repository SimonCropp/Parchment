namespace Parchment.Tokens;

internal enum BlockTagKind
{
    For,
    EndFor,
    If,
    ElsIf,
    Else,
    EndIf
}

internal sealed record BlockMarker(
    BlockTagKind Kind,
    string Source,
    string? Expression,
    IFluidTemplate? Condition,
    string? LoopVariable,
    IFluidTemplate? LoopSource,
    IReadOnlyList<IdentifierPath> References);
