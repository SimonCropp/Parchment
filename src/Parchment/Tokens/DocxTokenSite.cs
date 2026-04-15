sealed record DocxTokenSite(
    int Offset,
    int Length,
    string Source,
    IFluidTemplate Template,
    IReadOnlyList<IdentifierPath> References);
