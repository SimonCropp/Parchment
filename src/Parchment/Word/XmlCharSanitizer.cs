using System.Buffers;

namespace Parchment;

// Strips characters that XML 1.0 forbids (most C0 controls, lone surrogates,
// 0xFFFE/0xFFFF). Without this, any such char in a substituted model value
// crashes OpenXml's serializer with InvalidXmlChar at save time.
// TODO: revert once https://github.com/dotnet/Open-XML-SDK/issues/1532 ships —
// OpenXml itself should escape these via the OOXML _xHHHH_ convention.
static class XmlCharSanitizer
{
    // Set of chars that need closer inspection: forbidden C0 controls, all surrogates
    // (a high+low pair is valid, but recognizing that requires the per-char loop), and
    // 0xFFFE/0xFFFF. Anything outside this set is unconditionally valid for XML 1.0,
    // so a single vectorized IndexOfAny short-circuits the dominant case (substituted
    // values like numbers, names, dates, plain-text — none contain these chars).
    static readonly SearchValues<char> NeedsInspection = SearchValues.Create(BuildNeedsInspection());

    static char[] BuildNeedsInspection()
    {
        var list = new List<char>(2080);
        // C0 controls below 0x20 except tab (0x09), LF (0x0A), CR (0x0D).
        for (var c = 0x00; c <= 0x08; c++)
        {
            list.Add((char)c);
        }
        list.Add((char)0x0B);
        list.Add((char)0x0C);
        for (var c = 0x0E; c <= 0x1F; c++)
        {
            list.Add((char)c);
        }
        // Surrogate range — pair-checking happens in the slow path.
        for (var c = 0xD800; c <= 0xDFFF; c++)
        {
            list.Add((char)c);
        }
        // Non-character code points.
        list.Add((char)0xFFFE);
        list.Add((char)0xFFFF);
        return list.ToArray();
    }

    public static string Strip(string value)
    {
        if (value.Length == 0)
        {
            return value;
        }

        // Fast path: vectorized scan. If no char in the string falls in the inspection set,
        // the value is guaranteed XML-valid and is returned as-is — no allocation.
        if (value.AsSpan().IndexOfAny(NeedsInspection) < 0)
        {
            return value;
        }

        return StripSlow(value);
    }

    static string StripSlow(string value)
    {
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
