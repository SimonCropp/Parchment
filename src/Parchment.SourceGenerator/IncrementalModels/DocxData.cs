sealed record DocxData(
    string Path,
    EquatableArray<string> Paragraphs,
    string? ReadError);
