namespace Parchment;

// Strips characters that XML 1.0 forbids (most C0 controls, lone surrogates,
// 0xFFFE/0xFFFF). Without this, any such char in a substituted model value
// crashes OpenXml's serializer with InvalidXmlChar at save time.
// TODO: revert once https://github.com/dotnet/Open-XML-SDK/issues/1532 ships —
// OpenXml itself should escape these via the OOXML _xHHHH_ convention.
static class XmlCharSanitizer
{
    public static string Strip(string value)
    {
        if (value.Length == 0)
        {
            return value;
        }

        StringBuilder? builder = null;
        var i = 0;
        while (i < value.Length)
        {
            var c = value[i];
            int advance;
            bool valid;

            if (char.IsHighSurrogate(c) &&
                i + 1 < value.Length &&
                char.IsLowSurrogate(value[i + 1]))
            {
                advance = 2;
                valid = true;
            }
            else if (char.IsSurrogate(c))
            {
                advance = 1;
                valid = false;
            }
            else
            {
                advance = 1;
                valid = c == '\t' ||
                       c == '\n' ||
                       c == '\r' ||
                       (c >= 0x20 && c <= 0xD7FF) ||
                       (c >= 0xE000 && c <= 0xFFFD);
            }

            if (valid)
            {
                builder?.Append(value, i, advance);
            }
            else if (builder == null)
            {
                builder = new(value.Length);
                builder.Append(value, 0, i);
            }

            i += advance;
        }

        return builder?.ToString() ?? value;
    }
}
