sealed record PartScopeTree(
    string PartUri,
    IReadOnlyList<RangeNode> Nodes);
