namespace Parchment.Tokens;

sealed record StaticNode(string AnchorName) :
    RangeNode;
