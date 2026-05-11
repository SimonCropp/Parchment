// The SG project has no reference to Parchment.dll, so it can't see `FormatMapKind`. Mirror
// the two values locally — emission only needs the string name to print into the generated code.
enum FormatMapKind
{
    Html,
    Markdown
}