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
    Fluid.Ast.Expression? Condition,
    string? LoopVariable,
    Fluid.Ast.Expression? LoopSource,
    IReadOnlyList<IdentifierPath> References);
