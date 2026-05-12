static class StringBuilderExtensions
{
    public static StringBuilder Indent(this StringBuilder builder, int depth) =>
        builder.Append(' ', depth * 2);

    public static void TrimTrailingNewlines(this StringBuilder builder)
    {
        while (builder.Length > 0 && builder[^1] is '\r' or '\n')
        {
            builder.Length--;
        }
    }
}
