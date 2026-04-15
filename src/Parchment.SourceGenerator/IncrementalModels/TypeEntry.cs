sealed record TypeEntry(
    string TypeFullyQualifiedName,
    string? ElementTypeFullyQualifiedName,
    EquatableArray<MemberEntry> Members);
